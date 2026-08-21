using Content.Shared.Actions;
using Robust.Shared.Audio;

namespace Content.Shared._Warlock.Psionics.Events;

// _Warlock
// Второе пополнение каталога гримуаров: по пять строк в каждый из четырёх разделов.
//
// Правило то же, что и у первого набора: ни одно заклинание не является улучшенной
// версией другого. Если новая строка делает то же самое, но сильнее, — она лишняя.
// Поэтому здесь нет «большого огненного шара» и «улучшенного лечения»: есть чтение
// чужого имени сквозь маску, снятие огня со всей комнаты, отъём дара у своего же
// подчинённого и опись чужих карманов.
//
// Числа живут в YAML, а не в коде: правка баланса не должна требовать пересборки.

#region Рядовые

/// <summary>
/// _Warlock — «Руки Правки».
/// Латает железо касанием. На живое не действует вовсе: это ремонт, а не медицина.
/// </summary>
public sealed partial class WarlockMendingHandsEvent : EntityTargetActionEvent
{
    /// <summary>Сколько урона снимается с механизма.</summary>
    [DataField]
    public float Repair = 40f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Твёрдая Хватка».
/// Руки перестают дрожать: всё, что требует времени, делается заметно быстрее.
/// Единственное заклинание, которое ускоряет работу, а не бой.
/// </summary>
public sealed partial class WarlockSteadyGripEvent : InstantActionEvent
{
    [DataField]
    public float Duration = 30f;

    /// <summary>Множитель длительности действий. Меньше единицы — быстрее.</summary>
    [DataField]
    public float Multiplier = 0.6f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Лёгкий Шаг».
/// Вес уходит из ног. Ненадолго и без всякой боевой пользы, кроме той,
/// что от драки можно уйти.
/// </summary>
public sealed partial class WarlockLightStepEvent : InstantActionEvent
{
    [DataField]
    public float Duration = 20f;

    [DataField]
    public float Speed = 1.25f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Второй Взгляд».
/// Читает, кто перед вами, минуя маску, капюшон и чужой скафандр.
/// Ничего не делает с целью и цель об этом не узнает.
/// </summary>
public sealed partial class WarlockSecondSightEvent : EntityTargetActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Холодный Очаг».
/// Сбивает огонь со всех вокруг разом, включая себя. Пожар в помещении
/// это не тушит, но людей из него вынимает.
/// </summary>
public sealed partial class WarlockColdHearthEvent : InstantActionEvent
{
    [DataField]
    public float Radius = 4f;

    /// <summary>Сколько единиц огня снимается с каждого.</summary>
    [DataField]
    public float Extinguish = 10f;

    /// <summary>Сколько ожогов заодно затягивается.</summary>
    [DataField]
    public float Soothe = 8f;

    [DataField]
    public SoundSpecifier? Sound;
}

#endregion

#region Боевые

/// <summary>
/// _Warlock — «Залп Осколков».
/// Всё, что было пылью и крошкой, разгоняется наружу. Бьёт всех вокруг,
/// кроме самого заклинателя, и не разбирает своих.
/// </summary>
public sealed partial class WarlockSplinterVolleyEvent : InstantActionEvent
{
    [DataField]
    public float Radius = 4.5f;

    [DataField]
    public float Damage = 14f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Железная Судорога».
/// Мышцы цели сводит разом. Урона почти нет, но стоять цель перестаёт.
/// </summary>
public sealed partial class WarlockIronCrampEvent : EntityTargetActionEvent
{
    [DataField]
    public float Stamina = 55f;

    [DataField]
    public float Knockdown = 4f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Долг Крови».
/// Переписывает собственные раны на чужое тело. Заклинатель встаёт,
/// цель ложится — ровно на ту же величину.
/// </summary>
public sealed partial class WarlockBloodDebtEvent : EntityTargetActionEvent
{
    /// <summary>Сколько урона переносится.</summary>
    [DataField]
    public float Amount = 30f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Статический Покров».
/// Воздух вокруг густеет. Бить по заклинателю становится тяжело, ходить ему — тоже.
/// </summary>
public sealed partial class WarlockStaticShroudEvent : InstantActionEvent
{
    [DataField]
    public float Duration = 15f;

