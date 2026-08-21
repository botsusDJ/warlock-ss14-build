using System.Linq;
using Content.Shared._Warlock.Mechs;
using Content.Shared.DoAfter;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Warlock.Mechs;

/// <summary>
/// _Warlock
/// Мехи Братства Стали: экипаж, посты, неповоротливость, смазка.
///
/// Слой поверх ванильной механики. Ванильное здесь всё, что уже работает: пилот,
/// батарея, слоты оружия, интерфейс. Своё — четыре вещи, каждая из которых делает
/// мех машиной, а не бронированным человеком.
///
/// ЭКИПАЖ. Мест в кабине два: рычаги и прицелы. Второе место — свой контейнер, потому
/// что ванильный слот пилота один и рассчитан на одного. Стрелок стреляет ровно так же,
/// как стрелял бы пилот: через ретрансляцию взаимодействия на мех. Никакого отдельного
/// пути огня заводить не пришлось.
///
/// ПОСТЫ. Одиночка не достаёт до рычагов и прицелов одновременно, и это выражено
/// не запретом, а самой ретрансляцией: за рычагами её нет — стрелять нечем; за прицелами
/// она есть, но мех стоит. Со стрелком на борту водитель водит, стрелок стреляет,
/// и переключаться не нужно никому.
///
/// НЕПОВОРОТЛИВОСТЬ. Разворот стоит времени, а не скорости. Медленный мех водится как
/// тяжёлый человек; мех, который надо разворачивать, приходится выводить на позицию
/// заранее — а это уже другая игра.
///
/// СМАЗКА. Не топливо: на нуле рама не встаёт, она начинает есть себя. Сухая рама
/// медленнее, разворачивается дольше и стирает собственные узлы, так что экономия
/// на смазке оплачивается заменой ног.
/// </summary>
public sealed partial class WarlockMechSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedMechSystem _mech = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleSystem _vehicle = default!;
    [Dependency] private WarlockMechPartSystem _parts = default!;

    private static readonly SoundPathSpecifier TurnSound = new("/Audio/Mecha/mechmove03.ogg");
    private static readonly SoundPathSpecifier PourSound = new("/Audio/Effects/Fluids/glug.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockMechComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WarlockMechComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<WarlockMechComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);

        SubscribeLocalEvent<WarlockMechComponent, VehicleCanRunEvent>(OnCanRun);
        SubscribeLocalEvent<WarlockMechComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<WarlockMechComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<WarlockMechComponent, VehicleOperatorSetEvent>(OnOperatorSet);

        SubscribeLocalEvent<WarlockMechComponent, WarlockMechStationEvent>(OnStationSwitch);
        SubscribeLocalEvent<WarlockMechComponent, WarlockMechGunnerCycleEvent>(OnGunnerCycle);
        SubscribeLocalEvent<WarlockMechComponent, WarlockMechGunnerDoAfterEvent>(OnGunnerDoAfter);

        SubscribeLocalEvent<WarlockMechGreaseComponent, AfterInteractEvent>(OnGrease);
        SubscribeLocalEvent<WarlockMechComponent, WarlockMechGreaseDoAfterEvent>(OnGreaseDoAfter);
    }

    private void OnStartup(Entity<WarlockMechComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.GunnerSlot = _container.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.GunnerSlotId);
        ent.Comp.PartContainer = _container.EnsureContainer<Container>(ent.Owner, ent.Comp.PartContainerId);
        ent.Comp.NextTick = _timing.CurTime;
    }

    #region Осмотр

    private void OnExamined(Entity<WarlockMechComponent> ent, ref ExaminedEvent args)
    {
        var lube = (int) MathF.Round(ent.Comp.Lubricant / ent.Comp.MaxLubricant * 100f);
        args.PushMarkup(Loc.GetString("warlock-mech-examine-lube", ("pct", lube)));

        var missing = MissingSlots(ent).ToList();

        if (missing.Count > 0)
        {
            args.PushMarkup(Loc.GetString("warlock-mech-examine-missing",
                ("slots", string.Join(", ", missing.Select(s => Loc.GetString($"warlock-mech-slot-{s}"))))));
        }

        var wear = (int) MathF.Round(_parts.AverageWear(ent.Comp.PartContainer) * 100f);

        if (wear > 0)
            args.PushMarkup(Loc.GetString("warlock-mech-examine-wear", ("pct", wear)));

        args.PushMarkup(Loc.GetString(HasGunner(ent)
            ? "warlock-mech-examine-crewed"
            : ent.Comp.Station == WarlockMechStation.Gun
                ? "warlock-mech-examine-solo-gun"
                : "warlock-mech-examine-solo-drive"));
    }

    #endregion

    #region Экипаж

    private bool HasGunner(Entity<WarlockMechComponent> ent)
    {
        return ent.Comp.GunnerSlot.ContainedEntity != null;
    }

    private void OnVerbs(Entity<WarlockMechComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        // Вылезти со второго места может сам стрелок, посадить туда — кто угодно снаружи.
        if (ent.Comp.GunnerSlot.ContainedEntity == user)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("warlock-mech-verb-gunner-out"),
                Act = () => EjectGunner(ent),
            });
            return;
        }

        // Узлы снимаются отсюда же: своего меню у них нет, а пара
        // «WarlockMechComponent + GetVerbsEvent» допускает одну подписку на билд.
        _parts.AddRemovalVerbs(ent.Comp.PartContainer, ent.Owner, args);

        if (HasGunner(ent) || !HasComp<MobStateComponent>(user))
            return;

        var comp = ent.Comp;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("warlock-mech-verb-gunner-in"),
            Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user,
                comp.GunnerEntryDelay, new WarlockMechGunnerDoAfterEvent(), ent.Owner, ent.Owner)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
            }),
        });
    }

    private void OnGunnerDoAfter(Entity<WarlockMechComponent> ent, ref WarlockMechGunnerDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || HasGunner(ent))
            return;

        args.Handled = true;

        if (!_container.Insert(args.User, ent.Comp.GunnerSlot))
            return;

        // Стрелок стреляет тем же путём, что и пилот: его взаимодействия
        // ретранслируются на мех, а ванильный мех отдаёт их выбранному орудию.
        var relay = EnsureComp<InteractionRelayComponent>(args.User);
        _interaction.SetRelay(args.User, ent.Owner, relay);

        _actions.AddAction(args.User, ref ent.Comp.GunnerCycleActionEntity,
            ent.Comp.GunnerCycleAction, ent.Owner);

        // Со стрелком на борту водителю больше не надо выбирать между ходом и огнём:
        // он всегда за рычагами.
        SetStation(ent, WarlockMechStation.Drive);

        _popup.PopupEntity(Loc.GetString("warlock-mech-gunner-in"), ent.Owner, args.User);
    }

    private void EjectGunner(Entity<WarlockMechComponent> ent)
    {
        if (ent.Comp.GunnerSlot.ContainedEntity is not { } gunner)
            return;

        _container.Remove(gunner, ent.Comp.GunnerSlot);
        RemComp<InteractionRelayComponent>(gunner);
        _actions.RemoveProvidedActions(gunner, ent.Owner);

        _popup.PopupEntity(Loc.GetString("warlock-mech-gunner-out"), ent.Owner, gunner);

        // Водитель снова один и снова выбирает.
        ApplyStation(ent);
    }

    private void OnGunnerCycle(Entity<WarlockMechComponent> ent, ref WarlockMechGunnerCycleEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _mech.CycleEquipment(ent.Owner);
    }

    /// <summary>
    /// Водитель садится или встаёт. Пост при этом сбрасывается на рычаги:
    /// вылезший из-за прицелов мех должен уметь уехать.
    /// </summary>
    private void OnOperatorSet(Entity<WarlockMechComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (args.NewOperator is not { } driver)
            return;

        ent.Comp.Station = WarlockMechStation.Drive;
        _actions.AddAction(driver, ref ent.Comp.StationActionEntity, ent.Comp.StationAction, ent.Owner);
        ApplyStation(ent);
    }

    #endregion

    #region Посты

    private void OnStationSwitch(Entity<WarlockMechComponent> ent, ref WarlockMechStationEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (HasGunner(ent))
        {
            _popup.PopupEntity(Loc.GetString("warlock-mech-station-crewed"), ent.Owner, args.Performer);
            return;
        }

        SetStation(ent, ent.Comp.Station == WarlockMechStation.Drive
            ? WarlockMechStation.Gun
            : WarlockMechStation.Drive);

        _popup.PopupEntity(
            Loc.GetString(ent.Comp.Station == WarlockMechStation.Gun
                ? "warlock-mech-station-gun"
                : "warlock-mech-station-drive"),
            ent.Owner,
            args.Performer,
            PopupType.Medium);
    }

    private void SetStation(Entity<WarlockMechComponent> ent, WarlockMechStation station)
    {
        ent.Comp.Station = station;
        Dirty(ent);
        ApplyStation(ent);
    }

    /// <summary>
    /// Приводит ретрансляцию водителя в соответствие посту.
    ///
    /// Запрет на стрельбу выражен отсутствием ретрансляции, а не отдельной проверкой:
    /// ванильный мех отдаёт выстрел орудию именно через неё, и снять её — самый честный
    /// способ сказать «руки заняты рычагами».
    /// </summary>
    private void ApplyStation(Entity<WarlockMechComponent> ent)
    {
        if (!_vehicle.TryGetOperator(ent.Owner, out var op))
            return;

        var driver = op.Value.Owner;
        var manned = HasGunner(ent);

        // Со стрелком водитель никогда не стреляет: за прицелами сидит другой.
        var shoots = !manned && ent.Comp.Station == WarlockMechStation.Gun;

        if (shoots)
        {
            var relay = EnsureComp<InteractionRelayComponent>(driver);
            _interaction.SetRelay(driver, ent.Owner, relay);
        }
        else
        {
            RemComp<InteractionRelayComponent>(driver);
        }
    }

    #endregion

    #region Ход

    private void OnCanRun(Entity<WarlockMechComponent> ent, ref VehicleCanRunEvent args)
    {
        // За прицелами рама стоит: одиночка не достаёт до рычагов.
        if (!HasGunner(ent) && ent.Comp.Station == WarlockMechStation.Gun)
        {
            args.CanRun = false;
            return;
        }

        // Доворачивается.
        if (_timing.CurTime < ent.Comp.TurnUntil)
        {
            args.CanRun = false;
            return;
        }

        // Нет ступни — нет хода. Это и есть смысл разборной ноги.
        if (MissingSlots(ent).Any())
            args.CanRun = false;
    }

    private void OnRefreshSpeed(Entity<WarlockMechComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var factor = 1f;

        if (ent.Comp.Lubricant / ent.Comp.MaxLubricant < ent.Comp.DryBelow)
            factor *= ent.Comp.DrySpeed;

        // Изношенные узлы тормозят раму пропорционально среднему износу.
        factor *= 1f - _parts.AverageWear(ent.Comp.PartContainer) * ent.Comp.WearSlowdown;

        args.ModifySpeed(MathF.Max(factor, 0.2f));
    }

    /// <summary>
    /// Разворот. Считается по направлению фактического движения рамы: своего ввода
    /// у нас нет, а скорость есть и она честно показывает, куда мех поехал.
    /// </summary>
    private void Turn(Entity<WarlockMechComponent> ent)
    {
        if (!TryComp<PhysicsComponent>(ent.Owner, out var physics))
            return;

        var velocity = physics.LinearVelocity;

        if (velocity.LengthSquared() < 0.25f)
            return;

        var heading = MathF.Atan2(velocity.Y, velocity.X);
        var delta = MathF.Abs(WrapAngle(heading - ent.Comp.Facing));

        ent.Comp.Facing = heading;

        if (delta < ent.Comp.TurnAngle * MathF.PI / 180f)
            return;

        var dry = ent.Comp.Lubricant / ent.Comp.MaxLubricant < ent.Comp.DryBelow;
        var time = ent.Comp.TurnTime * (dry ? ent.Comp.DryTurn : 1f);

        ent.Comp.TurnUntil = _timing.CurTime + TimeSpan.FromSeconds(time);
        _audio.PlayPvs(TurnSound, ent.Owner);
    }

    private static float WrapAngle(float angle)
    {
        while (angle > MathF.PI)
            angle -= MathF.Tau;
        while (angle < -MathF.PI)
            angle += MathF.Tau;
        return angle;
    }

    #endregion

    #region Смазка

    private void OnGrease(Entity<WarlockMechGreaseComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || !HasComp<WarlockMechComponent>(target))
            return;

        args.Handled = true;

        if (ent.Comp.Amount <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("warlock-mech-grease-empty"), ent.Owner, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay,
            new WarlockMechGreaseDoAfterEvent(), target, target, ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnGreaseDoAfter(Entity<WarlockMechComponent> ent, ref WarlockMechGreaseDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } can)
            return;

        if (!TryComp<WarlockMechGreaseComponent>(can, out var grease))
            return;

        args.Handled = true;

        var poured = MathF.Min(grease.Pour, MathF.Min(grease.Amount,
            ent.Comp.MaxLubricant - ent.Comp.Lubricant));

        if (poured <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("warlock-mech-grease-full"), ent.Owner, args.User);
            return;
        }

        grease.Amount -= poured;
        ent.Comp.Lubricant += poured;
        Dirty(ent);
        Dirty(can, grease);

        _audio.PlayPvs(PourSound, ent.Owner);
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
        _popup.PopupEntity(Loc.GetString("warlock-mech-grease-poured",
            ("amount", (int) poured)), ent.Owner, args.User, PopupType.Medium);

        if (grease.Amount <= 0f)
            QueueDel(can);
    }

    #endregion

    #region Урон и потеря узлов

    /// <summary>
    /// От урона с рамы срывает узлы.
    ///
    /// Не сразу и не каждый раз: урон копится, и на пороге отлетает случайный
    /// установленный узел. Из-за этого повреждённый мех не просто хуже стреляет —
    /// он теряет ноги и остаётся стоять там, где его подловили.
    /// </summary>
    private void OnDamaged(Entity<WarlockMechComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta is not { } delta || !args.DamageIncreased)
            return;

        // Огонь по раме плавит установленные узлы. Отдельно от отлёта: сгоревший мех
        // можно собрать обратно, но детали в нём останутся оплавленными навсегда.
        var heat = 0f;

        foreach (var (type, value) in delta.DamageDict)
        {
            if (type.Id is "Heat" or "Caustic")
                heat += value.Float();
        }

        if (heat > 0f)
            _parts.MeltInstalled(ent.Comp.PartContainer, heat * 0.35f);

        ent.Comp.DamageSincePartLoss += delta.GetTotal().Float();

        if (ent.Comp.DamageSincePartLoss < ent.Comp.PartLossThreshold)
            return;

        ent.Comp.DamageSincePartLoss = 0f;

        var installed = ent.Comp.PartContainer.ContainedEntities.ToList();

        if (installed.Count == 0)
            return;

        var lost = _random.Pick(installed);

        if (!_container.Remove(lost, ent.Comp.PartContainer))
            return;

        _transform.SetCoordinates(lost, Transform(ent.Owner).Coordinates);
        _movement.RefreshMovementSpeedModifiers(ent.Owner);

        _popup.PopupEntity(
            Loc.GetString("warlock-mech-part-torn", ("part", Name(lost))),
            ent.Owner,
            PopupType.LargeCaution);
    }

    #endregion

    /// <summary>
    /// Какие обязательные гнёзда пустуют прямо сейчас.
    /// </summary>
    private IEnumerable<string> MissingSlots(Entity<WarlockMechComponent> ent)
    {
        var filled = new HashSet<string>();

        foreach (var part in ent.Comp.PartContainer.ContainedEntities)
        {
            if (TryComp<WarlockMechPartComponent>(part, out var comp))
                filled.Add(comp.Slot);
        }

        foreach (var slot in ent.Comp.RequiredSlots)
        {
            if (!filled.Contains(slot))
                yield return slot;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WarlockMechComponent, MechComponent>();

        while (query.MoveNext(out var uid, out var mech, out _))
        {
            var ent = new Entity<WarlockMechComponent>(uid, mech);

            Turn(ent);

            if (now < mech.NextTick)
                continue;

            mech.NextTick = now + TimeSpan.FromSeconds(mech.TickInterval);

            if (!_vehicle.HasOperator(uid))
                continue;

            var moving = TryComp<PhysicsComponent>(uid, out var physics)
                         && physics.LinearVelocity.LengthSquared() > 0.25f;

            if (moving && mech.Lubricant > 0f)
            {
                mech.Lubricant = MathF.Max(0f, mech.Lubricant - mech.LubricantPerSecond * mech.TickInterval);
                Dirty(uid, mech);

                // Пересчитываем скорость ровно на переходе через порог, а не каждый такт.
                if (mech.Lubricant / mech.MaxLubricant < mech.DryBelow
                    && (mech.Lubricant + mech.LubricantPerSecond * mech.TickInterval) / mech.MaxLubricant
                    >= mech.DryBelow)
                {
                    _movement.RefreshMovementSpeedModifiers(uid);
                    _popup.PopupEntity(Loc.GetString("warlock-mech-dry"), uid, PopupType.MediumCaution);
                }
            }

            // Сухая рама стирает саму себя.
            if (moving && mech.Lubricant / mech.MaxLubricant < mech.DryBelow)
                _parts.WearRandom(mech.PartContainer, mech.DryWear, _random);
        }
    }
}


