using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Injuries;

/// <summary>
/// _Warlock — «Гвоздь Механтехиона» доклепал чужой артефакт.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class WarlockArtefactEaterDoAfterEvent : SimpleDoAfterEvent;
