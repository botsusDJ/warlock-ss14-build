using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Exosuits;

/// <summary>
/// _Warlock — класс рамы. Определяет, кому она вообще по карману и насколько опасна.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockExoFrame : byte
{
    /// <summary>Малая рама. Технос и Варлок дальше неё не продвинулись.</summary>
    Small = 0,

    /// <summary>Обычная боевая рама Братства.</summary>
    Standard = 1,

    /// <summary>Тяжёлая штурмовая.</summary>
    Heavy = 2,

    /// <summary>Опытная. Мощнее всех и ломается непредсказуемо.</summary>
    Experimental = 3,

    /// <summary>Прото-рама унатхов. Собрана не под человека и греется как печь.</summary>
    Proto = 4,
}

/// <summary>
/// _Warlock — режим охлаждения. Настраивается в ОС.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockExoCooling : byte
{
    /// <summary>Пассивное. Не тратит заряд, остывает медленно.</summary>
    Passive = 0,

    /// <summary>Активное. Остывает вдвое быстрее, но само ест заряд.</summary>
    Active = 1,
}

/// <summary>
/// _Warlock
/// Экзоскелет.
///
/// Братство не умеет строить, оно умеет сваривать. Рама — это чужие приводы на чужой
/// батарее, и ведёт она себя соответственно: пока есть заряд, боец бьёт заметно сильнее
/// и не чувствует веса. Села батарея — рама превращается в железо на плечах.
///
/// Вокруг этого построено всё остальное. Приводы жрут заряд и греются, жар надо куда-то
/// девать, а распределение мощности между кулаками и тем, что в руках, боец выбирает сам
/// заранее — в бою переключаться уже поздно.
///
/// Питание, слот батареи и само включение сделаны ванильными узлами: PowerCellSlot,
/// PowerCellDraw, ToggleCellDraw, ItemToggle. Своего здесь только то, чего в ванили нет.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockExosuitComponent : Component
{
    /// <summary>
    /// Класс рамы. На механику влияет косвенно — через числа ниже, — но нужен
    /// для описаний и для того, чтобы отличать опытные рамы от серийных.
    /// </summary>
    [DataField, AutoNetworkedField]
    public WarlockExoFrame Frame = WarlockExoFrame.Standard;

    #region Сила

    /// <summary>
    /// Полная прибавка к удару, когда вся мощность отдана в этот канал.
    /// Фактическая прибавка делится между кулаками и предметом, см. распределение.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StrengthBonus = 1.35f;

    /// <summary>
    /// Скорость под питанием. Чуть меньше единицы: рама помогает, но не невесома.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PoweredWalk = 0.95f;

    [DataField, AutoNetworkedField]
    public float PoweredSprint = 0.95f;

    /// <summary>
    /// Скорость обесточенной рамы. Это и есть цена вопроса.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DeadWalk = 0.55f;

    [DataField, AutoNetworkedField]
    public float DeadSprint = 0.5f;

    /// <summary>
    /// Слот, в котором рама вообще работает.
    /// </summary>
    [DataField]
    public string Slot = "outerClothing";

    #endregion

    #region Распределение мощности

    /// <summary>
    /// Доля мощности, уходящая в кулаки, от нуля до единицы. Остаток идёт в кисти —
    /// то есть в то, что боец держит.
    ///
    /// Одно число вместо двух намеренно: два независимых ползунка позволяли бы
    /// выкрутить оба на максимум, и выбора бы не было. Здесь мощность именно
    /// делится, и усилить хват можно только ослабив удар.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FistShare = 0.5f;

    /// <summary>
    /// Насколько сильно перекос помогает своему каналу. При 0.6 полностью
    /// выкрученный канал получает на шестьдесят процентов больше базовой прибавки.
    /// </summary>
    [DataField]
    public float ShareSwing = 0.6f;

    #endregion

    #region Жар

    /// <summary>
    /// Текущий нагрев, от нуля до <see cref="MaxHeat"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Heat;

    [DataField, AutoNetworkedField]
    public float MaxHeat = 100f;

    /// <summary>
    /// Сколько жара даёт секунда работы под нагрузкой.
    /// </summary>
    [DataField]
    public float HeatPerSecond = 2.2f;

    /// <summary>
    /// Сколько жара добавляет один удар. Драка греет раму сильнее, чем ходьба:
    /// иначе перегрев ловился бы только у того, кто просто долго ходит включённым.
    /// </summary>
    [DataField]
    public float HeatPerSwing = 3.5f;

    /// <summary>
    /// Сколько жара уходит в секунду при пассивном охлаждении.
    /// </summary>
    [DataField]
    public float CoolPerSecond = 1.6f;

    /// <summary>
    /// Во сколько раз быстрее остывает активное охлаждение.
    /// </summary>
    [DataField]
    public float ActiveCoolFactor = 2.4f;

    /// <summary>
    /// Сколько заряда в секунду дополнительно ест активное охлаждение.
    /// </summary>
    [DataField]
    public float ActiveCoolDraw = 1.5f;

    /// <summary>
    /// Порог, с которого рама начинает жечь носителя.
    /// </summary>
    [DataField]
    public float ScorchAt = 70f;

    /// <summary>
    /// Урон жаром в секунду выше порога.
    /// </summary>
    [DataField]
    public float ScorchDamage = 1.2f;

    #endregion

    #region Выброс

    /// <summary>
    /// Ограничитель. Включённый гасит раму на пороге вместо выброса — безопасно,
    /// но в разгар боя вы просто остаётесь без приводов.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Limiter = true;

    /// <summary>
    /// Режим охлаждения.
    /// </summary>
    [DataField, AutoNetworkedField]
    public WarlockExoCooling Cooling = WarlockExoCooling.Passive;

    /// <summary>
    /// Радиус выброса в тайлах.
    /// </summary>
    [DataField]
    public float DischargeRange = 3.5f;

    /// <summary>
    /// Урон выброса всем вокруг, включая носителя.
    /// </summary>
    [DataField]
    public float DischargeDamage = 22f;

    /// <summary>
    /// Сколько заряда сжигает выброс. Опытные рамы сжигают всё.
    /// </summary>
    [DataField]
    public float DischargeCost = 300f;

    /// <summary>
    /// Шанс выброса на пороге даже при включённом ограничителе. У серийных рам
    /// ноль, у опытных заметный: в этом и состоит их опытность.
    /// </summary>
    [DataField]
    public float UnstableChance;

    #endregion

    #region Отрывание конечностей

    /// <summary>
    /// Сила рамы для отрывания конечностей. Сравнивается с сопротивлением жертвы:
    /// не хватило — конечность только ломается.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TearStrength = 1f;

    #endregion

    /// <summary>
    /// Когда система в следующий раз посчитает жар и расход.
    /// </summary>
    [DataField]
    public TimeSpan NextTick;

    [DataField]
    public float TickInterval = 1f;
}

