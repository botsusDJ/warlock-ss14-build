namespace Content.Server._Warlock.Psionics.Components;

// _Warlock
// Временные эффекты заклинаний второго набора.
//
// Все шесть устроены одинаково: поле EndAt и параметры, которые заклинание записало
// при наложении. Снимает их общий проход в WarlockExpandedSpellsSystem.Update.
//
// Компоненты серверные, как и WarlockGlassSkinComponent: клиент их не видит и не должен.
// Предсказывать чужие бафы всё равно нечем, а сетевой компонент, добавленный из общего
// кода, роняет клиент посреди отката предсказанных сущностей.
//
// Каждый эффект — отдельный компонент, а не общий с флагами. Причина техническая:
// движок допускает одну направленную подписку на пару «компонент + событие» во всей
// сборке, и три разных бафа на скорость обязаны висеть на трёх разных компонентах.

/// <summary>
/// _Warlock — «Твёрдая Хватка». Всё, что требует времени, идёт быстрее.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockSteadyGripComponent : Component
{
    [DataField]
    public TimeSpan EndAt;

    /// <summary>Множитель длительности действий. Меньше единицы — быстрее.</summary>
    [DataField]
    public float Multiplier = 0.6f;
}

/// <summary>
/// _Warlock — «Лёгкий Шаг». Только скорость и ничего больше.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockLightStepComponent : Component
{
    [DataField]
    public TimeSpan EndAt;

    [DataField]
    public float Speed = 1.25f;
}

/// <summary>
/// _Warlock — «Статический Покров». Держит удар, но не даёт бежать.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockStaticShroudComponent : Component
{
    [DataField]
    public TimeSpan EndAt;

    [DataField]
    public float Resist = 0.55f;

    [DataField]
    public float Slow = 0.7f;
}

/// <summary>
/// _Warlock — «Могильные Путы». Цель не сходит с места.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockGravebindComponent : Component
{
    [DataField]
    public TimeSpan EndAt;
}

/// <summary>
/// _Warlock — «Освящение». По носителю бьёт слабее.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockConsecratedComponent : Component
{
    [DataField]
    public TimeSpan EndAt;

    [DataField]
    public float Resist = 0.75f;
}

/// <summary>
/// _Warlock — «Цепь Приказа». Быстрее и выносливее, пока приказ в силе.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockRalliedComponent : Component
{
    [DataField]
    public TimeSpan EndAt;

    [DataField]
    public float Speed = 1.2f;

    [DataField]
    public float Stamina = 1.35f;
}

/// <summary>
/// _Warlock — «Обет Молчания» держится.
///
/// Отдельно от самого подавления дара: подавление ставится ванильным для нас
/// WarlockPsiSuppressedComponent, который не умеет истекать сам. Этот компонент
/// помнит, когда его снять, и снимает — но только если дар не отняли ещё и
/// «Отлучением», у которого срока нет вовсе.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockVowOfSilenceComponent : Component
{
    [DataField]
    public TimeSpan EndAt;
}

/// <summary>
/// _Warlock — «Отлучение». Дар снят решением главы гильдии и сам не вернётся.
///
/// Метка нужна, чтобы обет молчания, истекая, не возвращал дар отлучённому:
/// это разные наказания с разными сроками, и короткое не должно отменять долгое.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockExcommunicatedComponent : Component
{
    /// <summary>Кто отлучил. Вернуть дар может он же — тем же жестом.</summary>
    [DataField]
    public EntityUid? By;
}
