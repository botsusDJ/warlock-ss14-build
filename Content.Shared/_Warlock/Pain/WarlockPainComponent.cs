using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Pain;

/// <summary>
/// _Warlock — ступени боли.
///
/// Ступеней пять, и границы между ними намеренно широкие. Дробная шкала на сотню
/// делений игроку не видна: он замечает не число, а момент, когда тело перестало
/// слушаться. Поэтому важно не сколько именно боли, а через какой порог она перешла.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockPainLevel : byte
{
    /// <summary>Ничего не мешает.</summary>
    None = 0,

    /// <summary>Ноет. Заметно, но ни на что не влияет, кроме сообщений.</summary>
    Ache = 1,

    /// <summary>Режет. Медленнее ходишь, слабее бьёшь.</summary>
    Sharp = 2,

    /// <summary>Агония. Речь рвётся, руки не держат, выносливость утекает.</summary>
    Agony = 3,

    /// <summary>Затмение. Тело отказывает: падаешь и кричишь.</summary>
    Blackout = 4,
}

/// <summary>
/// _Warlock
/// Боль.
///
/// Ванильная сска знает только урон и выносливость: пока полоска не кончилась,
/// боец в полном порядке, а на нуле мгновенно выключается. Промежутка между
/// «здоров» и «выключен» там нет, и весь бой сводится к гонке полосок.
///
/// Боль — это и есть тот промежуток. Она копится от урона и от травм, спадает
/// сама и по дороге отбирает у бойца ровно то, чем он воюет: скорость, силу удара,
/// внятную речь, способность держать предмет. Раненый не выключается, он
/// становится хуже — и решает, отступить или дожать.
///
/// Важно: боль НЕ убивает. Умирают от урона. Иначе она превратилась бы во вторую
/// полоску здоровья, и смысл потерялся бы.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockPainComponent : Component
{
    /// <summary>
    /// Сколько боли накоплено, от нуля до <see cref="Max"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Pain;

    [DataField]
    public float Max = 100f;

    /// <summary>
    /// Текущая ступень. Держится отдельно от числа, чтобы система видела сам
    /// момент перехода и могла показать сообщение один раз, а не каждый тик.
    /// </summary>
    [DataField, AutoNetworkedField]
    public WarlockPainLevel Level;

    /// <summary>
    /// Пороги ступеней. Расстояние между ними растёт: от «ноет» до «режет» ближе,
    /// чем от «агонии» до «затмения». Последние ступени должны доставаться тяжело,
    /// иначе бой в них и будет проходить целиком.
    /// </summary>
    [DataField]
    public float AcheAt = 18f;

    [DataField]
    public float SharpAt = 40f;

    [DataField]
    public float AgonyAt = 66f;

    [DataField]
    public float BlackoutAt = 88f;

    /// <summary>
    /// Сколько боли уходит за секунду само.
    ///
    /// Спад медленный намеренно. Быстрый спад означал бы, что достаточно отбежать
    /// на десять секунд и вернуться как новый, и вся система перестала бы влиять
    /// на решения.
    /// </summary>
    [DataField]
    public float Decay = 0.9f;

    /// <summary>
    /// Множитель чувствительности. Меньше единицы — боль притуплена.
    /// У унатхов она именно такая: они хладнокровные.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Sensitivity = 1f;

    /// <summary>
    /// Сколько боли даёт единица урона. Разные типы болят по-разному: ожог и
    /// кислота мучительнее тупого удара той же силы.
    /// </summary>
    [DataField]
    public Dictionary<string, float> PerDamage = new()
    {
        ["Blunt"] = 0.55f,
        ["Slash"] = 0.75f,
        ["Piercing"] = 0.70f,
        ["Heat"] = 1.15f,
        ["Caustic"] = 1.05f,
        ["Shock"] = 0.95f,
        ["Cold"] = 0.60f,
        ["Poison"] = 0.45f,
        ["Radiation"] = 0.25f,
        ["Asphyxiation"] = 0.35f,
        ["Bloodloss"] = 0.30f,
    };

    /// <summary>
    /// Сколько боли в секунду добавляет каждый непролеченный перелом.
    /// </summary>
    [DataField]
    public float PerFracture = 0.55f;

    /// <summary>
    /// Замедление на ступенях «режет», «агония» и «затмение».
    /// </summary>
    [DataField]
    public float SharpSlow = 0.88f;

    [DataField]
    public float AgonySlow = 0.72f;

    [DataField]
    public float BlackoutSlow = 0.55f;

    /// <summary>
    /// Насколько слабее удар в агонии и в затмении.
    /// </summary>
    [DataField]
    public float AgonyDamage = 0.85f;

    [DataField]
    public float BlackoutDamage = 0.7f;

    /// <summary>
    /// Шанс за тик выронить то, что в руках. Работает с «агонии».
    /// </summary>
    [DataField]
    public float DropChance = 0.12f;

    /// <summary>
    /// Шанс за тик упасть. Работает только в «затмении».
    /// </summary>
    [DataField]
    public float FallChance = 0.18f;

    /// <summary>
    /// Сколько выносливости съедает затмение за тик.
    /// </summary>
    [DataField]
    public float BlackoutStamina = 4f;

    /// <summary>
    /// Когда система в следующий раз посчитает последствия.
    /// </summary>
    [DataField]
    public TimeSpan NextTick;

    /// <summary>
    /// Раз в столько секунд считаются переломы, падения и потеря предметов.
    /// Спад боли идёт непрерывно, а вот бросать кубик каждый кадр не нужно.
    /// </summary>
    [DataField]
    public float TickInterval = 2f;

    [DataField]
    public ProtoId<AlertPrototype> Alert = "WarlockPain";
}
