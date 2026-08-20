using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._Warlock.Artefacts.Components;

// _Warlock
// Пси-эссенция — жёлтая жижа, натекающая из-под мёртвого скарабея.
//
// Это ересь в самом прямом смысле: Союз молится артефактам, а эссенция предлагает
// обойтись без молитвы. Первая доза лечит и разгоняет, вторая тоже, третья тоже —
// и именно поэтому её пьют дальше. Расплата не приходит постепенно, она копится молча
// и вываливается вся сразу.
//
// Сама эссенция — реагент WarlockPsiEssence, а не предмет. Всё, что игра умеет
// делать с жидкостями, работает с ней бесплатно: лужа растекается и высыхает,
// шприц набирает, склянка хранит, бармен подмешивает.

/// <summary>
/// _Warlock — из этого тела натечёт эссенция, когда оно умрёт.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockEssenceSourceComponent : Component
{
    /// <summary>
    /// Какой реагент натечёт.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "WarlockPsiEssence";

    /// <summary>
    /// Сколько единиц. Одного скарабея хватает примерно на одну дозу —
    /// за второй придётся идти вниз.
    /// </summary>
    [DataField]
    public float Units = 15f;

    /// <summary>
    /// Уже натекло. Второй раз с того же трупа не собрать.
    /// </summary>
    [DataField]
    public bool Spent;
}

/// <summary>
/// _Warlock — эссенция сейчас работает.
///
/// Пока висит, носитель быстрее, выносливее и почти не чувствует боли.
/// Именно этот компонент и продают сам себе те, кто пьёт вторую дозу.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockEssenceHighComponent : Component
{
    [DataField]
    public TimeSpan EndAt;

    [DataField]
    public float SpeedModifier = 1.25f;

    [DataField]
    public float StaminaModifier = 1.4f;

    [DataField]
    public float DamageModifier = 0.75f;
}

/// <summary>
/// _Warlock — сколько эссенции в этом теле накопилось. Не убывает никогда.
///
/// Пороги подобраны так, чтобы первые две дозы выглядели чистой выгодой: расплата
/// начинается на третьей, когда бросить уже поздно, потому что откатить нельзя.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockEssenceCorruptionComponent : Component
{
    /// <summary>
    /// Сколько доз выпито за всю жизнь. Не убывает никогда.
    /// </summary>
    [DataField]
    public int Doses;

    /// <summary>
    /// Сколько единиц эссенции прошло через кровь. Реагент метаболизируется
    /// по капле, а пороги считаются дозами, поэтому единицы копятся здесь
    /// и превращаются в дозу, когда наберётся <see cref="UnitsPerDose"/>.
    /// </summary>
    [DataField]
    public float Units;

    /// <summary>
    /// Сколько единиц составляет одну дозу. Ровно столько натекает с одного скарабея.
    /// </summary>
    [DataField]
    public float UnitsPerDose = 15f;

    [DataField]
    public TimeSpan NextTick;

    [DataField]
    public float TickInterval = 10f;

    /// <summary>
    /// С какой дозы начинает прорываться жучий язык.
    /// </summary>
    [DataField]
    public int BabbleThreshold = 3;

    /// <summary>
    /// С какой дозы дар глохнет насовсем.
    /// </summary>
    [DataField]
    public int SilenceThreshold = 4;

    /// <summary>
    /// С какой дозы тело начинает травиться.
    /// </summary>
    [DataField]
    public int PoisonThreshold = 4;

    /// <summary>
    /// Сколько яда за тик приходится на каждую дозу сверх порога.
    /// </summary>
    [DataField]
    public float PoisonPerDose = 2.5f;

    /// <summary>
    /// С какой дозы это уже не отравление, а конец.
    /// </summary>
    [DataField]
    public int LethalThreshold = 7;

    /// <summary>
    /// Сколько яда за тик после последнего порога.
    /// </summary>
    [DataField]
    public float LethalPoison = 25f;
}
