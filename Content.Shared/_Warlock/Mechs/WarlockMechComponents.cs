using Content.Shared.Actions;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Mechs;

// _Warlock
// Мехи Братства Стали.
//
// Братство не проектирует машины, оно их доваривает. Отсюда всё устройство: мех — это
// рама, на которую по узлам навешано то, что нашлось, и каждый узел можно снять, потерять,
// расплавить или уронить в песок. Целого меха в Братстве не существует — существует мех,
// у которого пока всё на месте.
//
// Слой лежит поверх ванильной механики мехов, а не заменяет её. Пилот, батарея, слоты
// оружия и интерфейс ванильные; своё здесь только то, чего в ванили нет:
//
//   экипаж на двоих     — водитель и стрелок, и разница между ними осязаема;
//   неповоротливость    — разворот стоит времени, а не бесплатен;
//   смазка              — расходник, без которого рама ходит, но плохо;
//   узлы                — детали снимаются, отлетают от урона, плавятся и грязнятся.

/// <summary>
/// _Warlock — пост, на котором сидит одиночный водитель.
///
/// Мест в кабине два: рычаги и прицелы. Один человек физически не достаёт до обоих,
/// поэтому в одиночку он выбирает, чем занят прямо сейчас. Вдвоём выбирать не нужно.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockMechStation : byte
{
    /// <summary>За рычагами. Мех ходит, орудия заперты.</summary>
    Drive = 0,

    /// <summary>За прицелами. Орудия работают, мех стоит.</summary>
    Gun = 1,
}

/// <summary>
/// _Warlock — класс рамы. Определяет, кому она по карману и что от неё ждать.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockMechClass : byte
{
    /// <summary>Механизатор. Рабочая паучья рама, в бою почти бесполезна.</summary>
    Worker = 0,

    /// <summary>Легионер. Линейная двуногая, основная масса Братства.</summary>
    Line = 1,

    /// <summary>Трибун. Командная, тяжёлая и дорогая.</summary>
    Tribune = 2,

    /// <summary>Мекантек. Штурмовая, самая тяжёлая из всех.</summary>
    Mechantek = 3,
}

