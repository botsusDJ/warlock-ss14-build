using Robust.Shared.GameStates;

namespace Content.Shared._Warlock.Injuries;

/// <summary>
/// _Warlock
/// Летопись тела: что на нём осталось от полученного урона и от чужих намерений.
///
/// Хранится списком отдельных записей, потому что теперь важно не только «сколько»,
/// но и «где»: перелом в ноге замедляет, а в руке — нет.
///
/// Компонент сетевой: переломы и синяки влияют на движение и выносливость,
/// а это клиент обязан предсказывать, иначе персонажа будет дёргать.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockInjuriesComponent : Component
{
    /// <summary>
    /// Всё, что сейчас на теле.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<WarlockInjury> Injuries = new();

    /// <summary>
    /// Потолок на каждый вид травм, чтобы летопись не росла до бесконечности.
    /// Зубы и глаза считаются отдельно: их и так мало.
    /// </summary>
    [DataField]
    public int MaxPerType = 6;

    /// <summary>
    /// Сколько зубов вообще можно выбить.
    /// </summary>
    [DataField]
    public int MaxTeeth = 8;

    #region Пороги накопления

    /// <summary>
    /// Сколько режущего или колющего урона за раз даёт ссадину.
    /// </summary>
    [DataField]
    public float AbrasionThreshold = 12f;

    /// <summary>
    /// Сколько тупого урона за раз даёт синяк.
    /// </summary>
    [DataField]
    public float BruiseThreshold = 15f;

    /// <summary>
    /// Сколько тупого урона за раз ломает кость.
    /// </summary>
    [DataField]
    public float FractureThreshold = 40f;

    /// <summary>
    /// Шанс выбить зуб, когда сильный тупой удар пришёлся в голову.
    /// </summary>
    [DataField]
    public float ToothChance = 0.35f;

    /// <summary>
    /// Шанс потерять глаз при переломе черепа. Держится намеренно низким:
    /// это должно быть событием на всю смену, а не рядовой неприятностью.
    /// </summary>
    [DataField]
    public float EyeChance = 0.04f;

    #endregion

    #region Заживление

    /// <summary>
    /// Момент следующей проверки заживления.
    /// </summary>
    [DataField]
    public TimeSpan NextHeal;

    /// <summary>
    /// Как часто тело зализывает раны.
    /// </summary>
    [DataField]
    public TimeSpan HealInterval = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Сколько проверок заживления должен пережить перелом, прежде чем срастись.
    /// </summary>
    [DataField]
    public int FractureTicksToHeal = 4;

    /// <summary>
    /// Счётчик проверок для текущего перелома.
    /// </summary>
    [DataField]
    public int FractureProgress;

    #endregion

    #region Механические последствия

    /// <summary>
    /// Насколько каждый перелом ноги замедляет ходьбу и бег.
    /// </summary>
    [DataField]
    public float LegFractureSlowdown = 0.85f;

    /// <summary>
    /// Насколько каждый перелом не в ноге режет порог оглушения от усталости.
    /// </summary>
    [DataField]
    public float FractureStaminaPenalty = 0.9f;

    /// <summary>
    /// Насколько каждый синяк режет порог оглушения от усталости.
    /// </summary>
    [DataField]
    public float BruiseStaminaPenalty = 0.95f;

    /// <summary>
    /// Сколько повреждения глаз даёт потеря одного глаза.
    /// </summary>
    [DataField]
    public int EyeDamagePerEye = 3;

    #endregion
}
