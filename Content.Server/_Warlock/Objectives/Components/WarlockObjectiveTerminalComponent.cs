using Content.Shared._Warlock.Objectives;

namespace Content.Server._Warlock.Objectives.Components;

/// <summary>
/// _Warlock
/// Терминал целей фракции. Показывает, что фракции поручено на этот раунд,
/// и принимает предметы, если цель это считает.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockObjectiveTerminalComponent : Component
{
    /// <summary>
    /// Чей это терминал. Чужую цель на нём не прочитать.
    /// </summary>
    [DataField(required: true)]
    public WarlockFaction Faction;
}
