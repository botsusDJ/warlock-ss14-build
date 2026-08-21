using Content.Shared.Actions;
using Robust.Shared.Audio;

namespace Content.Shared._Warlock.Psionics.Events;

// _Warlock
// Действия реликвий глав гильдий. Реликвия работает в руках у кого угодно — она тянет
// силу из себя, а не из носителя, — но цену всё равно берёт с того, кто ей воспользовался.

/// <summary>
/// _Warlock — «Око Фактоса»: прочесть находку насквозь.
///
/// Показывает весь набор эффектов камня и вырезает из него вредные. Изменение
/// постоянное и общее: прирученный камень остаётся прирученным для всех.
/// </summary>
public sealed partial class WarlockSeersEyeEvent : EntityTargetActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Сердце Механтехиона»: разряд питания.
///
/// Заряжает досуха всё, что держит заряд, в радиусе — и бьёт током того, кто нажал,
/// тем сильнее, чем больше он зарядил.
/// </summary>
public sealed partial class WarlockMachineHeartEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}
