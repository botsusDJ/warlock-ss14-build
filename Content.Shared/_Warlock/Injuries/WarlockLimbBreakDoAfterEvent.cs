using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Injuries;

/// <summary>
/// _Warlock — конечность доломали.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class WarlockLimbBreakDoAfterEvent : DoAfterEvent
{
    /// <summary>
    /// Какую именно конечность ломают. Выбирается в подменю до начала.
    /// </summary>
    [DataField]
    public WarlockBodyPart Part;

    private WarlockLimbBreakDoAfterEvent()
    {
    }

    public WarlockLimbBreakDoAfterEvent(WarlockBodyPart part)
    {
        Part = part;
    }

    public override DoAfterEvent Clone() => this;
}
