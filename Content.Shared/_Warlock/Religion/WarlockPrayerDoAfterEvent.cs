using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Religion;

/// <summary>
/// _Warlock — завершение молитвы у святилища.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class WarlockPrayerDoAfterEvent : SimpleDoAfterEvent;
