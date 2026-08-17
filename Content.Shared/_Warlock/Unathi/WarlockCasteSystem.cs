using Content.Shared._Warlock.Interaction;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Warlock.Unathi;

/// <summary>
/// _Warlock
/// Касты унатхов.
///
/// Королевство держится не на должностях, а на том, кем унатх вылупился. Четыре касты
/// делят между собой ровно те роли, которые в бою нельзя совмещать:
///
///   легионер  — бьёт тем сильнее, чем хуже ему самому, но ничего не может руками;
///   матка     — выхаживает всех рядом за счёт собственного здоровья;
///   строитель — делает всё вдвое быстрее и умирает вдвое легче;
///   высший    — читает находки и двигает вещи не касаясь.
///
/// Каждая каста — это размен, а не ступень. Легионер сильнее строителя в драке ровно
/// настолько, насколько бесполезен там, где надо что-то починить.
/// </summary>
public sealed partial class WarlockCasteSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockCasteComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<WarlockCasteComponent, WarlockDoAfterSpeedEvent>(OnHandSpeed);
        SubscribeLocalEvent<WarlockCasteComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<WarlockCasteComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<WarlockCasteComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextTick = _timing.CurTime;
    }

    #region Руки

    /// <summary>
    /// Ловкость рук. Строитель делает всё заметно быстрее, легионер — медленнее.
    ///
    /// Множитель именно множится: если поверх касты появится ещё источник — травма
    /// или экзоскелет, — он ляжет сверху, а не затрёт кастовый.
    /// </summary>
    private void OnHandSpeed(Entity<WarlockCasteComponent> ent, ref WarlockDoAfterSpeedEvent args)
    {
        args.Multiplier *= ent.Comp.Caste switch
        {
            WarlockCaste.Builder => ent.Comp.BuilderFastHands,
            WarlockCaste.Legionary => ent.Comp.LegionarySlowHands,
            _ => 1f,
        };
    }

    #endregion

    #region Хрупкость

    private void OnDamageModify(Entity<WarlockCasteComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.Caste != WarlockCaste.Builder)
            return;

        args.Damage *= ent.Comp.BuilderFragility;
    }

    #endregion

    #region Ярость легионера

    /// <summary>
    /// Множитель урона легионера от его собственных ран.
    ///
    /// Вызывается из WarlockAttackStrengthSystem: пара MeleeWeaponComponent +
    /// GetMeleeDamageEvent допускает одну подписку на весь билд, и она занята там.
    /// </summary>
    public float MeleeModifier(EntityUid user)
    {
        if (!TryComp<WarlockCasteComponent>(user, out var caste)
            || caste.Caste != WarlockCaste.Legionary)
            return 1f;

        // Порог смерти и накопленный урон берутся через системы, а не из полей.
        //
        // К DamageableComponent.TotalDamage песочница движка не пускает: у поля
        // права rwxrwx---, и прямое чтение падает анализатором RA0002. Разбирать
        // словарь порогов руками тоже не надо — для этого есть готовый вызов,
        // и он заодно устойчив к тому, что порогов у моба может быть несколько.
        if (!_thresholds.TryGetDeadThreshold(user, out var deadThreshold)
            || deadThreshold is not { } dead
            || dead <= 0)
            return 1f;

        var total = _damageable.GetTotalDamage(user);
        var hurt = Math.Clamp((total / dead).Float(), 0f, 1f);
        return 1f + caste.RageDamage * hurt;
    }

    #endregion

    #region Осмотр

    private void OnExamined(Entity<WarlockCasteComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(
            $"warlock-caste-examine-{ent.Comp.Caste.ToString().ToLowerInvariant()}"));
    }

    #endregion

    #region Выводок матки

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Лечение считает только сервер: предсказанное на клиенте заживление
        // мигало бы туда-обратно на каждом расхождении с сервером.
        if (!_net.IsServer)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WarlockCasteComponent>();
        while (query.MoveNext(out var uid, out var caste))
        {
            if (caste.Caste != WarlockCaste.Matriarch || now < caste.NextTick)
                continue;

            caste.NextTick = now + TimeSpan.FromSeconds(caste.TickInterval);

            if (_mobState.IsDead(uid))
                continue;

            Brood((uid, caste));
        }
    }

    /// <summary>
    /// Выхаживание. Матка стягивает раны всем вокруг, включая себя, и платит за это
    /// собственным здоровьем — иначе отряд рядом с ней стал бы бессмертным.
    /// </summary>
    private void Brood(Entity<WarlockCasteComponent> ent)
    {
        var healed = 0;
        var coords = Transform(ent.Owner).Coordinates;

        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(coords, ent.Comp.BroodRange))
        {
            if (_mobState.IsDead(target.Owner))
                continue;

            // HealEvenly размазывает лечение по тем типам урона, которые у цели
            // действительно есть. Отрицательный урон в отсутствующий тип пропал бы зря.
            _damageable.HealEvenly(target.Owner, -ent.Comp.BroodHeal, origin: ent.Owner);
            healed++;
        }

        if (healed <= 1)
            return;

        // За каждого выхоженного, кроме себя, матка отдаёт своё.
        _damageable.TryChangeDamage(ent.Owner,
            new DamageSpecifier { DamageDict = { ["Cellular"] = ent.Comp.BroodCost * (healed - 1) } },
            ignoreResistances: true,
            origin: ent.Owner);
    }

    #endregion
}
