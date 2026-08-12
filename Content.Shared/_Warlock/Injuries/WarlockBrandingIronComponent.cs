using Robust.Shared.GameStates;

namespace Content.Shared._Warlock.Injuries;

/// <summary>
/// _Warlock
/// Клеймо. Ставится на живого человека и не сходит никогда — в этом весь смысл.
/// Королевство Унатхи клеймит рабов, Братство Стали — своих.
///
/// Настраиваемое клеймо позволяет через меню взаимодействия выбрать надпись и место.
/// Готовые клейма фракций идут с фиксированной надписью.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockBrandingIronComponent : Component
{
    /// <summary>
    /// Что выжигается по умолчанию. Строка локализации: у фракционных клейм надпись фиксирована.
    /// </summary>
    [DataField]
    public LocId Brand = "warlock-brand-blank";

    /// <summary>
    /// Надпись, набранная владельцем. Если задана, идёт вместо <see cref="Brand"/>.
    /// Хранится как есть, без локализации: это произвольный текст игрока.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? CustomText;

    /// <summary>
    /// Можно ли менять надпись через меню взаимодействия.
    /// У фракционных клейм выключено: оттиск отлит и не меняется.
    /// </summary>
    [DataField]
    public bool Adjustable;

    /// <summary>
    /// Куда будет поставлено клеймо.
    /// </summary>
    [DataField, AutoNetworkedField]
    public WarlockBodyPart TargetPart = WarlockBodyPart.Torso;

    /// <summary>
    /// Максимальная длина набранной надписи.
    /// </summary>
    [DataField]
    public int MaxLength = 32;

    /// <summary>
    /// Сколько секунд занимает клеймение. Долго намеренно: это не должно быть тычком в толпе.
    /// </summary>
    [DataField]
    public float Delay = 6f;

    /// <summary>
    /// Сколько жара получает заклеймённый.
    /// </summary>
    [DataField]
    public float Burn = 12f;
}
