using Content.Shared.Roles;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Warlock.Objectives;

/// <summary>
/// _Warlock
/// Цель фракции на раунд. Раскатывается один раз при старте раунда, читается на терминале целей.
///
/// Ограничений на пересечение нет намеренно: революция может выпасть всем трём гильдиям сразу,
/// и это нормальный сюжет, а не поломка. Большинство целей ролевые и ничем не отслеживаются —
/// механика отмечает только те, где считать действительно есть что.
/// </summary>
[Prototype]
public sealed partial class WarlockFactionObjectivePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Кому эта цель может выпасть. Одна и та же цель может подходить нескольким фракциям.
    /// </summary>
    [DataField(required: true)]
    public HashSet<WarlockFaction> Factions = new();

    /// <summary>
    /// Короткое название цели.
    /// </summary>
    [DataField(required: true)]
    public LocId Title = string.Empty;

    /// <summary>
    /// Полный текст задания, который читают на терминале.
    /// </summary>
    [DataField(required: true)]
    public LocId Description = string.Empty;

    /// <summary>
    /// Вес при случайном выборе. Чем больше, тем чаще выпадает.
    /// </summary>
    [DataField]
    public float Weight = 1f;

    /// <summary>
    /// Как отслеживается выполнение.
    /// </summary>
    [DataField]
    public WarlockObjectiveTracking Tracking = WarlockObjectiveTracking.None;

    /// <summary>
    /// Для <see cref="WarlockObjectiveTracking.Deliver"/>: какие предметы засчитываются.
    /// </summary>
    [DataField]
    public ProtoId<TagPrototype>? DeliverTag;

    /// <summary>
    /// Для <see cref="WarlockObjectiveTracking.Deliver"/>: сколько штук нужно сдать.
    /// </summary>
    [DataField]
    public int DeliverCount = 1;

    /// <summary>
    /// Для <see cref="WarlockObjectiveTracking.Assassinate"/>: чья смерть закрывает цель.
    /// </summary>
    [DataField]
    public ProtoId<JobPrototype>? TargetJob;
}
