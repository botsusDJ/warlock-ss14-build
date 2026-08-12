using Robust.Shared.GameStates;

namespace Content.Shared._Warlock.Unathi;

/// <summary>
/// _Warlock
/// Берсеркство унатха. Крестовый поход объявлен, и это его механическое выражение:
/// пока держится ярость, унатх бьёт сильнее и держит удар, а когда она отпускает —
/// падает с ног от накопленной усталости.
///
/// Компонент сетевой: он трогает урон и выносливость, клиент обязан это предсказывать.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockBerserkComponent : Component
{
    /// <summary>
    /// Когда ярость отпустит.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan EndAt;

    /// <summary>
    /// Во сколько раз сильнее бьёт берсерк.
    /// </summary>
    [DataField]
    public float DamageModifier = 1.5f;

    /// <summary>
    /// Во сколько раз меньше урона он получает.
    /// </summary>
    [DataField]
    public float ResistModifier = 0.75f;

    /// <summary>
    /// Во сколько раз выше порог оглушения от усталости, пока держится ярость.
    /// </summary>
    [DataField]
    public float StaminaModifier = 2f;

    /// <summary>
    /// Сколько усталости обрушивается на унатха, когда ярость проходит.
    /// Это и есть цена: берсеркство не бесплатное, оно взятое в долг.
    /// </summary>
    [DataField]
    public float Backlash = 60f;
}
