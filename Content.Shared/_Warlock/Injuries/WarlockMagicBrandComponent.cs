using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Injuries;

/// <summary>
/// _Warlock — что именно делает магическое клеймо.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockBrandEffect : byte
{
    /// <summary>
    /// Обычное клеймо: только надпись на коже.
    /// </summary>
    None = 0,

    /// <summary>
    /// Оковы. Руки не работают: ничего не поднять, ничем не воспользоваться, никого не ударить.
    /// </summary>
    Shackles = 1,

    /// <summary>
    /// Корни. Ноги не идут. Персонаж жив, в сознании, при руках — и прикован к месту.
    /// </summary>
    Roots = 2,

    /// <summary>
    /// Пепел. Выносливость выгорела: любое усилие валит с ног почти сразу.
    /// </summary>
    Ashes = 3,
}

/// <summary>
/// _Warlock
/// Магическое клеймо на теле. В отличие от обычного, это не надпись, а приговор:
/// снять нельзя ничем, вылечить нельзя ничем, и действует оно до конца смены.
///
/// Каждое из трёх отнимает ровно одну возможность и не трогает остальные.
/// Скованный ходит, укоренённый дерётся, выжженный делает и то и другое —
/// ровно до первого усилия. Так каждое клеймо остаётся отдельным наказанием,
/// а не общим «персонаж больше не играет».
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockMagicBrandComponent : Component
{
    /// <summary>
    /// Какие клейма на этом теле. Их может быть несколько, и они складываются.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<WarlockBrandEffect> Effects = new();

    /// <summary>
    /// Во сколько раз клеймо Пепла режет порог оглушения от усталости.
    /// </summary>
    [DataField]
    public float AshesStaminaPenalty = 0.15f;
}
