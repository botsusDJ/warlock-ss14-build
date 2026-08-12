using Content.Shared._Warlock.Injuries;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Warlock.Artefacts.Components;

/// <summary>
/// _Warlock — «Гвоздь Механтехиона».
/// Ест другие артефакты. Механтехион не терпит чужих богов в железе,
/// но и даром ничего не делает: каждый съеденный артефакт ломает кость гвоздарю.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockArtefactEaterComponent : Component
{
    /// <summary>
    /// Какие предметы гвоздь считает артефактами.
    /// </summary>
    [DataField]
    public ProtoId<TagPrototype> Tag = "WarlockArtefact";

    /// <summary>
    /// Сколько секунд занимает расклёпывание.
    /// </summary>
    [DataField]
    public float Delay = 5f;

    /// <summary>
    /// Сколько артефактов гвоздь способен съесть, прежде чем рассыплется сам.
    /// </summary>
    [DataField]
    public int Uses = 3;
}

/// <summary>
/// _Warlock — «Неотступный Шестерён».
/// Тронул — и он твой навсегда. Летит следом, не даётся в руки надолго
/// и не отстаёт ни при каком расстоянии.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockRelentlessComponent : Component
{
    /// <summary>
    /// За кем летит. Задаётся первым прикосновением и больше не меняется.
    /// </summary>
    [DataField]
    public EntityUid? Bound;

    /// <summary>
    /// Ближе этого расстояния шестерён не дёргается.
    /// </summary>
    [DataField]
    public float Slack = 1.5f;

    /// <summary>
    /// С какой силой он бросает себя вдогонку.
    /// </summary>
    [DataField]
    public float Speed = 7f;

    /// <summary>
    /// Как часто он вспоминает о владельце.
    /// </summary>
    [DataField]
    public float TickInterval = 1.2f;

    [DataField]
    public TimeSpan NextTick;
}

/// <summary>
/// _Warlock — «Ремонтный Червь Механтехиона».
/// Пока при носителе, чинит его травмы. Но Механтехиону нужно, чтобы было что чинить:
/// если носитель слишком долго цел, червь ломает ему что-нибудь сам.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockRepairWormComponent : Component
{
    [DataField]
    public TimeSpan NextTick;

    [DataField]
    public float TickInterval = 45f;

    /// <summary>
    /// Сколько тиков подряд носитель может быть без травм, прежде чем червь возьмётся за него.
    /// </summary>
    [DataField]
    public int IdleTicksBeforeHarm = 2;

    /// <summary>
    /// Счётчик простоя.
    /// </summary>
    [DataField]
    public int IdleTicks;
}

/// <summary>
/// _Warlock — «Ошейник Покорности».
/// Надевается и не снимается. Всё, что было на носителе, заменяется робой раба,
/// а на теле остаётся клеймо. Королевство Унатхи считает это законной процедурой.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockSubjugationCollarComponent : Component
{
    /// <summary>
    /// Во что переодевает.
    /// </summary>
    [DataField]
    public EntProtoId SlaveUniform = "WarlockUniformBrotherhoodSlave";

    /// <summary>
    /// Какое клеймо ставит.
    /// </summary>
    [DataField]
    public LocId Brand = "warlock-brand-collar";

    /// <summary>
    /// Слоты, которые ошейник опустошает. Ошейник и так на шее, её не трогаем.
    /// </summary>
    [DataField]
    public List<string> StrippedSlots = new() { "outerClothing", "head", "mask", "gloves", "shoes", "belt", "back" };

    /// <summary>
    /// Сработал ли уже. Второй раз ошейник не нужен.
    /// </summary>
    [DataField]
    public bool Used;
}

// «Клык Атрака» переехал в Content.Shared/_Warlock/Artefacts/Components:
// его прибавку к урону считает общая WarlockAttackStrengthSystem, а из Shared
// серверные компоненты не видны.

/// <summary>
/// _Warlock — «Семя Рузута».
/// Заживляет всё, что способно зажить, — но Рузут ещё и бог обмана.
/// Вместе со здоровьем он всегда отращивает что-нибудь лишнее.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockRuzutSeedComponent : Component
{
    /// <summary>
    /// Сколько урона снимает семя.
    /// </summary>
    [DataField]
    public float Heal = 60f;

    /// <summary>
    /// Что может остаться на память. Одно из этого достаётся всегда.
    /// </summary>
    [DataField]
    public List<WarlockInjuryType> Souvenirs = new()
    {
        WarlockInjuryType.Scar,
        WarlockInjuryType.MissingTooth,
    };
}
