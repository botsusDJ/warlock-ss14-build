using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Injuries;

/// <summary>
/// _Warlock — клеймение доведено до конца.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class WarlockBrandingDoAfterEvent : SimpleDoAfterEvent;
