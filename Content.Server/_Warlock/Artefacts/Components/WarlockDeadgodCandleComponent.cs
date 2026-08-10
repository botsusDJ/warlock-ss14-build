namespace Content.Server._Warlock.Artefacts.Components;

/// <summary>
/// _Warlock — «Свеча Мёртвого Бога».
/// Пока свеча горит, она тянет псионическую энергию из того, кто её держит, и тратит её
/// на удержание в живых всех, кто рядом умирает. Не-псайкера свеча жжёт вместо этого.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockDeadgodCandleComponent : Component
{
    /// <summary>
    /// Радиус, в котором свеча удерживает умирающих.
    /// </summary>
    [DataField]
    public float Radius = 4f;

    /// <summary>
    /// Сколько псионической энергии свеча съедает за тик.
    /// </summary>
    [DataField]
    public float EnergyPerTick = 2f;

    /// <summary>
    /// Сколько урона свеча наносит носителю без дара за тик.
    /// </summary>
    [DataField]
    public float BurnPerTick = 1.5f;

    /// <summary>
    /// Сколько урона лечится у каждого умирающего в радиусе за тик.
    /// </summary>
    [DataField]
    public float HealPerTick = 4f;

    /// <summary>
    /// Момент следующего тика.
    /// </summary>
    [DataField]
    public TimeSpan NextTick;

    /// <summary>
    /// Длительность тика в секундах.
    /// </summary>
    [DataField]
    public float TickInterval = 1f;
}
