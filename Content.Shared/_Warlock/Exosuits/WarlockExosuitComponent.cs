using Robust.Shared.GameStates;

namespace Content.Shared._Warlock.Exosuits;

/// <summary>
/// _Warlock
/// Экзоскелет Братства Стали.
///
/// Братство не умеет строить, оно умеет сваривать. Экзоскелет у них — это рама из чужих
/// приводов на чужой батарее, и ведёт она себя соответственно: пока есть заряд, боец бьёт
/// заметно сильнее и почти не замечает веса рамы. Как только батарея садится, рама
/// перестаёт помогать и превращается в несколько десятков килограммов железа на плечах.
///
/// Отсюда весь смысл вещи: экзоскелет силён не тем, сколько он даёт, а тем, чем за это
/// платят. Боец в севшей раме медленнее безоружного и не может ни догнать, ни убежать,
/// пока не сменит батарею или не снимет её с себя.
///
/// Питание, слот батареи и само переключение сделаны ванильными узлами — PowerCellSlot,
/// PowerCellDraw и ItemToggle. Своего здесь только то, чего в ванили нет: прибавка к удару
/// и тяжесть обесточенной рамы.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockExosuitComponent : Component
{
    /// <summary>
    /// Во сколько раз сильнее бьёт носитель, пока рама под питанием.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StrengthBonus = 1.35f;

    /// <summary>
    /// Скорость под питанием. Чуть меньше единицы: рама помогает, но не невесома.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PoweredWalk = 0.95f;

    [DataField, AutoNetworkedField]
    public float PoweredSprint = 0.95f;

    /// <summary>
    /// Скорость, когда рама надета, но не работает. Это и есть цена вопроса.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DeadWalk = 0.55f;

    [DataField, AutoNetworkedField]
    public float DeadSprint = 0.5f;

    /// <summary>
    /// Слот, в котором рама вообще что-то делает. В руках или в рюкзаке экзоскелет
    /// не должен ни ускорять, ни замедлять.
    /// </summary>
    [DataField]
    public string Slot = "outerClothing";
}

/// <summary>
/// _Warlock
/// Висит на том, кто носит работающий экзоскелет.
///
/// Нужен затем, чтобы прибавку к удару не искать перебором инвентаря на каждый замах:
/// удар — самое частое событие в бою, и лазить в него за экипировкой дорого.
/// Здесь лежит уже посчитанное число.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockExosuitWearerComponent : Component
{
    /// <summary>
    /// Множитель урона в ближнем бою. Единица означает, что рама надета, но обесточена.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Bonus = 1f;
}
