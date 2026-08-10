using Robust.Shared.GameStates;

namespace Content.Shared._Warlock.Psionics.Components;

/// <summary>
/// _Warlock
/// Псионика носителя заглушена: касты запрещены, энергия не восстанавливается.
/// Вешается артефактом "Оковы Логики" и всем, что должно отрезать техномага от дара.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WarlockPsiSuppressedComponent : Component;
