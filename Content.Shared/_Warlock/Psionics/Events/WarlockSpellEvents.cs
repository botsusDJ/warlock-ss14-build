using Content.Shared.Actions;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Warlock.Psionics.Events;

/// <summary>
/// _Warlock — «Литания Расщепления».
/// Техномаг разбирает машину или конструкцию на исходные принципы и выпивает высвободившуюся структуру,
/// возвращая себе часть псионической энергии. Гильдия Технос считает это святотатством, гильдия Фактос — обедом.
/// </summary>
public sealed partial class WarlockLitanyOfUnmakingEvent : EntityTargetActionEvent
{
    /// <summary>
    /// Сколько псионической энергии возвращает удачное расщепление.
    /// </summary>
    [DataField]
    public float EnergyReturn = 20f;

    /// <summary>
    /// Что осыпается на пол после расщепления.
    /// </summary>
    [DataField]
    public List<EntProtoId> Residue = new();

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Хор Стали».
/// Всё незакреплённое железо вокруг срывается с места и стягивается к техномагу, а затем,
/// когда хор "допевает", разлетается наружу шрапнелью.
/// </summary>
public sealed partial class WarlockChoirOfSteelEvent : InstantActionEvent
{
    /// <summary>
    /// Радиус, с которого стягиваются предметы.
    /// </summary>
    [DataField]
    public float GatherRadius = 7f;

    /// <summary>
    /// Радиус, из которого предметы выбрасывает наружу на второй фазе.
    /// </summary>
    [DataField]
    public float BurstRadius = 2.5f;

    /// <summary>
    /// Сколько секунд длится первая фаза (стягивание).
    /// </summary>
    [DataField]
    public float Duration = 4f;

    /// <summary>
    /// Максимум предметов, которые заклинание трогает за фазу. Защита от лагов на складе.
    /// </summary>
    [DataField]
    public int MaxItems = 20;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Отзвук Мёртвого Бога».
/// В точке каста разворачивается печать, которая запоминает, где в этот миг находилось всё живое рядом.
/// Когда печать схлопывается, она возвращает их обратно — в буквальном смысле откатывает положение.
/// </summary>
public sealed partial class WarlockDeadgodEchoEvent : WorldTargetActionEvent
{
    /// <summary>
    /// Прототип печати, которая ставится в точке каста.
    /// </summary>
    [DataField]
    public EntProtoId Anchor = "WarlockDeadgodAnchor";

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Заёмное Дыхание».
/// Техномаг латает чужое тело собственным даром: цель лечится, а сам техномаг платит за это
/// выносливостью и опустевшим резервом. Лечить себя таким образом бессмысленно.
/// </summary>
public sealed partial class WarlockBorrowedBreathEvent : EntityTargetActionEvent
{
    /// <summary>
    /// Что и насколько лечится у цели. Значения должны быть отрицательными.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier Healing = new();

    /// <summary>
    /// Урон по выносливости, который получает сам техномаг.
    /// </summary>
    [DataField]
    public float CasterStaminaCost = 35f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Разделённая Участь».
/// Техномаг связывает свою судьбу с чужой. Пока связь держится, любой урон делится между обоими.
/// Это одновременно щит для союзника и смертный приговор при неудачном выборе цели.
/// </summary>
public sealed partial class WarlockSharedFateEvent : EntityTargetActionEvent
{
    /// <summary>
    /// Сколько секунд держится связь.
    /// </summary>
    [DataField]
    public float Duration = 25f;

    /// <summary>
    /// Какая доля урона перекидывается на партнёра по связи.
    /// </summary>
    [DataField]
    public float Coefficient = 0.5f;

    [DataField]
    public SoundSpecifier? Sound;
}
