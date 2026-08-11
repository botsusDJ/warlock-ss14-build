using System.Numerics;
using Content.Server._Warlock.Psionics.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._Warlock.Psionics;
using Content.Shared._Warlock.Psionics.Components;
using Content.Shared._Warlock.Psionics.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Hands;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Warlock.Psionics;

/// <summary>
/// _Warlock
/// Второй круг псайкерства техномагов — ритуалы, у которых цена выше, чем запас энергии.
///
/// Первые пять заклинаний расы были тактическими: потратил резерв, получил эффект.
/// Здесь всё иначе. Часть ритуалов калечит самого кастующего навсегда, часть работает
/// как ловушка, которую нельзя отозвать, а «Погребальный Костёр» и вовсе не различает,
/// кто внутри области, потому что источник жара — сам техномаг.
/// </summary>
public sealed partial class WarlockRitualsSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private WarlockPsionicsSystem _psionics = default!;

    private static readonly ProtoId<TagPrototype> ArtefactTag = "WarlockArtefact";

    /// <summary>
    /// Слоты, которые перекраивает «Личина Брата», и что в них подставляется.
    /// </summary>
    private static readonly (string Slot, EntProtoId Proto)[] DisguiseSlots =
    {
        ("jumpsuit", "WarlockUniformBrotherhood"),
        ("outerClothing", "WarlockOuterBrotherhoodArmor"),
        ("head", "WarlockHeadBrotherhoodHelmet"),
        ("mask", "WarlockMaskBrotherhood"),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockMirrorEchoEvent>(OnMirrorEcho);
        SubscribeLocalEvent<WarlockCursedGraspEvent>(OnCursedGrasp);
        SubscribeLocalEvent<WarlockWardOfHurlingEvent>(OnWardOfHurling);
        SubscribeLocalEvent<WarlockWardOfBlightEvent>(OnWardOfBlight);
        SubscribeLocalEvent<WarlockRiteOfBulwarkEvent>(OnRiteOfBulwark);
        SubscribeLocalEvent<WarlockRelicScentEvent>(OnRelicScent);
        SubscribeLocalEvent<WarlockWitheringTouchEvent>(OnWitheringTouch);
        SubscribeLocalEvent<WarlockPyreAuraEvent>(OnPyreAura);
        SubscribeLocalEvent<WarlockFalseBrotherEvent>(OnFalseBrother);
        SubscribeLocalEvent<WarlockGiftHarvestEvent>(OnGiftHarvest);

        SubscribeLocalEvent<WarlockCursedGraspComponent, DidEquipHandEvent>(OnCursedGraspPickup);
        SubscribeLocalEvent<WarlockRuneComponent, StepTriggeredOffEvent>(OnRuneStepped);
        SubscribeLocalEvent<WarlockHollowedComponent, RefreshStaminaCritThresholdEvent>(OnHollowedStamina);
        SubscribeLocalEvent<WarlockFalseBrotherComponent, ComponentShutdown>(OnDisguiseShutdown);
    }

    #region 1. Эхо-Копия

    private void OnMirrorEcho(WarlockMirrorEchoEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var performer = args.Performer;
        var coords = Transform(performer).Coordinates;
        var copied = 0;

        foreach (var item in _lookup.GetEntitiesInRange<ItemComponent>(coords, args.Radius))
        {
            if (copied >= args.MaxCopies)
                break;

            // Живое, закреплённое и лежащее в контейнерах не копируем.
            // Пол и стены сюда и так не попадают — у них нет ItemComponent.
            if (item.Owner == performer || HasComp<MobStateComponent>(item.Owner))
                continue;

            if (_container.IsEntityInContainer(item.Owner) || Transform(item.Owner).Anchored)
                continue;

            if (MetaData(item.Owner).EntityPrototype?.ID is not { } proto)
                continue;

            Spawn(proto, Transform(item.Owner).Coordinates);
            copied++;
        }

        _audio.PlayPvs(args.Sound, performer);
        _popup.PopupEntity(Loc.GetString("warlock-spell-echo-copy", ("count", copied)), performer, performer);
    }

    #endregion

    #region 2. Проклятая Хватка

    private void OnCursedGrasp(WarlockCursedGraspEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var grasp = EnsureComp<WarlockCursedGraspComponent>(args.Performer);
        grasp.EndAt = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);
        grasp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(1);

        _audio.PlayPvs(args.Sound, args.Performer);
        _popup.PopupEntity(Loc.GetString("warlock-spell-grasp-start"), args.Performer, args.Performer, PopupType.LargeCaution);
    }

    private void OnCursedGraspPickup(Entity<WarlockCursedGraspComponent> ent, ref DidEquipHandEvent args)
    {
        var item = args.Equipped;
        if (TerminatingOrDeleted(item))
            return;

        var coords = _transform.GetMapCoordinates(item);

        // Предмет разрывает раньше, чем он успевает оказаться полезным.
        QueueDel(item);

        _explosion.QueueExplosion(
            coords,
            ent.Comp.ExplosionType,
            ent.Comp.ExplosionIntensity,
            2f,
            ent.Comp.ExplosionIntensity,
            ent.Owner,
            maxTileBreak: 0,
            canCreateVacuum: false);

        _popup.PopupEntity(Loc.GetString("warlock-spell-grasp-burst"), ent.Owner, ent.Owner, PopupType.MediumCaution);
    }

    private void TickCursedGrasp(Entity<WarlockCursedGraspComponent> ent)
    {
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(1);

        var heal = new DamageSpecifier
        {
            DamageDict =
            {
                ["Blunt"] = -ent.Comp.HealPerTick,
                ["Slash"] = -ent.Comp.HealPerTick,
                ["Piercing"] = -ent.Comp.HealPerTick,
                ["Heat"] = -ent.Comp.HealPerTick,
            },
        };

        _damageable.TryChangeDamage(ent.Owner, heal, true, origin: ent.Owner);
    }

    #endregion

    #region 3-4. Печати

    private void OnWardOfHurling(WarlockWardOfHurlingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        PlaceRune(args.Rune, args.Target, args.Performer, args.Sound);
    }

    private void OnWardOfBlight(WarlockWardOfBlightEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        PlaceRune(args.Rune, args.Target, args.Performer, args.Sound);
    }

    private void PlaceRune(EntProtoId proto, EntityCoordinates coords, EntityUid performer, SoundSpecifier? sound)
    {
        var rune = Spawn(proto, coords);

        if (TryComp<WarlockRuneComponent>(rune, out var runeComp))
            runeComp.Caster = performer;

        _audio.PlayPvs(sound, performer);
        _popup.PopupEntity(Loc.GetString("warlock-spell-ward-placed"), performer, performer);
    }

    private void OnRuneStepped(Entity<WarlockRuneComponent> ent, ref StepTriggeredOffEvent args)
    {
        var tripper = args.Tripper;

        // Своя печать хозяина не трогает — иначе ловушку невозможно поставить и уйти.
        if (ent.Comp.Caster == tripper)
            return;

        switch (ent.Comp.Effect)
        {
            case WarlockRuneEffect.Hurling:
            {
                var origin = _transform.GetWorldPosition(ent);
                var direction = _transform.GetWorldPosition(tripper) - origin;

                if (direction.LengthSquared() < 0.01f)
                    direction = new Vector2(1f, 0f);

                _throwing.TryThrow(
                    tripper,
                    direction.Normalized() * ent.Comp.ThrowDistance,
                    ent.Comp.ThrowStrength,
                    ent.Owner);

                _popup.PopupEntity(Loc.GetString("warlock-rune-hurled"), tripper, tripper, PopupType.LargeCaution);
                break;
            }

            case WarlockRuneEffect.Blight:
            {
                var coords = _transform.GetMapCoordinates(ent);

                _explosion.QueueExplosion(
                    coords,
                    ent.Comp.ExplosionType,
                    ent.Comp.ExplosionIntensity,
                    2f,
                    ent.Comp.ExplosionIntensity,
                    ent.Comp.Caster,
                    maxTileBreak: 0);

                var poison = new DamageSpecifier { DamageDict = { ["Poison"] = ent.Comp.PoisonDamage } };

                foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(ent).Coordinates, ent.Comp.PoisonRadius))
                {
                    _damageable.TryChangeDamage(mob.Owner, poison, origin: ent.Comp.Caster);
                }

                _popup.PopupEntity(Loc.GetString("warlock-rune-blighted"), tripper, tripper, PopupType.LargeCaution);
                break;
            }
        }

        QueueDel(ent);
    }

    #endregion

    #region 5. Литания Укрепления

    private void OnRiteOfBulwark(WarlockRiteOfBulwarkEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;
        var target = args.Target;

        // Укреплять можно только неживую закреплённую технику и только один раз.
        if (HasComp<MobStateComponent>(target) || !Transform(target).Anchored || !HasComp<DamageableComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-bulwark-invalid"), performer, performer, PopupType.MediumCaution);
            return;
        }

        if (HasComp<WarlockBulwarkedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-bulwark-already"), performer, performer, PopupType.MediumCaution);
            return;
        }

        args.Handled = true;

        EnsureComp<WarlockBulwarkedComponent>(target);
        _damageable.SetDamageModifierSetId(target, args.Modifiers);

        PayBulwarkPrice(performer, args);

        _audio.PlayPvs(args.Sound, target);
        _popup.PopupEntity(Loc.GetString("warlock-spell-bulwark-done"), performer, performer, PopupType.Large);
    }

    /// <summary>
    /// Плата за укрепление: пороги крита и смерти сдвигаются вниз навсегда,
    /// а порог оглушения от усталости падает множителем.
    /// </summary>
    private void PayBulwarkPrice(EntityUid performer, WarlockRiteOfBulwarkEvent args)
    {
        var hollowed = EnsureComp<WarlockHollowedComponent>(performer);
        hollowed.Rites++;
        hollowed.StaminaMultiplier *= args.StaminaPenalty;

        // Порог всегда оставляем выше нуля, иначе ритуал превращается в кнопку самоубийства.
        foreach (var state in new[] { MobState.Critical, MobState.Dead })
        {
            if (!_thresholds.TryGetThresholdForState(performer, state, out var threshold))
                continue;

            var reduced = threshold.Value - args.HealthCost;
            if (reduced < 10)
                reduced = 10;

            _thresholds.SetMobStateThreshold(performer, reduced, state);
        }

        _stamina.RefreshStaminaCritThreshold(performer);

        _popup.PopupEntity(Loc.GetString("warlock-spell-bulwark-price"), performer, performer, PopupType.LargeCaution);
    }

    private void OnHollowedStamina(Entity<WarlockHollowedComponent> ent, ref RefreshStaminaCritThresholdEvent args)
    {
        args.Modifier *= ent.Comp.StaminaMultiplier;
    }

    #endregion

    #region 6. Чутьё Реликвий

    private void OnRelicScent(WarlockRelicScentEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var scent = EnsureComp<WarlockRelicScentComponent>(args.Performer);
        scent.EndAt = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);
        scent.NextTick = TimeSpan.Zero;
        scent.Radius = args.Radius;

        _audio.PlayPvs(args.Sound, args.Performer);
        _popup.PopupEntity(Loc.GetString("warlock-spell-scent-start"), args.Performer, args.Performer);
    }

    private void TickRelicScent(Entity<WarlockRelicScentComponent> ent)
    {
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.TickInterval);

        var origin = _transform.GetWorldPosition(ent);

        EntityUid? closest = null;
        var closestDistance = float.MaxValue;

        foreach (var candidate in _lookup.GetEntitiesInRange<TagComponent>(Transform(ent).Coordinates, ent.Comp.Radius))
        {
            if (!_tag.HasTag(candidate.Comp, ArtefactTag))
                continue;

            if (candidate.Owner == ent.Owner)
                continue;

            var distance = (_transform.GetWorldPosition(candidate.Owner) - origin).Length();
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closest = candidate.Owner;
        }

        if (closest is not { } target)
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-scent-nothing"), ent.Owner, ent.Owner);
            return;
        }

        var direction = GetDirectionName(_transform.GetWorldPosition(target) - origin);

        _popup.PopupEntity(
            Loc.GetString("warlock-spell-scent-found",
                ("direction", Loc.GetString(direction)),
                ("distance", (int) closestDistance)),
            ent.Owner,
            ent.Owner);
    }

    /// <summary>
    /// Переводит вектор в человеческое «на северо-восток».
    /// </summary>
    private static string GetDirectionName(Vector2 direction)
    {
        var angle = MathF.Atan2(direction.Y, direction.X);
        var octant = (int) MathF.Round(angle / (MathF.PI / 4f));

        // Считаем октант отдельной переменной: если написать "% 8 switch", парсер отдаст
        // правую восьмёрку самому switch-выражению и попытается взять остаток от строки.
        var index = ((octant % 8) + 8) % 8;

        return index switch
        {
            0 => "warlock-direction-east",
            1 => "warlock-direction-northeast",
            2 => "warlock-direction-north",
            3 => "warlock-direction-northwest",
            4 => "warlock-direction-west",
            5 => "warlock-direction-southwest",
            6 => "warlock-direction-south",
            _ => "warlock-direction-southeast",
        };
    }

    #endregion

    #region 7. Иссушающее Касание

    private void OnWitheringTouch(WarlockWitheringTouchEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;
        var target = args.Target;

        if (target == performer || !TryComp<StaminaComponent>(target, out var targetStamina))
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-wither-invalid"), performer, performer, PopupType.MediumCaution);
            return;
        }

        args.Handled = true;

        // Осушаем цель до самого оглушения...
        var drain = targetStamina.CritThreshold - targetStamina.StaminaDamage;
        if (drain > 0)
            _stamina.TakeStaminaDamage(target, drain, targetStamina, source: performer);

        // ...и заливаем вытянутое в себя. Отрицательное значение система зажимает в ноль сама.
        if (TryComp<StaminaComponent>(performer, out var ownStamina))
            _stamina.TakeStaminaDamage(performer, -ownStamina.StaminaDamage, ownStamina, visual: false);

        _audio.PlayPvs(args.Sound, target);
        _popup.PopupEntity(Loc.GetString("warlock-spell-wither-target"), target, target, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("warlock-spell-wither-caster"), performer, performer);
    }

    #endregion

    #region 8. Погребальный Костёр

    private void OnPyreAura(WarlockPyreAuraEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var aura = EnsureComp<WarlockPyreAuraComponent>(args.Performer);
        aura.EndAt = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);
        aura.NextTick = TimeSpan.Zero;
        aura.Radius = args.Radius;
        aura.Temperature = args.Temperature;

        _audio.PlayPvs(args.Sound, args.Performer);
        _popup.PopupEntity(Loc.GetString("warlock-spell-pyre-start"), args.Performer, args.Performer, PopupType.LargeCaution);
    }

    private void TickPyreAura(Entity<WarlockPyreAuraComponent> ent)
    {
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.TickInterval);

        var xform = Transform(ent);
        var radius = (int) MathF.Ceiling(ent.Comp.Radius);

        // Разогреваем сам воздух — область считается от текущего положения носителя,
        // поэтому она честно таскается за ним, а не остаётся там, где был каст.
        if (xform.GridUid is { } grid)
        {
            var center = _transform.GetGridOrMapTilePosition(ent.Owner, xform);

            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    var tile = new Vector2i(center.X + x, center.Y + y);
                    var mixture = _atmosphere.GetTileMixture(grid, xform.MapUid, tile, true);

                    if (mixture == null)
                        continue;

                    if (mixture.Temperature < ent.Comp.Temperature)
                        mixture.Temperature = ent.Comp.Temperature;
                }
            }
        }

        // И жарим всё живое, включая самого носителя. В этом весь смысл ритуала.
        var burn = new DamageSpecifier { DamageDict = { ["Heat"] = ent.Comp.HeatPerTick } };

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, ent.Comp.Radius))
        {
            _damageable.TryChangeDamage(mob.Owner, burn, origin: ent.Owner);
            _flammable.AdjustFireStacks(mob.Owner, 2f, ignite: true);
        }
    }

    #endregion

    #region 9. Личина Брата

    private void OnFalseBrother(WarlockFalseBrotherEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;

        if (HasComp<WarlockFalseBrotherComponent>(performer))
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-disguise-already"), performer, performer, PopupType.MediumCaution);
            return;
        }

        if (!HasComp<InventoryComponent>(performer))
            return;

        args.Handled = true;

        var disguise = EnsureComp<WarlockFalseBrotherComponent>(performer);
        disguise.EndAt = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);

        var stash = _container.EnsureContainer<Container>(performer, disguise.StashId);

        foreach (var (slot, proto) in DisguiseSlots)
        {
            // Своё снимаем и прячем «между», чтобы вернуть в целости.
            if (_inventory.TryUnequip(performer, slot, out var removed, silent: true, force: true)
                && removed is { } item
                && _container.Insert(item, stash))
            {
                disguise.Stashed[slot] = item;
            }

            var fake = Spawn(proto, Transform(performer).Coordinates);

            if (_inventory.TryEquip(performer, fake, slot, silent: true, force: true))
                disguise.Disguise[slot] = fake;
            else
                QueueDel(fake);
        }

        _audio.PlayPvs(args.Sound, performer);
        _popup.PopupEntity(Loc.GetString("warlock-spell-disguise-start"), performer, performer);
    }

    private void OnDisguiseShutdown(Entity<WarlockFalseBrotherComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return;

        // Личина исчезает вместе с ритуалом, а не падает на пол.
        foreach (var (slot, fake) in ent.Comp.Disguise)
        {
            _inventory.TryUnequip(ent.Owner, slot, out _, silent: true, force: true);
            QueueDel(fake);
        }

        foreach (var (slot, item) in ent.Comp.Stashed)
        {
            if (TerminatingOrDeleted(item))
                continue;

            // Достаём из «между» и возвращаем на место.
            _container.RemoveEntity(ent.Owner, item, force: true);
            _inventory.TryEquip(ent.Owner, item, slot, silent: true, force: true);
        }

        ent.Comp.Disguise.Clear();
        ent.Comp.Stashed.Clear();

        _popup.PopupEntity(Loc.GetString("warlock-spell-disguise-end"), ent.Owner, ent.Owner);
    }

    #endregion

    #region 10. Жатва Дара

    private void OnGiftHarvest(WarlockGiftHarvestEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;
        var target = args.Target;

        if (!TryComp<WarlockPsionicComponent>(target, out var donor) || !_mobState.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-harvest-invalid"), performer, performer, PopupType.MediumCaution);
            return;
        }

        args.Handled = true;

        var harvested = _psionics.RestoreEnergy(performer, donor.MaxEnergy);

        // Донор выжат досуха: дара в нём больше нет.
        RemComp<WarlockPsionicComponent>(target);

        _audio.PlayPvs(args.Sound, target);

        if (_random.Prob(args.DisintegrationChance))
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-harvest-disintegrate"), performer, performer, PopupType.LargeCaution);
            _gibbing.Gib(performer, user: performer);
            return;
        }

        _popup.PopupEntity(
            Loc.GetString("warlock-spell-harvest-done", ("amount", harvested.Int())),
            performer,
            performer,
            PopupType.Medium);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var grasps = EntityQueryEnumerator<WarlockCursedGraspComponent>();
        while (grasps.MoveNext(out var uid, out var grasp))
        {
            if (now >= grasp.EndAt)
            {
                RemCompDeferred<WarlockCursedGraspComponent>(uid);
                continue;
            }

            if (now >= grasp.NextTick)
                TickCursedGrasp((uid, grasp));
        }

        var scents = EntityQueryEnumerator<WarlockRelicScentComponent>();
        while (scents.MoveNext(out var uid, out var scent))
        {
            if (now >= scent.EndAt)
            {
                RemCompDeferred<WarlockRelicScentComponent>(uid);
                continue;
            }

            if (now >= scent.NextTick)
                TickRelicScent((uid, scent));
        }

        var auras = EntityQueryEnumerator<WarlockPyreAuraComponent>();
        while (auras.MoveNext(out var uid, out var aura))
        {
            if (now >= aura.EndAt)
            {
                RemCompDeferred<WarlockPyreAuraComponent>(uid);
                continue;
            }

            if (now >= aura.NextTick)
                TickPyreAura((uid, aura));
        }

        var disguises = EntityQueryEnumerator<WarlockFalseBrotherComponent>();
        while (disguises.MoveNext(out var uid, out var disguise))
        {
            if (now >= disguise.EndAt)
                RemCompDeferred<WarlockFalseBrotherComponent>(uid);
        }
    }
}
