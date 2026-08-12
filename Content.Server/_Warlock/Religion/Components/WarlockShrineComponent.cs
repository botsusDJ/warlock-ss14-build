using Content.Shared._Warlock.Religion;

namespace Content.Server._Warlock.Religion.Components;

/// <summary>
/// _Warlock
/// Святилище, у которого можно молиться. Молитва — это дорогой и заметный ритуал:
/// она занимает время, прерывается движением и уроном, и у святилища есть общий откат.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockShrineComponent : Component
{
    /// <summary>
    /// Кому посвящено святилище.
    /// </summary>
    [DataField]
    public WarlockGod God = WarlockGod.Kass;

    /// <summary>
    /// Сколько секунд длится молитва.
    /// </summary>
    [DataField]
    public float PrayerTime = 8f;

    /// <summary>
    /// Радиус, в котором действует эффект молитвы.
    /// </summary>
    [DataField]
    public float Radius = 4f;

    /// <summary>
    /// Откат святилища между молитвами.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Когда святилище снова услышит просьбу.
    /// </summary>
    [DataField]
    public TimeSpan NextPrayer;

    /// <summary>
    /// Касс: сколько псионической энергии возвращает молитва.
    /// </summary>
    [DataField]
    public float EnergyRestored = 45f;

    /// <summary>
    /// Механтехион: сколько урона снимается с каждой неживой цели в радиусе.
    /// </summary>
    [DataField]
    public float RepairAmount = 40f;

    /// <summary>
    /// Атрак: на сколько секунд молитва вводит в ярость.
    /// </summary>
    [DataField]
    public float RageSeconds = 30f;

    /// <summary>
    /// Рузут: сколько урона заживляет молитва.
    /// </summary>
    [DataField]
    public float HealAmount = 50f;
}
