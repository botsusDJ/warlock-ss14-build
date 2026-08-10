using Content.Shared.Actions;
using Robust.Shared.Audio;

namespace Content.Shared._Warlock.Artefacts.Events;

/// <summary>
/// _Warlock — действие «Перчатки Тысячи Рук».
/// Позволяет выдернуть предмет прямо в руку с расстояния.
/// </summary>
public sealed partial class WarlockThousandHandsEvent : EntityTargetActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}
