using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Warlock.Psionics.Components;

/// <summary>
/// _Warlock
/// Вешается на сущность-действие (заклинание). Задаёт, сколько псионической энергии стоит каст
/// и требуется ли для него псионический дар вообще.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockPsiCostComponent : Component
{
    /// <summary>
    /// Стоимость каста в псионической энергии.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 Cost = 10;

    /// <summary>
    /// Если true — способностью не может воспользоваться существо без <see cref="WarlockPsionicComponent"/>.
    /// Отключается для артефактов, которые работают в руках у кого угодно.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool RequiresPsionics = true;
}
