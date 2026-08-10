using Content.Shared.Alert;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Warlock.Psionics.Components;

/// <summary>
/// _Warlock
/// Носитель псионического дара. Хранит запас псионической энергии — ресурс, который тратят заклинания
/// техномагов. Энергия восстанавливается сама, но медленно, поэтому техномаг вынужден выбирать,
/// на что её потратить, а не спамить способностями.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockPsionicComponent : Component
{
    /// <summary>
    /// Текущий запас псионической энергии.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 Energy = 100;

    /// <summary>
    /// Максимальный запас энергии.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxEnergy = 100;

    /// <summary>
    /// Сколько энергии восстанавливается за секунду.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 RegenPerSecond = FixedPoint2.New(1.5);

    /// <summary>
    /// Алерт-счётчик, показывающий запас энергии в интерфейсе.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> Alert = "WarlockPsiEnergy";

    /// <summary>
    /// Накопитель времени для регенерации. Не сохраняется и не сетится.
    /// </summary>
    [ViewVariables]
    public float RegenAccumulator;
}
