using Content.Shared.Actions;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Warlock.Psionics.Events;

/// <summary>
/// _Warlock — «Эхо-Копия».
/// Техномаг заставляет вещи вокруг вспомнить, какими они были, и вспоминание материализуется.
/// Пол, стены и живое заклинание не трогает: у них слишком длинная память.
/// </summary>
public sealed partial class WarlockMirrorEchoEvent : InstantActionEvent
{
    [DataField]
    public float Radius = 3f;

    /// <summary>
    /// Потолок числа копий за каст. Без него на складе получится лагомёт.
    /// </summary>
    [DataField]
    public int MaxCopies = 8;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Проклятая Хватка».
/// На полминуты чужая материя перестаёт терпеть прикосновение техномага: всё, что он берёт в руки,
/// разрывает. Взамен высвободившееся идёт на затягивание его собственных ран.
/// </summary>
public sealed partial class WarlockCursedGraspEvent : InstantActionEvent
{
    [DataField]
    public float Duration = 30f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Телекинетическая Хватка».
/// Одно заклинание на два движения: первое применение поднимает цель и держит её при себе,
/// второе швыряет туда, куда указали.
///
/// Держать дорого и недолго. Это приём в упор, а не способ утащить пленника через отсек:
/// захват срывается по времени, по расстоянию и по пустому резерву.
/// </summary>
public sealed partial class WarlockTelekineticGraspEvent : WorldTargetActionEvent
{
    /// <summary>
    /// Сколько секунд захват держится сам по себе.
    /// </summary>
    [DataField]
    public float HoldSeconds = 8f;

    /// <summary>
    /// Сколько энергии уходит каждую секунду удержания.
    /// </summary>
    [DataField]
    public float UpkeepPerSecond = 4f;

    /// <summary>
    /// С какой силой жертву подтягивает к техномагу.
    /// </summary>
    [DataField]
    public float PullSpeed = 6f;

    /// <summary>
    /// С какой силой её потом швыряют.
    /// </summary>
    [DataField]
    public float ThrowStrength = 14f;

    /// <summary>
    /// Сколько тупого урона добавляет само сдавливание при броске.
    /// Урон от встречи со стеной ваниль считает сама.
    /// </summary>
    [DataField]
    public float CrushDamage = 8f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Литания Укрепления».
/// Механизм переписывается на языке артефактов и перестаёт ломаться. Плата берётся не с механизма:
/// техномаг навсегда отдаёт часть собственной прочности и выносливости. Отменить это нельзя.
/// </summary>
public sealed partial class WarlockRiteOfBulwarkEvent : EntityTargetActionEvent
{
    /// <summary>
    /// Набор резистов, который получает укреплённый механизм.
    /// </summary>
    [DataField]
    public ProtoId<DamageModifierSetPrototype> Modifiers = "WarlockBulwark";

    /// <summary>
    /// Сколько здоровья техномаг теряет навсегда (сдвиг порогов крита и смерти).
    /// </summary>
    [DataField]
    public float HealthCost = 25f;

    /// <summary>
    /// Во сколько раз падает порог оглушения от усталости. Тоже навсегда.
    /// </summary>
    [DataField]
    public float StaminaPenalty = 0.75f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Чутьё Реликвий».
/// Дар нащупывает артефакты далеко за пределами видимости и раз в несколько секунд
/// подсказывает сторону и расстояние до ближайшего.
/// </summary>
public sealed partial class WarlockRelicScentEvent : InstantActionEvent
{
    [DataField]
    public float Duration = 60f;

    [DataField]
    public float Radius = 60f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Иссушающее Касание».
/// Техномаг выпивает чужую выносливость досуха и заливает её в себя.
/// </summary>
public sealed partial class WarlockWitheringTouchEvent : EntityTargetActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Погребальный Костёр».
/// Вокруг техномага держится область немыслимого жара. Источник — он сам, поэтому область
/// таскается за ним и жарит его наравне со всеми. Уйти от неё нельзя, можно только пережить.
/// </summary>
public sealed partial class WarlockPyreAuraEvent : InstantActionEvent
{
    [DataField]
    public float Duration = 20f;

    [DataField]
    public float Radius = 3f;

    /// <summary>
    /// До какой температуры разогреваются тайлы вокруг, в кельвинах.
    /// </summary>
    [DataField]
    public float Temperature = 2000f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Личина Брата».
/// Дар перекраивает одежду техномага в форму Братства Стали. Своё снаряжение никуда не девается —
/// оно сложено «между», и вернётся, когда личина спадёт.
/// </summary>
public sealed partial class WarlockFalseBrotherEvent : InstantActionEvent
{
    [DataField]
    public float Duration = 120f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Жатва Дара».
/// Из мёртвого техномага можно вытянуть остаток дара. Мёртвые этого не одобряют,
/// и иногда жнец уходит вместе с урожаем.
/// </summary>
public sealed partial class WarlockGiftHarvestEvent : EntityTargetActionEvent
{
    /// <summary>
    /// Вероятность того, что жнеца разорвёт вместе с добычей.
    /// </summary>
    [DataField]
    public float DisintegrationChance = 0.25f;

    [DataField]
    public SoundSpecifier? Sound;
}
