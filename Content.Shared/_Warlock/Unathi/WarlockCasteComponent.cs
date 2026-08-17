using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Unathi;

/// <summary>
/// _Warlock — касты унатхов.
///
/// Каста — не должность и не выучка, а то, кем унатх вылупился. Поменять её нельзя,
/// и именно поэтому Королевство устроено так, как устроено: легионер не станет
/// строителем, даже если очень захочет.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockCaste : byte
{
    /// <summary>Легионер. Разъярённый и недалёкий воин.</summary>
    Legionary = 0,

    /// <summary>Матка Рузута. Выхаживает выводок и всех, кто рядом.</summary>
    Matriarch = 1,

    /// <summary>Строитель. Хрупкий, но руки быстрее любых чужих.</summary>
    Builder = 2,

    /// <summary>Высший. Видит в камне то, чего не видят остальные.</summary>
    Higher = 3,
}

/// <summary>
/// _Warlock
/// Каста унатха.
///
/// Один компонент на все четыре касты вместо четырёх отдельных: касты — это ветки
/// одного выбора, они взаимоисключающие, и держать их разными компонентами значило бы
/// разрешить унатха-легионера-и-строителя одновременно.
///
/// Числа лежат здесь же, а не в системе, чтобы касту можно было подкрутить в YAML
/// без пересборки.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockCasteComponent : Component
{
    [DataField, AutoNetworkedField]
    public WarlockCaste Caste = WarlockCaste.Legionary;

    #region Легионер

    /// <summary>
    /// Насколько сильнее бьёт легионер, когда изранен досуха.
    ///
    /// Ярость растёт от собственных ран: целый легионер бьёт как обычный унатх,
    /// добитый — вдвое сильнее. Это делает его опасным именно в тот момент, когда
    /// его считают выбитым, и ровно за это Королевство его и держит.
    /// </summary>
    [DataField]
    public float RageDamage = 1.0f;

    /// <summary>
    /// Во сколько раз дольше легионер возится со всем, что требует времени.
    /// Он не глуп руками — он не может на этом сосредоточиться.
    /// </summary>
    [DataField]
    public float LegionarySlowHands = 1.5f;

    #endregion

    #region Матка Рузута

    /// <summary>Радиус выхаживания в тайлах.</summary>
    [DataField]
    public float BroodRange = 4.5f;

    /// <summary>Сколько ран затягивается за один такт у каждого в радиусе.</summary>
    [DataField]
    public float BroodHeal = 1.8f;

    /// <summary>
    /// Матка платит за это своим. Ноль означал бы бесплатное лечение отряда,
    /// и в бою рядом с маткой не было бы риска ни для кого.
    /// </summary>
    [DataField]
    public float BroodCost = 0.6f;

    #endregion

    #region Строитель

    /// <summary>
    /// Во сколько раз быстрее строитель делает всё, что требует времени.
    /// Это его единственная сила и вся причина, по которой его берут.
    /// </summary>
    [DataField]
    public float BuilderFastHands = 0.55f;

    /// <summary>
    /// Насколько строитель хрупче остальных. Множитель к получаемому урону.
    /// </summary>
    [DataField]
    public float BuilderFragility = 1.35f;

    #endregion

    /// <summary>Раз в столько секунд считается аура матки.</summary>
    [DataField]
    public float TickInterval = 2f;

    [DataField]
    public TimeSpan NextTick;
}

/// <summary>
/// _Warlock
/// Умение читать находки.
///
/// Обычный игрок узнаёт, что делает реликвия, единственным способом — применив её.
/// Носитель этого компонента видит содержимое при осмотре, и это главная ценность
/// Фактоса, ритуалистов и высшей касты: они превращают лотерею в работу.
///
/// Компонент пустой намеренно: это метка, а не набор настроек. Выдаётся ролям через
/// AddComponentSpecial и снимается вместе со сменой роли.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WarlockArtefactSightComponent : Component;
