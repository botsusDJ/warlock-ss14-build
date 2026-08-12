using Robust.Shared.GameStates;

namespace Content.Shared._Warlock.Injuries;

/// <summary>
/// _Warlock
/// Летопись тела: что на нём осталось от полученного урона и от чужих намерений.
///
/// Хранится счётчиками, а не списком отдельных ран — так дешевле по сети и проще читать.
/// Компонент сетевой: переломы и синяки влияют на движение и выносливость,
/// а это клиент обязан предсказывать, иначе персонажа будет дёргать.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockInjuriesComponent : Component
{
    /// <summary>
    /// Сколько травм каждого вида сейчас на теле.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<WarlockInjuryType, int> Injuries = new();

    /// <summary>
    /// Тексты поставленных клейм. Клеймо всегда именное: важно не что оно есть, а чьё оно.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> Brands = new();

    /// <summary>
    /// Потолок на каждый вид травм, чтобы летопись не росла до бесконечности.
    /// </summary>
    [DataField]
    public int MaxPerType = 6;

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
    /// Насколько каждый перелом замедляет ходьбу и бег.
    /// </summary>
    [DataField]
    public float FractureSlowdown = 0.9f;

    /// <summary>
    /// Насколько каждый синяк режет порог оглушения от усталости.
    /// </summary>
    [DataField]
    public float BruiseStaminaPenalty = 0.95f;

    #endregion
}
