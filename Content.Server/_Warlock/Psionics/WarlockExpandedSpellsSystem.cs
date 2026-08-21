using System.Linq;
using Content.Server._Warlock.Guilds;
using Content.Server._Warlock.Psionics.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Warlock.Interaction;
using Content.Shared._Warlock.Objectives;
using Content.Shared._Warlock.Pain;
using Content.Shared._Warlock.Psionics.Components;
using Content.Shared._Warlock.Psionics.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Warlock.Psionics;

/// <summary>
/// _Warlock
/// Второе пополнение каталога гримуаров: по пять строк в каждый раздел.
///
/// Держится того же правила, что и первый набор: ни одна новая строка не является
/// улучшенной версией старой. Поэтому здесь нет ни одного «сильного удара» и ни одного
/// «большого лечения» — есть чтение имени сквозь маску, сбитый со всей комнаты огонь,
/// перенос собственных ран на чужое тело, отъём дара у своего же подчинённого
/// и опись чужих карманов.
///
/// Отдельной системой, а не дописыванием в WarlockGrimoireSpellsSystem: тот файл
/// и без того на полтысячи строк, а разбиение по наборам заодно показывает, что
/// добавилось позже.
/// </summary>
public sealed partial class WarlockExpandedSpellsSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private WarlockGuildSystem _guilds = default!;
    [Dependency] private WarlockPainSystem _pain = default!;

    /// <summary>
    /// Восемь сторон света для «Созыва Паствы». Порядок совпадает с секторами
    /// по часовой стрелке от востока.
    /// </summary>
    private static readonly string[] Compass =
    {
        "warlock-dir-e", "warlock-dir-ne", "warlock-dir-n", "warlock-dir-nw",
        "warlock-dir-w", "warlock-dir-sw", "warlock-dir-s", "warlock-dir-se",
    };

    public override void Initialize()
    {
        base.Initialize();

        // --- рядовые
        SubscribeLocalEvent<WarlockMendingHandsEvent>(OnMendingHands);
        SubscribeLocalEvent<WarlockSteadyGripEvent>(OnSteadyGrip);
        SubscribeLocalEvent<WarlockLightStepEvent>(OnLightStep);
        SubscribeLocalEvent<WarlockSecondSightEvent>(OnSecondSight);
        SubscribeLocalEvent<WarlockColdHearthEvent>(OnColdHearth);

        // --- боевые
        SubscribeLocalEvent<WarlockSplinterVolleyEvent>(OnSplinterVolley);
        SubscribeLocalEvent<WarlockIronCrampEvent>(OnIronCramp);
        SubscribeLocalEvent<WarlockBloodDebtEvent>(OnBloodDebt);
        SubscribeLocalEvent<WarlockStaticShroudEvent>(OnStaticShroud);
        SubscribeLocalEvent<WarlockGravebindEvent>(OnGravebind);

        // --- капелланские
        SubscribeLocalEvent<WarlockLastRitesEvent>(OnLastRites);
        SubscribeLocalEvent<WarlockVowOfSilenceEvent>(OnVowOfSilence);
        SubscribeLocalEvent<WarlockBurdenShareEvent>(OnBurdenShare);
        SubscribeLocalEvent<WarlockConsecrateEvent>(OnConsecrate);
        SubscribeLocalEvent<WarlockCallTheFlockEvent>(OnCallTheFlock);

        // --- командирские
        SubscribeLocalEvent<WarlockWritOfSeizureEvent>(OnWritOfSeizure);
        SubscribeLocalEvent<WarlockChainOfCommandEvent>(OnChainOfCommand);
        SubscribeLocalEvent<WarlockExcommunicateEvent>(OnExcommunicate);
        SubscribeLocalEvent<WarlockCensusEvent>(OnCensus);
        SubscribeLocalEvent<WarlockRightOfSearchEvent>(OnRightOfSearch);

        // --- эффекты, пока держатся
        SubscribeLocalEvent<WarlockSteadyGripComponent, WarlockDoAfterSpeedEvent>(OnGripSpeed);
        SubscribeLocalEvent<WarlockLightStepComponent, RefreshMovementSpeedModifiersEvent>(OnStepSpeed);
        SubscribeLocalEvent<WarlockStaticShroudComponent, RefreshMovementSpeedModifiersEvent>(OnShroudSpeed);
        SubscribeLocalEvent<WarlockStaticShroudComponent, DamageModifyEvent>(OnShroudDamage);
        SubscribeLocalEvent<WarlockGravebindComponent, UpdateCanMoveEvent>(OnBindMove);
        SubscribeLocalEvent<WarlockConsecratedComponent, DamageModifyEvent>(OnConsecratedDamage);
        SubscribeLocalEvent<WarlockRalliedComponent, RefreshMovementSpeedModifiersEvent>(OnRalliedSpeed);
        SubscribeLocalEvent<WarlockRalliedComponent, RefreshStaminaCritThresholdEvent>(OnRalliedStamina);
    }

    #region Рядовые

    /// <summary>
    /// Латает железо касанием. Живое отсеивается: это ремонт, а не медицина,
    /// и лечить им людей нельзя принципиально — иначе капелланский раздел не нужен.
    /// </summary>
    private void OnMendingHands(WarlockMendingHandsEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<MobStateComponent>(args.Target) || !HasComp<DamageableComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-mend-flesh"), args.Performer, args.Performer);
            return;
        }

        args.Handled = true;

        _damageable.HealEvenly(args.Target, -args.Repair, origin: args.Performer);
        _popup.PopupEntity(Loc.GetString("warlock-spell-mend"), args.Target, args.Performer, PopupType.Medium);
    }

    private void OnSteadyGrip(WarlockSteadyGripEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var grip = EnsureComp<WarlockSteadyGripComponent>(args.Performer);
        grip.EndAt = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);
        grip.Multiplier = args.Multiplier;

        _popup.PopupEntity(Loc.GetString("warlock-spell-grip"), args.Performer, args.Performer, PopupType.Medium);
    }

    private void OnLightStep(WarlockLightStepEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var step = EnsureComp<WarlockLightStepComponent>(args.Performer);
        step.EndAt = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);
        step.Speed = args.Speed;

        _movement.RefreshMovementSpeedModifiers(args.Performer);
        _popup.PopupEntity(Loc.GetString("warlock-spell-step"), args.Performer, args.Performer, PopupType.Medium);
    }

    /// <summary>
    /// Читает, кто перед вами, минуя маску и капюшон.
    ///
    /// Имя берётся с самой сущности, а не из системы личности: та как раз и умеет
    /// подменять его на «неизвестного». Должность — из разума, поэтому чужая роба
    /// в отчёте не участвует.
    /// </summary>
    private void OnSecondSight(WarlockSecondSightEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var job = _guilds.GetJobOf(args.Target);
        var guild = _guilds.GetGuildOf(args.Target);

        _popup.PopupEntity(
            Loc.GetString("warlock-spell-sight",
                ("name", Name(args.Target)),
                ("job", job is { } j ? j.Id : Loc.GetString("warlock-spell-sight-nojob")),
                ("guild", guild is { } g
                    ? Loc.GetString($"warlock-faction-{g.ToString().ToLowerInvariant()}")
                    : Loc.GetString("warlock-spell-sight-noguild"))),
            args.Performer,
            args.Performer,
            PopupType.Medium);
    }

    /// <summary>
    /// Сбивает огонь со всех вокруг разом. Пожар в помещении это не тушит,
    /// но людей из него вынимает — и в этом вся разница с огнетушителем.
    /// </summary>
    private void OnColdHearth(WarlockColdHearthEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var coords = Transform(args.Performer).Coordinates;

        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(coords, args.Radius))
        {
            _flammable.AdjustFireStacks(target.Owner, -args.Extinguish);

            if (args.Soothe > 0f)
            {
                _damageable.TryChangeDamage(
                    target.Owner,
                    new DamageSpecifier { DamageDict = { ["Heat"] = -args.Soothe } },
                    origin: args.Performer);
            }
        }

        _popup.PopupEntity(Loc.GetString("warlock-spell-hearth"), args.Performer, args.Performer, PopupType.Medium);
    }

    #endregion

    #region Боевые

    /// <summary>
    /// Бьёт всех вокруг, кроме заклинателя, и не разбирает своих. Это не недоработка:
    /// у гильдий нет ни одного заклинания по площади, которое было бы безопасно
    /// применять в строю.
    /// </summary>
    private void OnSplinterVolley(WarlockSplinterVolleyEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var coords = Transform(args.Performer).Coordinates;
        var shards = new DamageSpecifier { DamageDict = { ["Piercing"] = args.Damage } };

        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(coords, args.Radius))
        {
            if (target.Owner == args.Performer)
                continue;

            _damageable.TryChangeDamage(target.Owner, shards, origin: args.Performer);
        }
    }

    private void OnIronCramp(WarlockIronCrampEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _stamina.TakeStaminaDamage(args.Target, args.Stamina, source: args.Performer);
        _stun.TryKnockdown(args.Target, TimeSpan.FromSeconds(args.Knockdown));

        _popup.PopupEntity(Loc.GetString("warlock-spell-cramp"), args.Target, args.Target, PopupType.LargeCaution);
    }

    /// <summary>
    /// Переписывает собственные раны на чужое тело. Переносится ровно столько,
    /// сколько у заклинателя есть: на целом теле заклинание не делает ничего,
    /// и в этом его смысл — оно спасает, а не убивает.
    /// </summary>
    private void OnBloodDebt(WarlockBloodDebtEvent args)
    {
        if (args.Handled)
            return;

        var total = _damageable.GetTotalDamage(args.Performer).Float();

        if (total <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-debt-clean"), args.Performer, args.Performer);
            return;
        }

        args.Handled = true;

        var moved = MathF.Min(args.Amount, total);

        _damageable.HealEvenly(args.Performer, -moved, origin: args.Performer);
        _damageable.TryChangeDamage(
            args.Target,
            new DamageSpecifier { DamageDict = { ["Cellular"] = moved } },
            ignoreResistances: true,
            origin: args.Performer);

        _popup.PopupEntity(Loc.GetString("warlock-spell-debt"), args.Target, args.Target, PopupType.LargeCaution);
    }

    private void OnStaticShroud(WarlockStaticShroudEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var shroud = EnsureComp<WarlockStaticShroudComponent>(args.Performer);
        shroud.EndAt = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);
        shroud.Resist = args.Resist;
        shroud.Slow = args.Slow;

        _movement.RefreshMovementSpeedModifiers(args.Performer);
        _popup.PopupEntity(Loc.GetString("warlock-spell-shroud"), args.Performer, args.Performer, PopupType.Medium);
    }

    private void OnGravebind(WarlockGravebindEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var bind = EnsureComp<WarlockGravebindComponent>(args.Target);
        bind.EndAt = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);

        _movement.RefreshMovementSpeedModifiers(args.Target);
        _popup.PopupEntity(Loc.GetString("warlock-spell-bind"), args.Target, args.Target, PopupType.LargeCaution);
    }

    #endregion

    #region Капелланские

    /// <summary>
    /// Вытаскивает того, кого уже списали, и половину вытащенного забирает себе.
    /// Клеточным: обряд нельзя отработать лазаретом, за него платят до конца смены.
    /// </summary>
    private void OnLastRites(WarlockLastRitesEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _damageable.HealEvenly(args.Target, -args.Heal, origin: args.Performer);
        _damageable.TryChangeDamage(
            args.Performer,
            new DamageSpecifier { DamageDict = { ["Cellular"] = args.Heal * args.Backlash } },
            ignoreResistances: true,
            origin: args.Performer);

        _popup.PopupEntity(Loc.GetString("warlock-spell-rites"), args.Target, args.Target, PopupType.Large);
    }

    /// <summary>
    /// Затыкает чужой дар на время. Отлучённому не возвращает ничего:
    /// у отлучения нет срока, и короткий обет не должен его отменять.
    /// </summary>
    private void OnVowOfSilence(WarlockVowOfSilenceEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        EnsureComp<WarlockPsiSuppressedComponent>(args.Target);

        var vow = EnsureComp<WarlockVowOfSilenceComponent>(args.Target);
        vow.EndAt = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);

        _popup.PopupEntity(Loc.GetString("warlock-spell-vow"), args.Target, args.Target, PopupType.LargeCaution);
    }

    /// <summary>
    /// Забирает чужую боль себе. Не лечит ни на единицу — боль просто меняет владельца,
    /// и капеллан, вытащивший отряд, к концу боя не может держать оружие сам.
    /// </summary>
    private void OnBurdenShare(WarlockBurdenShareEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<WarlockPainComponent>(args.Target, out var pain) || pain.Pain <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-burden-none"), args.Performer, args.Performer);
            return;
        }

        args.Handled = true;

        var moved = pain.Pain * args.Share;

        _pain.AddPain(args.Target, -moved);
        _pain.AddPain(args.Performer, moved);

        _popup.PopupEntity(Loc.GetString("warlock-spell-burden"), args.Target, args.Target, PopupType.Medium);
    }

    private void OnConsecrate(WarlockConsecrateEvent args)
    {
        if (args.Handled)
            return;

        var guild = _guilds.GetGuildOf(args.Performer);

        if (guild is not { } faction)
            return;

        args.Handled = true;

        var coords = Transform(args.Performer).Coordinates;
        var until = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);

        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(coords, args.Radius))
        {
            if (_mobState.IsDead(target.Owner) || _guilds.GetGuildOf(target.Owner) != faction)
                continue;

            var blessing = EnsureComp<WarlockConsecratedComponent>(target.Owner);
            blessing.EndAt = until;
            blessing.Resist = args.Resist;

            _popup.PopupEntity(Loc.GetString("warlock-spell-consecrate"), target.Owner, target.Owner);
        }
    }

    /// <summary>
    /// Каждый свой по гильдии узнаёт, где заклинатель и как далеко.
    /// Расстояние в тайлах и сторона света — ни карты, ни маркера: Союз обходится словами.
    /// </summary>
    private void OnCallTheFlock(WarlockCallTheFlockEvent args)
    {
        if (args.Handled)
            return;

        var guild = _guilds.GetGuildOf(args.Performer);

        if (guild is not { } faction)
            return;

        args.Handled = true;

        var here = _transform.GetMapCoordinates(args.Performer);
        var query = EntityQueryEnumerator<MobStateComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            if (uid == args.Performer || _mobState.IsDead(uid) || _guilds.GetGuildOf(uid) != faction)
                continue;

            var there = _transform.GetMapCoordinates(uid);

            if (there.MapId != here.MapId)
                continue;

            var delta = here.Position - there.Position;
            var sector = (int) MathF.Round(MathF.Atan2(delta.Y, delta.X) / (MathF.PI / 4f));
            var dir = Loc.GetString(Compass[(sector + 8) % 8]);

            _popup.PopupEntity(
                Loc.GetString("warlock-spell-flock",
                    ("dir", dir), ("dist", (int) MathF.Round(delta.Length()))),
                uid,
                uid,
                PopupType.Large);
        }

        _popup.PopupEntity(Loc.GetString("warlock-spell-flock-self"), args.Performer, args.Performer);
    }

    #endregion

    #region Командирские

    /// <summary>
    /// Всё, что цель держит в руках, оказывается на полу. Работает и на своих —
    /// поэтому и лежит в командирском разделе, а не в боевом.
    /// </summary>
    private void OnWritOfSeizure(WarlockWritOfSeizureEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        // Список снимается заранее: TryDrop меняет содержимое рук, и перебирать
        // их напрямую значило бы менять коллекцию во время обхода.
        var held = _hands.EnumerateHeld(args.Target).ToList();

        foreach (var item in held)
        {
            _hands.TryDrop(args.Target, item, checkActionBlocker: false);
        }

        _popup.PopupEntity(
            Loc.GetString(held.Count > 0 ? "warlock-spell-seizure" : "warlock-spell-seizure-empty"),
            args.Target,
            args.Target,
            PopupType.MediumCaution);
    }

    private void OnChainOfCommand(WarlockChainOfCommandEvent args)
    {
        if (args.Handled)
            return;

        var guild = _guilds.GetGuildOf(args.Performer);

        if (guild is not { } faction)
            return;

        args.Handled = true;

        var coords = Transform(args.Performer).Coordinates;
        var until = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);

        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(coords, args.Radius))
        {
            if (_mobState.IsDead(target.Owner) || _guilds.GetGuildOf(target.Owner) != faction)
                continue;

            var rally = EnsureComp<WarlockRalliedComponent>(target.Owner);
            rally.EndAt = until;
            rally.Speed = args.Speed;
            rally.Stamina = args.Stamina;

            _movement.RefreshMovementSpeedModifiers(target.Owner);
            _stamina.RefreshStaminaCritThreshold(target.Owner);

            _popup.PopupEntity(Loc.GetString("warlock-spell-chain"), target.Owner, target.Owner);
        }
    }

    /// <summary>
    /// Снимает дар с подчинённого насовсем — и тем же жестом возвращает.
    /// Срока нет: отсидеть отлучение нельзя, его можно только отслужить.
    /// </summary>
    private void OnExcommunicate(WarlockExcommunicateEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (HasComp<WarlockExcommunicatedComponent>(args.Target))
        {
            RemComp<WarlockExcommunicatedComponent>(args.Target);

            // Обет молчания мог висеть параллельно: возвращаем дар только если
            // на цели больше не висит ничего временного.
            if (!HasComp<WarlockVowOfSilenceComponent>(args.Target))
                RemComp<WarlockPsiSuppressedComponent>(args.Target);

            _popup.PopupEntity(Loc.GetString("warlock-spell-restore"), args.Target, args.Target, PopupType.Large);
            return;
        }

        var mark = EnsureComp<WarlockExcommunicatedComponent>(args.Target);
        mark.By = args.Performer;

        EnsureComp<WarlockPsiSuppressedComponent>(args.Target);

        _popup.PopupEntity(Loc.GetString("warlock-spell-excommunicate"), args.Target, args.Target, PopupType.Large);
    }

    /// <summary>
    /// Сколько кого осталось в живых. Ни имён, ни мест — только счёт, и этого хватает,
    /// чтобы понять, чем кончился штурм на другом конце карты.
    /// </summary>
    private void OnCensus(WarlockCensusEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var counts = new Dictionary<WarlockFaction, int>();
        var query = EntityQueryEnumerator<MobStateComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            if (_mobState.IsDead(uid) || _guilds.GetGuildOf(uid) is not { } faction)
                continue;

            counts[faction] = counts.TryGetValue(faction, out var had) ? had + 1 : 1;
        }

        if (counts.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-census-empty"), args.Performer, args.Performer);
            return;
        }

        var report = string.Join(", ", counts.Select(pair =>
            Loc.GetString("warlock-spell-census-line",
                ("guild", Loc.GetString($"warlock-faction-{pair.Key.ToString().ToLowerInvariant()}")),
                ("count", pair.Value))));

        _popup.PopupEntity(
            Loc.GetString("warlock-spell-census", ("report", report)),
            args.Performer,
            args.Performer,
            PopupType.Medium);
    }

    /// <summary>
    /// Опись всего, что у цели при себе, вплоть до содержимого сумки.
    /// Цель не узнаёт: досмотр командирский, а не таможенный.
    /// </summary>
    private void OnRightOfSearch(WarlockRightOfSearchEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var names = new List<string>();

        foreach (var container in _container.GetAllContainers(args.Target))
        {
            foreach (var item in container.ContainedEntities)
            {
                names.Add(Name(item));
            }
        }

        _popup.PopupEntity(
            names.Count == 0
                ? Loc.GetString("warlock-spell-search-empty")
                : Loc.GetString("warlock-spell-search", ("items", string.Join(", ", names))),
            args.Performer,
            args.Performer,
            PopupType.Medium);
    }

    #endregion

    #region Пока держатся

    private void OnGripSpeed(Entity<WarlockSteadyGripComponent> ent, ref WarlockDoAfterSpeedEvent args)
    {
        args.Multiplier *= ent.Comp.Multiplier;
    }

    private void OnStepSpeed(Entity<WarlockLightStepComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.Speed);
    }

    private void OnShroudSpeed(Entity<WarlockStaticShroudComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.Slow);
    }

    private void OnShroudDamage(Entity<WarlockStaticShroudComponent> ent, ref DamageModifyEvent args)
    {
        args.Damage *= ent.Comp.Resist;
    }

    private void OnBindMove(EntityUid uid, WarlockGravebindComponent comp, UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnConsecratedDamage(Entity<WarlockConsecratedComponent> ent, ref DamageModifyEvent args)
    {
        args.Damage *= ent.Comp.Resist;
    }

    private void OnRalliedSpeed(Entity<WarlockRalliedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.Speed);
    }

    private void OnRalliedStamina(Entity<WarlockRalliedComponent> ent, ref RefreshStaminaCritThresholdEvent args)
    {
        args.Modifier *= ent.Comp.Stamina;
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var grips = EntityQueryEnumerator<WarlockSteadyGripComponent>();
        while (grips.MoveNext(out var uid, out var grip))
        {
            if (now >= grip.EndAt)
                RemCompDeferred<WarlockSteadyGripComponent>(uid);
        }

        var steps = EntityQueryEnumerator<WarlockLightStepComponent>();
        while (steps.MoveNext(out var uid, out var step))
        {
            if (now < step.EndAt)
                continue;

            RemCompDeferred<WarlockLightStepComponent>(uid);
            _movement.RefreshMovementSpeedModifiers(uid);
        }

        var shrouds = EntityQueryEnumerator<WarlockStaticShroudComponent>();
        while (shrouds.MoveNext(out var uid, out var shroud))
        {
            if (now < shroud.EndAt)
                continue;

            RemCompDeferred<WarlockStaticShroudComponent>(uid);
            _movement.RefreshMovementSpeedModifiers(uid);
        }

        var binds = EntityQueryEnumerator<WarlockGravebindComponent>();
        while (binds.MoveNext(out var uid, out var bind))
        {
            if (now < bind.EndAt)
                continue;

            RemCompDeferred<WarlockGravebindComponent>(uid);
            _movement.RefreshMovementSpeedModifiers(uid);
        }

        var blessings = EntityQueryEnumerator<WarlockConsecratedComponent>();
        while (blessings.MoveNext(out var uid, out var blessing))
        {
            if (now >= blessing.EndAt)
                RemCompDeferred<WarlockConsecratedComponent>(uid);
        }

        var rallies = EntityQueryEnumerator<WarlockRalliedComponent>();
        while (rallies.MoveNext(out var uid, out var rally))
        {
            if (now < rally.EndAt)
                continue;

            RemCompDeferred<WarlockRalliedComponent>(uid);
            _movement.RefreshMovementSpeedModifiers(uid);
            _stamina.RefreshStaminaCritThreshold(uid);
        }

        var vows = EntityQueryEnumerator<WarlockVowOfSilenceComponent>();
        while (vows.MoveNext(out var uid, out var vow))
        {
            if (now < vow.EndAt)
                continue;

            RemCompDeferred<WarlockVowOfSilenceComponent>(uid);

            // Отлучённому дар не возвращается: у отлучения срока нет.
            if (!HasComp<WarlockExcommunicatedComponent>(uid))
                RemComp<WarlockPsiSuppressedComponent>(uid);
        }
    }
}
