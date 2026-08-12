using Robust.Shared.GameStates;

namespace Content.Shared._Warlock.Injuries;

/// <summary>
/// _Warlock
/// Клеймо. Ставится на живого человека и не сходит никогда — в этом весь смысл.
/// Королевство Унатхи клеймит рабов, Братство Стали — своих.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WarlockBrandingIronComponent : Component
{
    /// <summary>
    /// Что именно выжигается. Строка локализации: клеймо всегда именное.
    /// </summary>
    [DataField(required: true)]
    public LocId Brand = string.Empty;

    /// <summary>
    /// Сколько секунд занимает клеймение. Долго намеренно: это не должно быть тычком в толпе.
    /// </summary>
    [DataField]
    public float Delay = 6f;

    /// <summary>
    /// Сколько жара получает заклеймённый.
    /// </summary>
    [DataField]
    public float Burn = 12f;
}
