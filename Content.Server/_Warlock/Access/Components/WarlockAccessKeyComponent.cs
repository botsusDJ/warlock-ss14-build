using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Server._Warlock.Access.Components;

/// <summary>
/// _Warlock
/// Одноразовый ключ-карта Братства Стали.
///
/// Намеренно НЕ имеет компонента Access: обычный считыватель такую карту не видит,
/// и носить её в слоте ID бесполезно. Работает только грубо — картой тычут прямо в дверь,
/// дверь открывается, карта остаётся в приёмнике. Именно этого колхоза Братство и держится:
/// собственной электроники у них нет, есть трофейные карты, которые они режут на одноразовые.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockAccessKeyComponent : Component
{
    /// <summary>
    /// Какие уровни доступа открывает карта.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<AccessLevelPrototype>> Tags = new();

    /// <summary>
    /// Сгорает ли карта после срабатывания. Выключается для отладочных «вечных» карт.
    /// </summary>
    [DataField]
    public bool Consume = true;
}
