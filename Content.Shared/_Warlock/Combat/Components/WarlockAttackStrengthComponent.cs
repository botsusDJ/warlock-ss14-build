using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Combat.Components;

/// <summary>
/// _Warlock
/// Сила ближнего удара, выбранная бойцом. Работает и с кулаками, и с любым оружием ближнего боя:
/// модификатор вешается на само оружие в момент расчёта урона, а платит за него владелец.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockAttackStrengthComponent : Component
{
    /// <summary>
    /// Текущий выбранный режим.
    /// </summary>
    [DataField, AutoNetworkedField]
    public WarlockAttackStrength Strength = WarlockAttackStrength.Normal;

    /// <summary>
    /// Множитель урона слабого удара. Толкнуть, а не покалечить.
    /// </summary>
    [DataField]
    public float WeakDamage = 0.3f;

    /// <summary>
    /// Множитель урона сильного удара.
    /// </summary>
    [DataField]
    public float StrongDamage = 1.9f;

    /// <summary>
    /// Множитель скорости слабых ударов — они заметно быстрее обычных.
    /// </summary>
    [DataField]
    public float WeakAttackRate = 1.4f;

    /// <summary>
    /// Множитель скорости сильных ударов — замах долгий.
    /// </summary>
    [DataField]
    public float StrongAttackRate = 0.55f;

    /// <summary>
    /// Расход выносливости за слабый удар.
    /// </summary>
    [DataField]
    public float WeakStaminaCost;

    /// <summary>
    /// Расход выносливости за обычный удар. Ноль — ванильное поведение не меняется.
    /// </summary>
    [DataField]
    public float NormalStaminaCost;

    /// <summary>
    /// Расход выносливости за сильный удар. Именно он делает режим по-настоящему дорогим.
    /// </summary>
    [DataField]
    public float StrongStaminaCost = 20f;

    /// <summary>
    /// Алерт, показывающий текущий режим.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> Alert = "WarlockAttackStrength";
}

/// <summary>
/// _Warlock — три ступени силы удара.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockAttackStrength : byte
{
    /// <summary>
    /// Почти безвредный тычок: быстрый, дешёвый, годится чтобы растолкать или не убить.
    /// </summary>
    Weak = 0,

    /// <summary>
    /// Ванильный удар без изменений.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// Тяжёлый замах: больно, медленно и дорого по выносливости.
    /// </summary>
    Strong = 2,
}
