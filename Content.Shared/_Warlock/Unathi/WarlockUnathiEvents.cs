using Content.Shared.Actions;
using Content.Shared.Dataset;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Warlock.Unathi;

/// <summary>
/// _Warlock — «Боевой Клич».
/// Унатх орёт в голос что-то из походного набора. Слышно всем рядом.
/// </summary>
public sealed partial class WarlockWarCryEvent : InstantActionEvent
{
    /// <summary>
    /// Откуда берутся выкрики.
    /// </summary>
    [DataField]
    public ProtoId<LocalizedDatasetPrototype> Cries = "WarlockUnathiWarCries";

    /// <summary>
    /// Радиус, в котором клич сбивает дыхание врагам.
    /// </summary>
    [DataField]
    public float Radius = 5f;

    /// <summary>
    /// Сколько выносливости теряют те, кто рядом. Своих клич не трогает.
    /// </summary>
    [DataField]
    public float StaminaDamage = 15f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Священная Ярость».
/// Поход объявлен, и унатх на время перестаёт чувствовать усталость и половину боли.
/// Расплата приходит сразу после.
/// </summary>
public sealed partial class WarlockBerserkEvent : InstantActionEvent
{
    /// <summary>
    /// Сколько секунд держится ярость.
    /// </summary>
    [DataField]
    public float Duration = 25f;

    [DataField]
    public SoundSpecifier? Sound;
}
