using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Warlock.Psionics.Events;

// _Warlock
// Заклинания из гримуаров гильдий. В отличие от первых двух кругов, эти не выдаются расой:
// каждое покупается в книге за очки и достаётся не всем.
//
// Разбиты по назначению, а не по силе. Ролевое может решить раунд лучше атакующего,
// если им правильно воспользоваться, — и наоборот.

#region Ролевые

/// <summary>
/// _Warlock — «Чтение Праха».
/// Техномаг кладёт руку на тело или вещь и слышит последнее, что с ней случилось.
/// Ничего не лечит и никого не бьёт: это способ узнать то, о чём никто не расскажет.
/// </summary>
public sealed partial class WarlockDustReadingEvent : EntityTargetActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Голос Гильдии».
/// Одна фраза, которую услышат все свои, где бы они ни были. Радио у Союза нет,
/// и это единственная связь через полстанции — по одной фразе за раз и не бесплатно.
/// </summary>
public sealed partial class WarlockGuildVoiceEvent : InstantActionEvent
{
    /// <summary>
    /// Максимальная длина фразы. Это не переговорка, а крик через весь мир.
    /// </summary>
    [DataField]
    public int MaxLength = 120;

    [DataField]
    public SoundSpecifier? Sound;
}

#endregion

#region Телекинетические

/// <summary>
/// _Warlock — «Отпирающий Жест».
/// Предмет в указанной точке сам ложится в свободную руку. Прикрученное, живое
/// и лежащее в чужих карманах не поддаётся.
/// </summary>
public sealed partial class WarlockBeckonEvent : WorldTargetActionEvent
{
    /// <summary>
    /// В каком радиусе от точки клика искать предмет.
    /// </summary>
    [DataField]
    public float Radius = 0.4f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Волна Отторжения».
/// Всё незакреплённое вокруг разлетается прочь. Толпу это не убьёт,
/// но разомкнёт — и часто этого достаточно.
/// </summary>
public sealed partial class WarlockRepulseWaveEvent : InstantActionEvent
{
    [DataField]
    public float Radius = 4f;

    [DataField]
    public float Strength = 12f;

    [DataField]
    public SoundSpecifier? Sound;
}

#endregion

#region Стратегические

/// <summary>
/// _Warlock — «Метка Гильдии».
/// Ставит на пол знак, который держится половину смены, и сообщает своим, где он.
/// Способ назначить точку сбора там, где нет ни радио, ни карты.
/// </summary>
public sealed partial class WarlockGuildMarkEvent : WorldTargetActionEvent
{
    [DataField]
    public EntProtoId Mark = "WarlockGuildMark";

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Круг Тишины».
/// Пятно, внутри которого не работает ни одна рация. Ставится и уходит само.
/// </summary>
public sealed partial class WarlockSilenceCircleEvent : WorldTargetActionEvent
{
    [DataField]
    public EntProtoId Circle = "WarlockSilenceCircle";

    [DataField]
    public SoundSpecifier? Sound;
}

#endregion

#region Тактические

/// <summary>
/// _Warlock — «Смена Мест».
/// Техномаг и цель мгновенно меняются местами. Ни урона, ни оглушения —
/// только то, что теперь вы стоите там, где стоял он.
/// </summary>
public sealed partial class WarlockSwapPlacesEvent : EntityTargetActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Стеклянная Кожа».
/// Несколько секунд по технмагу нельзя попасть — и всё это время он не может сойти с места.
/// Пережить залп можно, воспользоваться передышкой нельзя.
/// </summary>
public sealed partial class WarlockGlassSkinEvent : InstantActionEvent
{
    [DataField]
    public float Duration = 6f;

    [DataField]
    public SoundSpecifier? Sound;
}

#endregion

#region Атакующие

/// <summary>
/// _Warlock — «Копьё Пустоты».
/// Узкий прокол на расстоянии. Броню почти не замечает, зато и бьёт по одному.
/// </summary>
public sealed partial class WarlockVoidSpearEvent : EntityTargetActionEvent
{
    [DataField]
    public float Damage = 26f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// _Warlock — «Схлопывание».
/// В указанной точке пространство собирается внутрь: всё вокруг стаскивает к центру
/// и мнёт. Бьёт слабее прямого удара, зато по всем сразу и сбивает строй.
/// </summary>
public sealed partial class WarlockImplosionEvent : WorldTargetActionEvent
{
    [DataField]
    public float Radius = 3.5f;

    [DataField]
    public float PullStrength = 10f;

    [DataField]
    public float Damage = 14f;

    [DataField]
    public SoundSpecifier? Sound;
}

#endregion
