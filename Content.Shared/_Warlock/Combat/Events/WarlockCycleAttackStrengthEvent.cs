using Content.Shared.Actions;

namespace Content.Shared._Warlock.Combat.Events;

/// <summary>
/// _Warlock — переключение силы удара по кругу: Слабый → Средний → Сильный → Слабый.
/// </summary>
public sealed partial class WarlockCycleAttackStrengthEvent : InstantActionEvent;
