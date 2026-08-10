namespace Content.Server._Warlock.Artefacts.Components;

/// <summary>
/// _Warlock — «Кость Судьбы».
/// Артефакт вымершей расы в форме двадцатигранника. Он не предсказывает исход, он его назначает.
/// Чем выше выпавшее число, тем щедрее артефакт; единица — приговор.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockFateDieComponent : Component
{
    /// <summary>
    /// Сколько псионической энергии возвращает бросок при хорошем результате (умножается на бросок).
    /// </summary>
    [DataField]
    public float EnergyPerPip = 3f;

    /// <summary>
    /// Урон по выносливости на неудачном броске (2-5).
    /// </summary>
    [DataField]
    public float FailureStamina = 40f;

    /// <summary>
    /// Урон электричеством при критическом провале (бросок 1).
    /// </summary>
    [DataField]
    public float CritFailureShock = 25f;
}
