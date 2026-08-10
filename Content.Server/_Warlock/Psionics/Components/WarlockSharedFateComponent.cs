namespace Content.Server._Warlock.Psionics.Components;

/// <summary>
/// _Warlock
/// Одна сторона связи «Разделённой Участи». Вешается сразу на обоих участников;
/// урон, полученный одним, частично прилетает второму.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockSharedFateComponent : Component
{
    /// <summary>
    /// Второй участник связи.
    /// </summary>
    [DataField]
    public EntityUid Partner;

    /// <summary>
    /// Момент, когда связь распадётся.
    /// </summary>
    [DataField]
    public TimeSpan EndAt;

    /// <summary>
    /// Доля урона, перекидываемая на партнёра.
    /// </summary>
    [DataField]
    public float Coefficient = 0.5f;

    /// <summary>
    /// Защита от бесконечного пинг-понга уроном между связанными.
    /// </summary>
    [ViewVariables]
    public bool Relaying;
}
