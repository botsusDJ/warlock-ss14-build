using System.Numerics;
using Content.Server._Warlock.Psionics.Components;
using Content.Server.Construction.Components;
using Content.Shared._Warlock.Psionics;
using Content.Shared._Warlock.Psionics.Components;
using Content.Shared._Warlock.Psionics.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._Warlock.Psionics;

/// <summary>
/// _Warlock
/// Реализация пяти заклинаний техномагов. Всё выполняется на сервере: заклинания двигают предметы,
/// удаляют конструкции и телепортируют существ — предсказывать это на клиенте смысла нет.
/// </summary>
public sealed partial class WarlockSpellsSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private WarlockPsionicsSystem _psionics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockLitanyOfUnmakingEvent>(OnLitanyOfUnmaking);
        SubscribeLocalEvent<WarlockChoirOfSteelEvent>(OnChoirOfSteel);
        SubscribeLocalEvent<WarlockDeadgodEchoEvent>(OnDeadgodEcho);
        SubscribeLocalEvent<WarlockBorrowedBreathEvent>(OnBorrowedBreath);
        SubscribeLocalEvent<WarlockSharedFateEvent>(OnSharedFate);

        SubscribeLocalEvent<WarlockSharedFateComponent, DamageChangedEvent>(OnSharedFateDamage);
        SubscribeLocalEvent<WarlockSharedFateComponent, ComponentShutdown>(OnSharedFateShutdown);
    }

    #region Литания Расщепления

    private void OnLitanyOfUnmaking(WarlockLitanyOfUnmakingEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        var performer = args.Performer;

        // Расщеплять можно только неживую закреплённую технику: машины, шлюзы, консоли и прочее железо.
        if (HasComp<MobStateComponent>(target) || !Transform(target).Anchored)
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-litany-invalid-target"), performer, performer, PopupType.MediumCaution);
            return;
        }

        args.Handled = true;

        var coordinates = Transform(target).Coordinates;

        _audio.PlayPvs(args.Sound, coordinates);

        foreach (var residue in args.Residue)
        {
            Spawn(residue, coordinates);
        }

        // Чем сложнее машина, тем больше структуры высвобождается.
        var bonus = HasComp<MachineComponent>(target) ? 1.5f : 1f;
        var restored = _psionics.RestoreEnergy(performer, args.EnergyReturn * bonus);

        QueueDel(target);

        _popup.PopupEntity(
            Loc.GetString("warlock-spell-litany-success", ("amount", restored.Int())),
            performer,
            performer);
    }

    #endregion

    #region Хор Стали

    private void OnChoirOfSteel(WarlockChoirOfSteelEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var performer = args.Performer;
        var casterCoords = Transform(performer).Coordinates;

        _audio.PlayPvs(args.Sound, performer);

        // Первая фаза: стягиваем к себе всё незакреплённое железо в радиусе.
        var pulled = 0;
        foreach (var item in GetLooseItems(casterCoords, args.GatherRadius, args.MaxItems))
        {
            if (item.Owner == performer)
                continue;

            _throwing.TryThrow(item, casterCoords, 8f, performer, playSound: false);
            pulled++;
        }

        // Вторая фаза наступит по таймеру — предметы разлетятся наружу.
        var choir = EnsureComp<WarlockSteelChoirComponent>(performer);
        choir.BurstAt = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);
        choir.BurstRadius = args.BurstRadius;
        choir.MaxItems = args.MaxItems;

        _popup.PopupEntity(
            Loc.GetString("warlock-spell-choir-cast", ("count", pulled)),
            performer,
            performer);
    }

    private void BurstSteelChoir(Entity<WarlockSteelChoirComponent> ent)
    {
        var coords = Transform(ent).Coordinates;
        var origin = _transform.GetWorldPosition(ent);

        foreach (var item in GetLooseItems(coords, ent.Comp.BurstRadius, ent.Comp.MaxItems))
        {
            if (item.Owner == ent.Owner)
                continue;

            var direction = _transform.GetWorldPosition(item) - origin;

            // Предмет, лежащий ровно под техномагом, надо толкнуть хоть куда-то.
            if (direction.LengthSquared() < 0.01f)
                direction = new Vector2(1f, 0f);

            _throwing.TryThrow(item, direction.Normalized() * 6f, 18f, ent.Owner, playSound: false);
        }

        RemCompDeferred<WarlockSteelChoirComponent>(ent);
    }

    #endregion

    #region Отзвук Мёртвого Бога

    private void OnDeadgodEcho(WarlockDeadgodEchoEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var anchorUid = Spawn(args.Anchor, args.Target);
        var anchor = EnsureComp<WarlockDeadgodAnchorComponent>(anchorUid);

        anchor.Caster = args.Performer;
        anchor.CollapseAt = _timing.CurTime + TimeSpan.FromSeconds(anchor.Delay);
        anchor.Snapshot.Clear();

        // Снимаем слепок: кто где стоял в момент установки печати.
        var coords = Transform(anchorUid).Coordinates;
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, anchor.Radius))
        {
            anchor.Snapshot.Add((mob.Owner, Transform(mob.Owner).Coordinates));
        }

        _audio.PlayPvs(args.Sound, anchorUid);

        _popup.PopupEntity(
            Loc.GetString("warlock-spell-echo-cast", ("count", anchor.Snapshot.Count)),
            args.Performer,
            args.Performer);
    }

    private void CollapseDeadgodAnchor(Entity<WarlockDeadgodAnchorComponent> ent)
    {
        var returned = 0;

        foreach (var (entity, coordinates) in ent.Comp.Snapshot)
        {
            if (TerminatingOrDeleted(entity) || !coordinates.IsValid(EntityManager))
                continue;

            // Возвращаем только тех, кто всё ещё рядом с печатью — убежавших далеко откат не достаёт.
            if (!_transform.InRange(Transform(entity).Coordinates, Transform(ent).Coordinates, ent.Comp.Radius))
                continue;

            _transform.SetCoordinates(entity, coordinates);
            returned++;

            _popup.PopupEntity(Loc.GetString("warlock-spell-echo-pulled"), entity, entity, PopupType.MediumCaution);
        }

        if (ent.Comp.Caster is { } caster && !TerminatingOrDeleted(caster))
        {
            _popup.PopupEntity(
                Loc.GetString("warlock-spell-echo-collapse", ("count", returned)),
                caster,
                caster);
        }

        QueueDel(ent);
    }

    #endregion

    #region Заёмное Дыхание

    private void OnBorrowedBreath(WarlockBorrowedBreathEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;
        var target = args.Target;

        if (target == performer)
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-breath-self"), performer, performer, PopupType.MediumCaution);
            return;
        }

        if (!HasComp<DamageableComponent>(target))
            return;

        args.Handled = true;

        _damageable.TryChangeDamage(target, args.Healing, true, origin: performer);

        // Плата: чужие раны техномаг тянет через себя.
        if (HasComp<StaminaComponent>(performer))
            _stamina.TakeStaminaDamage(performer, args.CasterStaminaCost, source: performer);

        _audio.PlayPvs(args.Sound, target);

        _popup.PopupEntity(Loc.GetString("warlock-spell-breath-target"), target, target);
        _popup.PopupEntity(Loc.GetString("warlock-spell-breath-caster"), performer, performer);
    }

    #endregion

    #region Разделённая Участь

    private void OnSharedFate(WarlockSharedFateEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;
        var target = args.Target;

        if (target == performer || !HasComp<DamageableComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-fate-invalid-target"), performer, performer, PopupType.MediumCaution);
            return;
        }

        args.Handled = true;

        var endAt = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);

        BindFate(performer, target, endAt, args.Coefficient);
        BindFate(target, performer, endAt, args.Coefficient);

        _audio.PlayPvs(args.Sound, performer);

        _popup.PopupEntity(Loc.GetString("warlock-spell-fate-bound"), target, target, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("warlock-spell-fate-bound"), performer, performer, PopupType.Large);
    }

    private void BindFate(EntityUid uid, EntityUid partner, TimeSpan endAt, float coefficient)
    {
        var comp = EnsureComp<WarlockSharedFateComponent>(uid);
        comp.Partner = partner;
        comp.EndAt = endAt;
        comp.Coefficient = coefficient;
        comp.Relaying = false;
    }

    private void OnSharedFateDamage(Entity<WarlockSharedFateComponent> ent, ref DamageChangedEvent args)
    {
        // Делим только реальный входящий урон, лечение не размазываем.
        if (args.DamageDelta is not { } delta || !args.DamageIncreased)
            return;

        // Флаг не даёт двум связанным бесконечно перекидывать урон друг другу.
        if (ent.Comp.Relaying)
            return;

        var partner = ent.Comp.Partner;
        if (TerminatingOrDeleted(partner) || !TryComp<WarlockSharedFateComponent>(partner, out var partnerFate))
            return;

        ent.Comp.Relaying = true;
        partnerFate.Relaying = true;

        _damageable.TryChangeDamage(partner, delta * ent.Comp.Coefficient, true, origin: args.Origin);

        ent.Comp.Relaying = false;
        partnerFate.Relaying = false;
    }

    private void OnSharedFateShutdown(Entity<WarlockSharedFateComponent> ent, ref ComponentShutdown args)
    {
        var partner = ent.Comp.Partner;
        if (TerminatingOrDeleted(partner))
            return;

        // Связь всегда рвётся с обеих сторон сразу.
        if (TryComp<WarlockSharedFateComponent>(partner, out var partnerFate) && partnerFate.Partner == ent.Owner)
            RemCompDeferred<WarlockSharedFateComponent>(partner);
    }

    #endregion

    /// <summary>
    /// Возвращает "свободные" предметы вокруг точки: не в контейнере, не закреплённые, с физикой.
    /// </summary>
    private List<Entity<ItemComponent>> GetLooseItems(EntityCoordinates coordinates, float radius, int max)
    {
        var result = new List<Entity<ItemComponent>>();

        foreach (var item in _lookup.GetEntitiesInRange<ItemComponent>(coordinates, radius))
        {
            if (result.Count >= max)
                break;

            if (_container.IsEntityInContainer(item.Owner))
                continue;

            // Поля PhysicsComponent закрыты песочницей, поэтому просто требуем его наличие:
            // без физики ThrowingSystem всё равно ничего не сделает. Закреплённое отсекаем трансформом.
            if (!HasComp<PhysicsComponent>(item.Owner))
                continue;

            if (Transform(item.Owner).Anchored)
                continue;

            result.Add(item);
        }

        return result;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var choirs = EntityQueryEnumerator<WarlockSteelChoirComponent>();
        while (choirs.MoveNext(out var uid, out var choir))
        {
            if (now < choir.BurstAt)
                continue;

            BurstSteelChoir((uid, choir));
        }

        var anchors = EntityQueryEnumerator<WarlockDeadgodAnchorComponent>();
        while (anchors.MoveNext(out var uid, out var anchor))
        {
            if (now < anchor.CollapseAt)
                continue;

            CollapseDeadgodAnchor((uid, anchor));
        }

        var fates = EntityQueryEnumerator<WarlockSharedFateComponent>();
        while (fates.MoveNext(out var uid, out var fate))
        {
            if (now < fate.EndAt)
                continue;

            RemCompDeferred<WarlockSharedFateComponent>(uid);
        }
    }
}