/// <summary>
/// _Warlock
/// Мех Братства. Вешается поверх ванильного MechComponent.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockMechComponent : Component
{
    [DataField, AutoNetworkedField]
    public WarlockMechClass Class = WarlockMechClass.Line;

    #region Экипаж

    /// <summary>
    /// Второе место в кабине. Ванильный слот пилота держит водителя, этот — стрелка.
    /// </summary>
    [ViewVariables]
    public ContainerSlot GunnerSlot = default!;

    [ViewVariables]
    public readonly string GunnerSlotId = "warlock-mech-gunner-slot";

    /// <summary>
    /// Пост одиночного водителя. Пока стрелка нет, водитель или ходит, или стреляет.
    /// </summary>
    [DataField, AutoNetworkedField]
    public WarlockMechStation Station = WarlockMechStation.Drive;

    /// <summary>Сколько секунд занимает влезть на место стрелка.</summary>
    [DataField]
    public float GunnerEntryDelay = 4f;

    #endregion

    #region Неповоротливость

    /// <summary>
    /// Сколько секунд рама разворачивается, когда её направляют в другую сторону.
    ///
    /// Разворот стоит времени, а не скорости: замедленный мех всё равно можно
    /// водить как человека, а мех, который надо разворачивать, приходится выводить
    /// на позицию заранее. Это и делает его машиной, а не тяжёлым человеком.
    /// </summary>
    [DataField]
    public float TurnTime = 0.55f;

    /// <summary>
    /// На сколько градусов надо отклониться, чтобы это считалось разворотом.
    /// Шаг вбок рама делает без задержки, разворот назад — нет.
    /// </summary>
    [DataField]
    public float TurnAngle = 80f;

    /// <summary>Куда рама смотрит сейчас, в радианах. Служебное.</summary>
    [DataField]
    public float Facing;

    /// <summary>Пока не истечёт — рама стоит и доворачивается.</summary>
    [DataField]
    public TimeSpan TurnUntil;

    #endregion

    #region Смазка

    /// <summary>
    /// Сколько смазки в системе, от нуля до <see cref="MaxLubricant"/>.
    ///
    /// Смазка — не топливо: на нуле рама не встаёт. Она начинает жрать саму себя.
    /// Сухая рама ходит медленнее, разворачивается дольше и стирает собственные узлы,
    /// так что экономия на смазке оплачивается заменой ног.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Lubricant = 100f;

    [DataField, AutoNetworkedField]
    public float MaxLubricant = 100f;

    /// <summary>Сколько смазки уходит за секунду хода.</summary>
    [DataField]
    public float LubricantPerSecond = 0.22f;

    /// <summary>Ниже этой доли рама считается сухой и начинает страдать.</summary>
    [DataField]
    public float DryBelow = 0.25f;

    /// <summary>Множитель скорости у сухой рамы.</summary>
    [DataField]
    public float DrySpeed = 0.7f;

    /// <summary>Во сколько раз дольше сухая рама разворачивается.</summary>
    [DataField]
    public float DryTurn = 1.8f;

    /// <summary>Сколько износа за такт получает случайный узел сухой рамы.</summary>
    [DataField]
    public float DryWear = 1.4f;

    #endregion

    #region Узлы

    /// <summary>
    /// Контейнер установленных узлов. Ванильный EquipmentContainer держит оружие,
    /// этот — ноги, кабину и реактор.
    /// </summary>
    [ViewVariables]
    public Container PartContainer = default!;

    [ViewVariables]
    public readonly string PartContainerId = "warlock-mech-part-container";

    /// <summary>
    /// Какие узлы рама обязана иметь, чтобы ходить. Ключ — идентификатор гнезда.
    /// Проверяется по установленным узлам: нет ступни — рама не ходит.
    /// </summary>
    [DataField]
    public List<string> RequiredSlots = new();

    /// <summary>
    /// Сколько урона по раме нужно накопить, чтобы с неё сорвало узел.
    /// </summary>
    [DataField]
    public float PartLossThreshold = 45f;

    /// <summary>Накоплено урона с прошлого сорванного узла. Служебное.</summary>
    [DataField]
    public float DamageSincePartLoss;

    /// <summary>
    /// Насколько сильно изношенные узлы тормозят раму. Итоговый множитель скорости —
    /// единица минус средний износ, умноженный на это число.
    /// </summary>
    [DataField]
    public float WearSlowdown = 0.45f;

    #endregion

    #region Действия

    /// <summary>
    /// Переключение поста. Выдаётся водителю при посадке.
    /// </summary>
    [DataField]
    public EntProtoId StationAction = "WarlockActionMechStation";

    /// <summary>
    /// Смена орудия. Выдаётся стрелку: сам выстрел идёт обычным кликом
    /// через ретрансляцию, а вот выбрать, из чего стрелять, надо чем-то.
    /// </summary>
    [DataField]
    public EntProtoId GunnerCycleAction = "WarlockActionMechGunnerCycle";

    [DataField]
    public EntityUid? StationActionEntity;

    [DataField]
    public EntityUid? GunnerCycleActionEntity;

    #endregion

    /// <summary>Когда система в следующий раз посчитает смазку и износ.</summary>
    [DataField]
    public TimeSpan NextTick;

    [DataField]
    public float TickInterval = 1f;
}

