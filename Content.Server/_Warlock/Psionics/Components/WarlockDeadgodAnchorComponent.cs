using Robust.Shared.Map;

namespace Content.Server._Warlock.Psionics.Components;

/// <summary>
/// _Warlock
/// Печать «Отзвука Мёртвого Бога». Помнит, где стояли существа в момент установки,
/// и при схлопывании возвращает их туда же.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockDeadgodAnchorComponent : Component
{
    /// <summary>
    /// Радиус, в котором печать снимает и восстанавливает слепок положений.
    /// </summary>
    [DataField]
    public float Radius = 5f;

    /// <summary>
    /// Сколько секунд печать держится перед схлопыванием.
    /// </summary>
    [DataField]
    public float Delay = 12f;

    /// <summary>
    /// Момент схлопывания.
    /// </summary>
    [DataField]
    public TimeSpan CollapseAt;

    /// <summary>
    /// Кто поставил печать. Ему приходит уведомление о срабатывании.
    /// </summary>
    [DataField]
    public EntityUid? Caster;

    /// <summary>
    /// Слепок: кого и куда возвращать.
    /// </summary>
    [ViewVariables]
    public List<(EntityUid Entity, EntityCoordinates Coordinates)> Snapshot = new();
}
