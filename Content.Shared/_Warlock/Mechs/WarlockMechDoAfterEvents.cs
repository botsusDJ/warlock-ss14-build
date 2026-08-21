using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Mechs;

// _Warlock
// События длительных действий с мехами.
//
// Лежат в Shared, а не рядом с системами: DoAfter гоняется по сети между клиентом
// и сервером, и событие, объявленное только на сервере, клиент не разберёт.

/// <summary>
/// _Warlock — влезание на место стрелка.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class WarlockMechGunnerDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// _Warlock — заливка смазки в раму.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class WarlockMechGreaseDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// _Warlock — протирание узла ветошью.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class WarlockMechWipeDoAfterEvent : SimpleDoAfterEvent;
