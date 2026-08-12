using System.Linq;
using Content.Server._Warlock.Artefacts.Components;
using Content.Shared._Warlock.Artefacts.Components;
using Content.Shared._Warlock.Injuries;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Warlock.Artefacts;

/// <summary>
/// _Warlock
/// Артефакты Механтехиона и трёх богов Королевства.
///
/// В отличие от реликвий вымершей расы, эти сделаны живыми культами и оттого злее:
/// почти каждый берёт с владельца плату, и почти от каждого нельзя отказаться,
/// когда уже взял.
/// </summary>
public sealed partial class WarlockDivineArtefactSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private WarlockInjuriesSystem _injuries = default!;

    private static readonly SoundPathSpecifier BreakSound = new("/Audio/Effects/metal_crunch.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockArtefactEaterComponent, AfterInteractEvent>(OnEaterInteract);
        SubscribeLocalEvent<WarlockArtefactEaterComponent, WarlockArtefactEaterDoAfterEvent>(OnEaterFinished);
        SubscribeLocalEvent<WarlockArtefactEaterComponent, ExaminedEvent>(OnEaterExamined);

        SubscribeLocalEvent<WarlockRelentlessComponent, AfterInteractEvent>(OnRelentlessTouched);
        SubscribeLocalEvent<WarlockRelentlessComponent, UseInHandEvent>(OnRelentlessUsed);

        SubscribeLocalEvent<WarlockSubjugationCollarComponent, GotEquippedEvent>(OnCollarEquipped);
        SubscribeLocalEvent<WarlockSubjugationCollarComponent, BeingUnequippedAttemptEvent>(OnCollarUnequipAttempt);

        SubscribeLocalEvent<WarlockAtrakFangComponent, MeleeHitEvent>(OnFangHit);

        SubscribeLocalEvent<WarlockRuzutSeedComponent, UseInHandEvent>(OnSeedUsed);
    }

    #region Гвоздь Механтехиона

    private void OnEaterExamined(Entity<WarlockArtefactEaterComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("warlock-nail-examine", ("uses", ent.Comp.Uses)));
    }

    private void OnEaterInteract(Entity<WarlockArtefactEaterComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!_tag.HasTag(target, ent.Comp.Tag) || target == ent.Owner)
            return;

        args.Handled = true;

        if (ent.Comp.Uses <= 0)
        {
            _popup.PopupEntity(Loc.GetString("warlock-nail-spent"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.Delay,
            new WarlockArtefactEaterDoAfterEvent(),
            ent.Owner,
            target: target,
            used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("warlock-nail-start"), ent, args.User);
    }

    private void OnEaterFinished(Entity<WarlockArtefactEaterComponent> ent, ref WarlockArtefactEaterDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        QueueDel(target);
        ent.Comp.Uses--;

        _audio.PlayPvs(BreakSound, ent);

        // Механтехион не делает ничего даром: за каждый съеденный артефакт ломается кость.
        _injuries.TryAddInjury(args.User, WarlockInjuryType.Fracture, RollLimb());

        _popup.PopupEntity(Loc.GetString("warlock-nail-eaten"), ent, args.User, PopupType.Large);
        _popup.PopupEntity(Loc.GetString("warlock-nail-price"), args.User, args.User, PopupType.LargeCaution);

        if (ent.Comp.Uses > 0)
            return;

        _popup.PopupEntity(Loc.GetString("warlock-nail-crumbles"), ent, args.User, PopupType.MediumCaution);
        QueueDel(ent);
    }

    private WarlockBodyPart RollLimb()
    {
        return _random.Pick(new[]
        {
            WarlockBodyPart.LeftArm,
            WarlockBodyPart.RightArm,
            WarlockBodyPart.LeftLeg,
            WarlockBodyPart.RightLeg,
        });
    }

    #endregion

    #region Неотступный Шестерён

    private void OnRelentlessTouched(Entity<WarlockRelentlessComponent> ent, ref AfterInteractEvent args)
    {
        Bind(ent, args.User);
    }

    private void OnRelentlessUsed(Entity<WarlockRelentlessComponent> ent, ref UseInHandEvent args)
    {
        Bind(ent, args.User);
    }

    /// <summary>
    /// Первое прикосновение решает всё. Перепривязать шестерня нельзя ничем.
    /// </summary>
    private void Bind(Entity<WarlockRelentlessComponent> ent, EntityUid user)
    {
        if (ent.Comp.Bound != null)
            return;

        ent.Comp.Bound = user;
        ent.Comp.NextTick = _timing.CurTime;

        _popup.PopupEntity(Loc.GetString("warlock-cog-bound"), ent, user, PopupType.LargeCaution);
    }

    private void TickRelentless(Entity<WarlockRelentlessComponent> ent)
    {
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.TickInterval);

        if (ent.Comp.Bound is not { } target || TerminatingOrDeleted(target))
            return;

        // Пока шестерён у кого-то в руках или в сумке, дёргаться незачем.
        if (_container.IsEntityInContainer(ent.Owner))
            return;

        var here = _transform.GetMapCoordinates(ent.Owner);
        var there = _transform.GetMapCoordinates(target);

        if (here.MapId != there.MapId)
            return;

        var delta = there.Position - here.Position;

        if (delta.Length() <= ent.Comp.Slack)
            return;

        _throwing.TryThrow(ent.Owner, delta, ent.Comp.Speed, playSound: false);
    }

    #endregion

    #region Ошейник Покорности

    private void OnCollarEquipped(Entity<WarlockSubjugationCollarComponent> ent, ref GotEquippedEvent args)
    {
        if (ent.Comp.Used)
            return;

        ent.Comp.Used = true;

        var victim = args.EquipTarget;

        // Всё лишнее снимается и падает на пол: раб не владеет ничем.
        foreach (var slot in ent.Comp.StrippedSlots)
        {
            _inventory.TryUnequip(victim, slot, silent: true, force: true);
        }

        var uniform = Spawn(ent.Comp.SlaveUniform, Transform(victim).Coordinates);

        // Снимаем прежнюю робу, если она была: у голого TryUnequip вернёт false,
        // и это не повод оставлять раба без формы — одеваем в любом случае.
        _inventory.TryUnequip(victim, "jumpsuit", silent: true, force: true);

        if (!_inventory.TryEquip(victim, uniform, "jumpsuit", silent: true, force: true))
            QueueDel(uniform);

        _injuries.AddBrand(victim, ent.Comp.Brand, WarlockBodyPart.Torso);

        _popup.PopupEntity(Loc.GetString("warlock-collar-locked"), victim, victim, PopupType.LargeCaution);
    }

    private void OnCollarUnequipAttempt(Entity<WarlockSubjugationCollarComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        // Ошейник не снимается вообще ничем и никем. На то он и ошейник.
        args.Cancel();
        args.Reason = "warlock-collar-stuck";
    }

    #endregion

    #region Клык Атрака

    private void OnFangHit(Entity<WarlockAtrakFangComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var hit in args.HitEntities)
        {
            // Поля MobStateComponent закрыты песочницей, состояние спрашиваем у системы.
            if (!_mobState.IsDead(hit))
                continue;

            Feed(ent, args.User);
            break;
        }
    }

    /// <summary>
    /// Клык напился: владелец лечится, клык становится злее.
    /// </summary>
    private void Feed(Entity<WarlockAtrakFangComponent> ent, EntityUid user)
    {
        ent.Comp.Kills++;
        ent.Comp.LastKill = _timing.CurTime;

        // Счётчик убийств читает общая WarlockAttackStrengthSystem, в том числе на клиенте, —
        // без Dirty предсказание урона разъедется с сервером.
        Dirty(ent);

        _damageable.HealEvenly(user, -ent.Comp.HealOnKill, origin: ent.Owner);

        _popup.PopupEntity(Loc.GetString("warlock-fang-fed", ("kills", ent.Comp.Kills)), user, user, PopupType.Medium);
    }

    private void TickFang(Entity<WarlockAtrakFangComponent> ent)
    {
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(15);

        if (_timing.CurTime - ent.Comp.LastKill < TimeSpan.FromSeconds(ent.Comp.HungerDelay))
            return;

        // Голодный клык ест того, кто его держит.
        if (!_container.TryGetContainingContainer((ent.Owner, null, null), out var container))
            return;

        var holder = container.Owner;

        if (!HasComp<MobStateComponent>(holder))
            return;

        _damageable.TryChangeDamage(
            holder,
            new DamageSpecifier { DamageDict = { ["Slash"] = ent.Comp.HungerDamage } },
            origin: ent.Owner);

        _popup.PopupEntity(Loc.GetString("warlock-fang-hungry"), holder, holder, PopupType.MediumCaution);
    }

    #endregion

    #region Семя Рузута

    private void OnSeedUsed(Entity<WarlockRuzutSeedComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var user = args.User;

        _damageable.HealEvenly(user, -ent.Comp.Heal, origin: ent.Owner);
        _injuries.HealAll(user);

        // Рузут ещё и бог обмана: он всегда отращивает что-нибудь лишнее.
        if (ent.Comp.Souvenirs.Count > 0)
        {
            var souvenir = _random.Pick(ent.Comp.Souvenirs);
            var part = souvenir == WarlockInjuryType.MissingTooth ? WarlockBodyPart.Head : RollLimb();

            _injuries.TryAddInjury(user, souvenir, part);
        }

        _popup.PopupEntity(Loc.GetString("warlock-seed-used"), user, user, PopupType.Medium);
        QueueDel(ent);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var cogs = EntityQueryEnumerator<WarlockRelentlessComponent>();
        while (cogs.MoveNext(out var uid, out var cog))
        {
            if (cog.Bound == null || now < cog.NextTick)
                continue;

            TickRelentless((uid, cog));
        }

        var fangs = EntityQueryEnumerator<WarlockAtrakFangComponent>();
        while (fangs.MoveNext(out var uid, out var fang))
        {
            if (now < fang.NextTick)
                continue;

            TickFang((uid, fang));
        }

        var worms = EntityQueryEnumerator<WarlockRepairWormComponent>();
        while (worms.MoveNext(out var uid, out var worm))
        {
            if (now < worm.NextTick)
                continue;

            TickWorm((uid, worm));
        }
    }

    /// <summary>
    /// Ремонтный червь чинит носителя, а если чинить нечего — сам делает работу себе.
    /// </summary>
    private void TickWorm(Entity<WarlockRepairWormComponent> ent)
    {
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.TickInterval);

        if (!_container.TryGetContainingContainer((ent.Owner, null, null), out var container))
            return;

        var holder = container.Owner;

        if (!TryComp<WarlockInjuriesComponent>(holder, out var injuries))
            return;

        var healable = injuries.Injuries.Count(i =>
            i.Type is WarlockInjuryType.Abrasion or WarlockInjuryType.Bruise or WarlockInjuryType.Fracture);

        if (healable > 0)
        {
            ent.Comp.IdleTicks = 0;

            _injuries.TryRemoveInjury(holder, WarlockInjuryType.Abrasion);
            _injuries.TryRemoveInjury(holder, WarlockInjuryType.Bruise);
            _injuries.TryRemoveInjury(holder, WarlockInjuryType.Fracture);

            _popup.PopupEntity(Loc.GetString("warlock-worm-repairs"), holder, holder);
            return;
        }

        ent.Comp.IdleTicks++;

        if (ent.Comp.IdleTicks < ent.Comp.IdleTicksBeforeHarm)
            return;

        ent.Comp.IdleTicks = 0;

        // Механтехиону нужен ремонт, а не здоровье. Нет поломки — червь её сделает.
        _injuries.TryAddInjury(holder, WarlockInjuryType.Fracture, RollLimb());

        _popup.PopupEntity(Loc.GetString("warlock-worm-breaks"), holder, holder, PopupType.LargeCaution);
    }
}
