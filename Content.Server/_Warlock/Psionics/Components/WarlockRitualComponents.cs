using Content.Shared.Damage.Prototypes;
using Content.Shared.Explosion;
using Robust.Shared.Prototypes;

namespace Content.Server._Warlock.Psionics.Components;

/// <summary>
/// _Warlock — активная «Проклятая Хватка».
/// Пока висит, всё взятое в руки разрывает, а носитель затягивает свои раны.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockCursedGraspComponent : Component
{
    [DataField]
    public TimeSpan EndAt;

    [DataField]
    public TimeSpan NextTick;

    /// <summary>
    /// Сколько урона снимается с носителя за тик.
    /// </summary>
    [DataField]
    public float HealPerTick = 4f;

    /// <summary>
    /// Мощность разрыва предмета.
    /// </summary>
    [DataField]
    public float ExplosionIntensity = 8f;

    [DataField]
    public ProtoId<ExplosionPrototype> ExplosionType = "Default";
}

/// <summary>
/// _Warlock — активное «Чутьё Реликвий».
/// Раз в несколько секунд подсказывает носителю сторону и расстояние до ближайшего артефакта.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockRelicScentComponent : Component
{
    [DataField]
    public TimeSpan EndAt;

    [DataField]
    public TimeSpan NextTick;

    [DataField]
    public float TickInterval = 3f;

    [DataField]
    public float Radius = 60f;
}

/// <summary>
/// _Warlock — активный «Погребальный Костёр».
/// Источник жара — сам носитель, поэтому область считается от его текущего положения каждый тик.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockPyreAuraComponent : Component
{
    [DataField]
    public TimeSpan EndAt;

    [DataField]
    public TimeSpan NextTick;

    [DataField]
    public float TickInterval = 1f;

    [DataField]
    public float Radius = 3f;

    /// <summary>
    /// Целевая температура тайлов вокруг, в кельвинах.
    /// </summary>
    [DataField]
    public float Temperature = 2000f;

    /// <summary>
    /// Сколько тепла прилетает каждому в области за тик, включая самого носителя.
    /// </summary>
    [DataField]
    public float HeatPerTick = 8f;
}

/// <summary>
/// _Warlock — активная «Личина Брата».
/// Помнит, что именно было снято, чтобы вернуть это владельцу, когда личина спадёт.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockFalseBrotherComponent : Component
{
    [DataField]
    public TimeSpan EndAt;

    /// <summary>
    /// Слот -> спавненная вещь Братства. Её надо удалить при снятии личины.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, EntityUid> Disguise = new();

    /// <summary>
    /// Слоты, из которых было что-то снято. Содержимое лежит в контейнере <see cref="StashId"/>.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, EntityUid> Stashed = new();

    [DataField]
    public string StashId = "warlock_disguise_stash";
}

/// <summary>
/// _Warlock — след «Литании Укрепления» на самом техномаге.
/// Снять нельзя: это и есть цена ритуала.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockHollowedComponent : Component
{
    /// <summary>
    /// Накопленный множитель порога усталости. Каждый новый ритуал делает его меньше.
    /// </summary>
    [DataField]
    public float StaminaMultiplier = 1f;

    /// <summary>
    /// Сколько раз техномаг проводил ритуал.
    /// </summary>
    [DataField]
    public int Rites;
}

/// <summary>
/// _Warlock — метка укреплённого механизма, чтобы ритуал не проводили на одном и том же дважды.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockBulwarkedComponent : Component
{
    [DataField]
    public ProtoId<DamageModifierSetPrototype>? PreviousModifiers;
}

/// <summary>
/// _Warlock — какую печать оставил техномаг и что она сделает наступившему.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockRuneComponent : Component
{
    [DataField]
    public WarlockRuneEffect Effect = WarlockRuneEffect.Hurling;

    /// <summary>
    /// Кто поставил печать. На него самого она не срабатывает.
    /// </summary>
    [DataField]
    public EntityUid? Caster;

    /// <summary>
    /// Сила броска для печати отбрасывания.
    /// </summary>
    [DataField]
    public float ThrowStrength = 14f;

    /// <summary>
    /// Дальность броска в тайлах.
    /// </summary>
    [DataField]
    public float ThrowDistance = 10f;

    /// <summary>
    /// Мощность взрыва печати гнили.
    /// </summary>
    [DataField]
    public float ExplosionIntensity = 12f;

    [DataField]
    public ProtoId<ExplosionPrototype> ExplosionType = "Default";

    /// <summary>
    /// Радиус, в котором печать гнили травит живое.
    /// </summary>
    [DataField]
    public float PoisonRadius = 3f;

    /// <summary>
    /// Сколько яда получает каждый в радиусе.
    /// </summary>
    [DataField]
    public float PoisonDamage = 35f;
}

/// <summary>
/// _Warlock — что делает печать при срабатывании.
/// </summary>
public enum WarlockRuneEffect : byte
{
    /// <summary>
    /// Швыряет наступившего далеко прочь.
    /// </summary>
    Hurling = 0,

    /// <summary>
    /// Вскрывается взрывом и травит всё живое рядом.
    /// </summary>
    Blight = 1,
}
