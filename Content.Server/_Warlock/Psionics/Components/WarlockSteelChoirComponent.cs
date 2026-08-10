namespace Content.Server._Warlock.Psionics.Components;

/// <summary>
/// _Warlock
/// Активная фаза «Хора Стали»: вокруг носителя вращается стянутый металл.
/// Когда таймер истекает, всё это разлетается наружу.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockSteelChoirComponent : Component
{
    /// <summary>
    /// Момент, когда хор "допоёт" и предметы разлетятся.
    /// </summary>
    [DataField]
    public TimeSpan BurstAt;

    /// <summary>
    /// Радиус, из которого предметы выбрасывает наружу.
    /// </summary>
    [DataField]
    public float BurstRadius = 2.5f;

    /// <summary>
    /// Максимум предметов, которые разлетятся.
    /// </summary>
    [DataField]
    public int MaxItems = 20;
}
