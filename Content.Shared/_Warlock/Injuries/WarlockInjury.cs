using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Injuries;

/// <summary>
/// _Warlock — одна запись в летописи тела: что, где и, если это клеймо, с какой надписью.
/// </summary>
[Serializable, NetSerializable]
public struct WarlockInjury
{
    public WarlockInjuryType Type;
    public WarlockBodyPart Part;

    /// <summary>
    /// Текст клейма. У остальных травм пуст: клеймо единственное, что бывает именным.
    /// </summary>
    public string? Text;

    public WarlockInjury(WarlockInjuryType type, WarlockBodyPart part, string? text = null)
    {
        Type = type;
        Part = part;
        Text = text;
    }
}
