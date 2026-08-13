using Content.Shared.Damage.Prototypes;
using Content.Shared.Explosion;
using Robust.Shared.Prototypes;

namespace Content.Server._Warlock.Psionics.Components;

/// <summary>
/// _Warlock — активная «Проклятая Хватка».
/// Пока висит, всё взятое в руки разрывает, а носитель затягивает свои раны.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockCursedGraspComponent : Component
{
    [DataField]
    public TimeSpan EndAt;

    [DataField]
    public TimeSpan NextTick;

    /// <summary>
    /// Сколько урона снимается с носителя за тик.
    /// </summary>
    [DataField]
    public float HealPerTick = 4f;

    /// <summary>
    /// Мощность разрыва предмета.
    /// </summary>
    [DataField]
    public float ExplosionIntensity = 8f;

    [DataField]
    public ProtoId<ExplosionPrototype> ExplosionType = "Default";
}

/// <summary>
/// _Warlock — активное «Чутьё Реликвий».
/// Раз в несколько секунд подсказывает носителю сторону и расстояние до ближайшего артефакта.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockRelicScentComponent : Component
{
    [DataField]
    public TimeSpan EndAt;

    [DataField]
    public TimeSpan NextTick;

    /// <summary>
    /// Сколько подсказок осталось.
    ///
    /// Срок жизни считается подсказками, а не только часами: счётчик уменьшается ровно там,
    /// где показывается всплывашка, и промахнуться мимо конца невозможно. EndAt оставлен
    /// подстраховкой на случай, если носителя заморозят и тики перестанут идти.
    /// </summary>
    [DataField]
    public int TicksLeft;

    [DataField]
    public float TickInterval = 3f;

    [DataField]
    public float Radius = 60f;
}

/// <summary>
/// _Warlock — активный «Погребальный Костёр».
/// Источник жара — сам носитель, поэтому область считается от его текущего положения каждый тик.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockPyreAuraComponent : Component
{
    [DataField]
    public TimeSpan EndAt;

    [DataField]
    public TimeSpan NextTick;

    [DataField]
    public float TickInterval = 1f;

    [DataField]
    public float Radius = 3f;

    /// <summary>
    /// Целевая температура тайлов вокруг, в кельвинах.
    /// </summary>
    [DataField]
    public float Temperature = 2000f;

    /// <summary>
    /// Сколько тепла прилетает каждому в области за тик, включая самого носителя.
    /// </summary>
    [DataField]
    public float HeatPerTick = 8f;
}

/// <summary>
/// _Warlock — активная «Личина Брата».
/// Помнит, что именно было снято, чтобы вернуть это владельцу, когда личина спадёт.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockFalseBrotherComponent : Component
{
    [DataField]
    public TimeSpan EndAt;

    /// <summary>
    /// Слот -> спавненная вещь Братства. Её надо удалить при снятии личины.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, EntityUid> Disguise = new();

    /// <summary>
    /// Слоты, из которых было что-то снято. Содержимое лежит в контейнере <see cref="StashId"/>.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, EntityUid> Stashed = new();

    [DataField]
    public string StashId = "warlock_disguise_stash";
}

/// <summary>
/// _Warlock — след «Литании Укрепления» на самом техномаге.
/// Снять нельзя: это и есть цена ритуала.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockHollowedComponent : Component
{
    /// <summary>
    /// Накопленный множитель порога усталости. Каждый новый ритуал делает его меньше.
    /// </summary>
    [DataField]
    public float StaminaMultiplier = 1f;

    /// <summary>
    /// Сколько раз техномаг проводил ритуал.
    /// </summary>
    [DataField]
    public int Rites;
}

/// <summary>
/// _Warlock — метка укреплённого механизма, чтобы ритуал не проводили на одном и том же дважды.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockBulwarkedComponent : Component
{
    [DataField]
    public ProtoId<DamageModifierSetPrototype>? PreviousModifiers;
}

/// <summary>
/// _Warlock — что техномаг сейчас держит телекинезом.
///
/// Висит на самом техномаге, а не на жертве: держащий может быть только один,
/// и это же даёт бесплатную проверку «уже держу — значит бросаю».
/// </summary>
[RegisterComponent]
public sealed partial class WarlockTelekineticGripComponent : Component
{
    /// <summary>
    /// Кого держим. Пусто — компонент тут же снимается.
    /// </summary>
    [DataField]
    public EntityUid? Held;

    /// <summary>
    /// Когда захват сорвётся сам.
    /// </summary>
    [DataField]
    public TimeSpan Expires;

    /// <summary>
    /// Следующее списание энергии.
    /// </summary>
    [DataField]
    public TimeSpan NextTick;

    /// <summary>
    /// Сколько энергии уходит за секунду удержания.
    /// </summary>
    [DataField]
    public float UpkeepPerSecond = 4f;

    /// <summary>
    /// С какой силой жертву подтягивает.
    /// </summary>
    [DataField]
    public float Speed = 6f;

    /// <summary>
    /// Дальше этого захват рвётся. Утащить пленника через отсек нельзя.
    /// </summary>
    [DataField]
    public float MaxDistance = 8f;
}
