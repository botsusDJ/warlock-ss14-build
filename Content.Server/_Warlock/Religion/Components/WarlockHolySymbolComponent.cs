using Content.Shared._Warlock.Religion;

namespace Content.Server._Warlock.Religion.Components;

/// <summary>
/// _Warlock
/// Переносная культовая атрибутика. Сама по себе ничего не делает — это усилитель:
/// молитва с подходящим символом в руках идёт быстрее и даёт больше.
/// Чужому богу символ не помогает и слегка мешает.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockHolySymbolComponent : Component
{
    /// <summary>
    /// Чей это символ.
    /// </summary>
    [DataField]
    public WarlockGod God = WarlockGod.Kass;

    /// <summary>
    /// Во сколько раз усиливается эффект молитвы.
    /// </summary>
    [DataField]
    public float PowerMultiplier = 1.6f;

    /// <summary>
    /// Множитель длительности молитвы. Меньше единицы — молиться быстрее.
    /// </summary>
    [DataField]
    public float TimeMultiplier = 0.6f;
}
