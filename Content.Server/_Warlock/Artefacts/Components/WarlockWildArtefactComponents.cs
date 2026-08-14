using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Warlock.Artefacts.Components;

// _Warlock
// Дикие реликвии фанатиков Касса и биотехнологии к-хритов.
//
// Разница между двумя наборами не в силе, а в намерении. Фанатики делали вещи, которые
// что-то отнимают: голос, зрение, речь, место в мире. Улей делал инструменты для улья —
// они полезны, но всегда тянут владельца в сторону насекомого.

#region Фанатики Касса

/// <summary>
/// _Warlock — «Гортань Касса».
/// Ломает речь навсегда: слова остаются, язык становится чужим.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockKassThroatComponent : Component
{
}

/// <summary>
/// _Warlock — «Второе Сердце» уже внутри.
/// Держит владельца на ногах там, где он должен был упасть, — и всё это время
/// перекачивает его собственную кровь наружу.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockSecondHeartComponent : Component
{
    [DataField]
    public TimeSpan NextTick;

    [DataField]
    public float TickInterval = 15f;

    /// <summary>
    /// Сколько побоев второе сердце закрывает за тик.
    /// </summary>
    [DataField]
    public float Mend = 12f;

    /// <summary>
    /// И сколько крови берёт за это.
    /// </summary>
    [DataField]
    public float Bleed = 8f;
}

/// <summary>
/// _Warlock — «Гортань Касса» уже в горле.
/// Висит на теле, а не на предмете: предмет рассыпается сразу, а язык остаётся чужим навсегда.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockKassThroatSpeakerComponent : Component
{
}

/// <summary>
/// _Warlock — «Второе Сердце» бьётся внутри этого тела.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockSecondHeartHostComponent : Component
{
    [DataField]
    public TimeSpan NextTick;

    [DataField]
    public float TickInterval = 15f;

    [DataField]
    public float Mend = 12f;

    [DataField]
    public float Bleed = 8f;
}

/// <summary>
/// _Warlock — «Панцирный Нарост» прирос к этому телу. Снять нельзя.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockCarapaceHostComponent : Component
{
}

/// <summary>
/// _Warlock — рот заперт «Колоколом Немоты».
/// </summary>
[RegisterComponent]
public sealed partial class WarlockMutedComponent : Component
{
    [DataField]
    public TimeSpan EndAt;
}

/// <summary>
/// _Warlock — «Колокол Немоты».
/// Один удар, и все вокруг замолкают. Звонарь в том числе: колокол не различает,
/// кто держит верёвку.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockSilenceBellComponent : Component
{
    [DataField]
    public float Radius = 8f;

    [DataField]
    public float Duration = 60f;
}

/// <summary>
/// _Warlock — «Слепой Хор».
/// Вспышка, от которой спасает только закрытое лицо. Тем, кто в шлеме или маске,
/// достаётся вдвое меньше — фанатики строили это против непокрытых голов.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockBlindChoirComponent : Component
{
    [DataField]
    public float Radius = 6f;

    [DataField]
    public int EyeDamage = 8;
}

/// <summary>
/// _Warlock — «Печать Обмена».
/// Меняет владельца местами со случайным живым существом где угодно. Одноразово,
/// и куда именно вас забросит, не знает никто.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockSwapSealComponent : Component
{
}

#endregion

#region К-хриты

/// <summary>
/// _Warlock — «Золотой Рой».
/// Будит сторожа. Скарабей не станет вашим: улей давно мёртв, и приказы отдавать некому.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockGoldenSwarmComponent : Component
{
    [DataField]
    public EntProtoId Scarab = "WarlockMobKhritScorpion";

    /// <summary>
    /// Сколько сторожей ещё спит внутри.
    /// </summary>
    [DataField]
    public int Uses = 1;
}

/// <summary>
/// _Warlock — «Улей в Ладони».
/// Затягивает раны владельца, перекладывая их на ближайшее живое. Улей не лечит,
/// улей перераспределяет.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockHiveInHandComponent : Component
{
    [DataField]
    public float Amount = 25f;

    [DataField]
    public float Radius = 6f;
}

/// <summary>
/// _Warlock — «Янтарный Глаз».
/// Пересчитывает всё живое вокруг и показывает, откуда ждать. Улей всегда знал,
/// сколько его.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockAmberEyeComponent : Component
{
    [DataField]
    public float Radius = 20f;
}

/// <summary>
/// _Warlock — «Панцирный Нарост» уже прирос.
/// Панцирь к-хритов на человеческой коже: физический урон почти не проходит,
/// зато кислота и огонь достают до того, что под ним.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockCarapaceGraftComponent : Component
{
    /// <summary>
    /// Какой набор резистов был у тела до прививки. Возвращать его некому и незачем —
    /// хранится только для отладки.
    /// </summary>
    [DataField]
    public ProtoId<DamageModifierSetPrototype>? PreviousModifiers;
}

#endregion
