using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Injuries;

/// <summary>
/// _Warlock — конечность дотянули.
///
/// Отдельное событие от слома намеренно: отрыв идёт втрое быстрее, требует рамы
/// и по итогу решает проверку силы. Одно событие с флагом «рвём или ломаем» пришлось
/// бы разбирать в двух местах, а так каждая ветка живёт своей жизнью.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class WarlockLimbTearDoAfterEvent : DoAfterEvent
{
    /// <summary>
    /// Какую именно конечность тянут. Выбирается в подменю до начала.
    /// </summary>
    [DataField]
    public WarlockBodyPart Part;

    private WarlockLimbTearDoAfterEvent()
    {
    }

    public WarlockLimbTearDoAfterEvent(WarlockBodyPart part)
    {
        Part = part;
    }

    public override DoAfterEvent Clone() => this;
}