/// <summary>
/// _Warlock
/// Узел меха: нога по частям, кабина, реактор, оружейный пилон.
///
/// Нога намеренно не одна деталь. Основание, коленный узел и ступня ломаются по-разному
/// и стоят разного: ступню в песке стирает за смену, а основание переживает раму.
/// Из-за этого у Братства есть ремонт как занятие, а не как кнопка.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockMechPartComponent : Component
{
    /// <summary>
    /// В какое гнездо рамы этот узел встаёт. Гнездо занято ровно одним узлом.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public string Slot = string.Empty;

    /// <summary>
    /// К какой раме подходит. Пустая строка — подходит к любой,
    /// но таких узлов у Братства почти нет.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Chassis = string.Empty;

    #region Износ

    /// <summary>
    /// Грязь, от нуля до сотни. Копится, пока узел валяется на земле, и мешает,
    /// пока стоит на раме. Стирается ветошью.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Dirt;

    /// <summary>
    /// Оплавление, от нуля до сотни. Копится от жара — и на раме, и на полу
    /// в горящей комнате. Не лечится ничем: оплавленный узел меняют.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Melt;

    /// <summary>
    /// Насколько узел важен для хода. Ноль — узел ни на что не влияет
    /// (пилон), единица — рама без него не ходит вовсе (ступня).
    /// </summary>
    [DataField]
    public float Weight = 1f;

    /// <summary>
    /// Множитель скорости загрязнения. У ступни он больше единицы:
    /// она стоит в песке, а не висит над ним.
    /// </summary>
    [DataField]
    public float DirtRate = 1f;

    /// <summary>
    /// Множитель скорости оплавления.
    /// </summary>
    [DataField]
    public float MeltRate = 1f;

    #endregion
}

/// <summary>
/// _Warlock — сколько грязи даёт этот тайл за такт.
///
/// Отдельным компонентом на прототипе тайла было бы правильнее, но тайлы в движке
/// не сущности, и вешать на них нечего. Поэтому таблица лежит на самой системе,
/// а здесь только её единица.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct WarlockDirtRate(string Tile, float Rate);

/// <summary>
/// _Warlock
/// Рама в сборке.
///
/// Собирается не ванильным графом конструкции, а вставкой узлов: граф пришлось бы
/// расписывать на каждый мех шагами с визуализатором, а узлов у нас по десятку на раму,
/// и порядок сборки не важен. Здесь важно только то, что все гнёзда заполнены.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockMechFrameComponent : Component
{
    /// <summary>
    /// Во что рама превратится, когда все гнёзда будут заняты.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Result;

    /// <summary>
    /// Какое шасси принимает эта рама. Узел от другого меха в неё не встанет.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public string Chassis = string.Empty;

    /// <summary>
    /// Какие гнёзда надо заполнить.
    /// </summary>
    [DataField(required: true)]
    public List<string> Slots = new();

    [ViewVariables]
    public Container PartContainer = default!;

    [ViewVariables]
    public readonly string PartContainerId = "warlock-mech-frame-container";
}

/// <summary>
/// _Warlock — ветошь. Стирает грязь с узлов и больше ничего не умеет.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockMechRagComponent : Component
{
    /// <summary>Сколько грязи снимает одно протирание.</summary>
    [DataField]
    public float Clean = 45f;

    /// <summary>Сколько раз ветошью можно воспользоваться, пока она не сгниёт.</summary>
    [DataField, AutoNetworkedField]
    public int Uses = 6;

    [DataField]
    public float Delay = 2.5f;
}

/// <summary>
/// _Warlock — канистра смазки. Заливается в раму.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockMechGreaseComponent : Component
{
    /// <summary>Сколько смазки в канистре.</summary>
    [DataField, AutoNetworkedField]
    public float Amount = 100f;

    /// <summary>Сколько заливается за один раз.</summary>
    [DataField]
    public float Pour = 50f;

    [DataField]
    public float Delay = 4f;
}

// ==================================================================================================
// События
// ==================================================================================================

/// <summary>
/// _Warlock — водитель переключает пост между рычагами и прицелами.
/// </summary>
public sealed partial class WarlockMechStationEvent : InstantActionEvent;

/// <summary>
/// _Warlock — стрелок переключает орудие.
///
/// Отдельного действия «выстрелить» нет намеренно: стрелок стреляет обычным кликом,
/// потому что его взаимодействия ретранслируются на мех ровно так же, как у пилота.
/// Второй путь огня был бы вторым источником правды о том, что и куда стреляет.
/// </summary>
public sealed partial class WarlockMechGunnerCycleEvent : InstantActionEvent;
