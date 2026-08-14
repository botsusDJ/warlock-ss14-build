using Robust.Shared.Prototypes;

namespace Content.Server._Warlock.Artefacts.Components;

// _Warlock
// Пси-эссенция — жёлтая жижа, натекающая из-под мёртвого скарабея.
//
// Это ересь в самом прямом смысле: Союз молится артефактам, а эссенция предлагает
// обойтись без молитвы. Первая доза лечит и разгоняет, вторая тоже, третья тоже —
// и именно поэтому её пьют дальше. Расплата не приходит постепенно, она копится молча
// и вываливается вся сразу.

/// <summary>
/// _Warlock — из этого тела натечёт эссенция, когда оно умрёт.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockEssenceSourceComponent : Component
{
    /// <summary>
    /// Что именно натечёт.
    /// </summary>
    [DataField]
    public EntProtoId Essence = "WarlockPsiEssence";

    /// <summary>
    /// Сколько порций. Одного скарабея хватает на одну — за второй придётся идти вниз.
    /// </summary>
    [DataField]
    public int Amount = 1;

    /// <summary>
    /// Уже натекло. Второй раз с того же трупа не собрать.
    /// </summary>
    [DataField]
    public bool Spent;
}

/// <summary>
/// _Warlock — порция пси-эссенции.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockPsiEssenceComponent : Component
{
    /// <summary>
    /// Сколько урона закрывает доза.
    /// </summary>
    [DataField]
    public float Heal = 45f;

    /// <summary>
    /// Сколько резерва возвращает.
    /// </summary>
    [DataField]
    public float Energy = 60f;

    /// <summary>
    /// Сколько секунд держится разгон.
    /// </summary>
    [DataField]
    public float HighDuration = 45f;
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
    /// Сколько доз выпито за всю жизнь.
    /// </summary>
    [DataField]
    public int Doses;

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