    /// <summary>Множитель получаемого урона.</summary>
    [DataField]
    public float Resist = 0.55f;

    /// <summary>Множитель скорости. Это и есть цена.</summary>
    [DataField]
    public float Slow = 0.7f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Могильные Путы».
/// Держит цель на месте. Бить и стрелять она может, уйти — нет.
/// </summary>
public sealed partial class WarlockGravebindEvent : EntityTargetActionEvent
{
    [DataField]
    public float Duration = 6f;

    [DataField]
    public SoundSpecifier? Sound;
}

#endregion

#region Капелланские

/// <summary>
/// _Warlock — «Последний Обряд».
/// Вытаскивает того, кого уже списали. Заклинатель забирает половину
/// вытащенного себе, и отменить это нельзя.
/// </summary>
public sealed partial class WarlockLastRitesEvent : EntityTargetActionEvent
{
    /// <summary>Сколько урона снимается с умирающего.</summary>
    [DataField]
    public float Heal = 70f;

    /// <summary>Какая доля этого ложится на заклинателя.</summary>
    [DataField]
    public float Backlash = 0.5f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Обет Молчания».
/// Затыкает чужой дар. Ненадолго, зато на любом — в том числе на архимаге.
/// </summary>
public sealed partial class WarlockVowOfSilenceEvent : EntityTargetActionEvent
{
    [DataField]
    public float Duration = 45f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Разделённая Ноша».
/// Забирает чужую боль себе. Не лечит ни на единицу: боль просто меняет владельца.
/// </summary>
public sealed partial class WarlockBurdenShareEvent : EntityTargetActionEvent
{
    /// <summary>Какая доля чужой боли переходит к заклинателю.</summary>
    [DataField]
    public float Share = 0.6f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Освящение».
/// Накрывает своих по гильдии. Пока держится, по ним бьёт слабее.
/// </summary>
public sealed partial class WarlockConsecrateEvent : InstantActionEvent
{
    [DataField]
    public float Radius = 5f;

    [DataField]
    public float Duration = 25f;

    /// <summary>Множитель получаемого урона у освящённых.</summary>
    [DataField]
    public float Resist = 0.75f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Созыв Паствы».
/// Каждый свой по гильдии узнаёт, где сейчас заклинатель и как далеко.
/// Радио у Союза нет, и это единственный способ собрать людей в одну точку.
/// </summary>
public sealed partial class WarlockCallTheFlockEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

#endregion

#region Командирские

/// <summary>
/// _Warlock — «Ордер на Изъятие».
/// Всё, что цель держит в руках, оказывается на полу. Работает на ком угодно,
/// включая своих, и именно поэтому лежит в командирском разделе.
/// </summary>
public sealed partial class WarlockWritOfSeizureEvent : EntityTargetActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Цепь Приказа».
/// Своих по гильдии в радиусе разгоняет и держит на ногах дольше обычного.
/// </summary>
public sealed partial class WarlockChainOfCommandEvent : InstantActionEvent
{
    [DataField]
    public float Radius = 6f;

    [DataField]
    public float Duration = 30f;

    [DataField]
    public float Speed = 1.2f;

    /// <summary>Множитель порога усталости: свои дольше не падают.</summary>
    [DataField]
    public float Stamina = 1.35f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Отлучение».
/// Снимает дар с подчинённого насовсем — до тех пор, пока отлучивший не вернёт его
/// тем же жестом. Единственное наказание в Союзе, которое нельзя отсидеть.
/// </summary>
public sealed partial class WarlockExcommunicateEvent : EntityTargetActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Перепись».
/// Сколько кого осталось в живых по всем фракциям. Ни имён, ни мест — только счёт,
/// и этого хватает, чтобы понять, чем закончился штурм на другом конце карты.
/// </summary>
public sealed partial class WarlockCensusEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Право Досмотра».
/// Опись всего, что у цели при себе, вплоть до содержимого сумки. Цель не узнаёт.
/// </summary>
public sealed partial class WarlockRightOfSearchEvent : EntityTargetActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

#endregion