/// <summary>
/// _Warlock
/// Висит на том, кто носит раму.
///
/// Нужен затем, чтобы прибавку к удару не искать перебором инвентаря на каждый замах:
/// удар — самое частое событие в бою. Здесь лежат уже посчитанные числа.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockExosuitWearerComponent : Component
{
    /// <summary>Множитель урона голыми руками.</summary>
    [DataField, AutoNetworkedField]
    public float FistBonus = 1f;

    /// <summary>Множитель урона предметом в руках.</summary>
    [DataField, AutoNetworkedField]
    public float ToolBonus = 1f;

    /// <summary>Сила для отрывания конечностей. Ноль означает, что рама мертва.</summary>
    [DataField, AutoNetworkedField]
    public float TearStrength;

    /// <summary>Сама рама — чтобы система отрывания могла греть её за работу.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Suit;
}

/// <summary>
/// _Warlock
/// Ячейка экзоскелета.
///
/// Обычная батарея тоже подойдёт, но специальная меняет характер рамы: одна даёт
/// больше мощности ценой нагрева, другая холодная и слабая. Вешается поверх
/// ванильного Battery, поэтому ёмкость остаётся ванильной.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockExoCellComponent : Component
{
    /// <summary>Множитель прибавки к силе.</summary>
    [DataField, AutoNetworkedField]
    public float Output = 1f;

    /// <summary>Множитель нагрева.</summary>
    [DataField, AutoNetworkedField]
    public float HeatFactor = 1f;

    /// <summary>Прибавка к шансу выброса.</summary>
    [DataField, AutoNetworkedField]
    public float UnstableBonus;
}

/// <summary>
/// _Warlock — ключ интерфейса ОС экзоскелета.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockExoOsUiKey : byte
{
    Key,
}

/// <summary>
/// _Warlock — что ОС показывает на экране.
/// </summary>
[Serializable, NetSerializable]
public sealed class WarlockExoOsState(
    float fistShare,
    bool limiter,
    WarlockExoCooling cooling,
    float heat,
    float maxHeat,
    float charge,
    float maxCharge,
    WarlockExoFrame frame,
    bool active) : BoundUserInterfaceState
{
    public readonly float FistShare = fistShare;
    public readonly bool Limiter = limiter;
    public readonly WarlockExoCooling Cooling = cooling;
    public readonly float Heat = heat;
    public readonly float MaxHeat = maxHeat;
    public readonly float Charge = charge;
    public readonly float MaxCharge = maxCharge;
    public readonly WarlockExoFrame Frame = frame;
    public readonly bool Active = active;
}

/// <summary>
/// _Warlock — игрок поменял настройку в ОС.
/// </summary>
[Serializable, NetSerializable]
public sealed class WarlockExoOsSetMessage(float fistShare, bool limiter, WarlockExoCooling cooling)
    : BoundUserInterfaceMessage
{
    public readonly float FistShare = fistShare;
    public readonly bool Limiter = limiter;
    public readonly WarlockExoCooling Cooling = cooling;
}
