using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Warlock.Exosuits;

/// <summary>
/// _Warlock
/// Экзоскелеты.
///
/// Питание, слот батареи и включение работают на ванильных PowerCellSlot, PowerCellDraw,
/// ToggleCellDraw и ItemToggle. Переписывать их своими руками значило бы завести второй
/// источник правды о заряде и рано или поздно с ним разойтись.
///
/// Своё здесь четыре вещи, которых в ванили нет:
///
///   1. Тяжесть обесточенной рамы. Ванильный ClothingSpeedModifier умеет замедлять только
///      когда вещь включена, а нужно наоборот: включённая рама почти не мешает, а мёртвая
///      превращается в груз.
///   2. Распределение мощности между кулаками и тем, что в руках. Мощность именно делится,
///      усилить хват можно только ослабив удар.
///   3. Жар. Приводы греются от работы и особенно от драки, и жар надо куда-то девать.
///   4. Выброс. Когда девать некуда, рама сбрасывает всё разом — по площади и по своим.
/// </summary>
public sealed partial class WarlockExosuitSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerCellSystem _cell = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockExosuitComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<WarlockExosuitComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<WarlockExosuitComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<WarlockExosuitComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<WarlockExosuitComponent, InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshSpeed);
    }

    #region Надевание

    private void OnEquipped(Entity<WarlockExosuitComponent> ent, ref GotEquippedEvent args)
    {
        if (args.Slot != ent.Comp.Slot)
            return;

        Apply(ent, args.EquipTarget);
    }

    private void OnUnequipped(Entity<WarlockExosuitComponent> ent, ref GotUnequippedEvent args)
    {
        if (args.Slot != ent.Comp.Slot)
            return;

        RemComp<WarlockExosuitWearerComponent>(args.EquipTarget);
        _speed.RefreshMovementSpeedModifiers(args.EquipTarget);
    }

    private void OnToggled(Entity<WarlockExosuitComponent> ent, ref ItemToggledEvent args)
    {
        if (!_container.TryGetContainingContainer((ent.Owner, null, null), out var container))
            return;

        // Рама могла лежать в рюкзаке. Владелец контейнера тогда — сам рюкзак, и вешать
        // на него компонент носителя нельзя. Признак того, что рама надета, — компонент,
        // поставленный при надевании.
        if (!HasComp<WarlockExosuitWearerComponent>(container.Owner))
            return;

        Apply(ent, container.Owner);
    }

    /// <summary>
    /// Пересчитать всё, что рама даёт носителю, и обновить его скорость.
    /// Вызывается на надевании, на переключении и на смене настроек в ОС.
    /// </summary>
    public void Apply(Entity<WarlockExosuitComponent> ent, EntityUid wearer)
    {
        var w = EnsureComp<WarlockExosuitWearerComponent>(wearer);
        w.Suit = ent.Owner;

        if (!_toggle.IsActivated(ent.Owner))
        {
            w.FistBonus = 1f;
            w.ToolBonus = 1f;
            w.TearStrength = 0f;
        }
        else
        {
            var gain = (ent.Comp.StrengthBonus - 1f) * CellOutput(ent);
            var swing = ent.Comp.ShareSwing;
            var fist = ent.Comp.FistShare;
            var tool = 1f - fist;

            // Доля канала умножается сама на себя через ShareSwing: перекос выгоднее
            // ровного деления, но выкрутить оба канала нельзя — их сумма всегда единица.
            //
            //   перекос в кулаки  -> кулак 1.56, предмет 1.00
            //   поровну           -> оба 1.23
            //
            // Ровное деление специально хуже любого перекоса: рама должна заставлять
            // выбирать роль в бою заранее, а не давать универсальную прибавку.
            w.FistBonus = 1f + gain * fist * (1f + swing * fist);
            w.ToolBonus = 1f + gain * tool * (1f + swing * tool);
            w.TearStrength = ent.Comp.TearStrength * CellOutput(ent) * (0.5f + fist);
        }

        Dirty(wearer, w);
        _speed.RefreshMovementSpeedModifiers(wearer);
    }

    /// <summary>
    /// Множитель мощности от вставленной ячейки. Обычная батарея даёт единицу.
    /// </summary>
    private float CellOutput(Entity<WarlockExosuitComponent> ent)
    {
        return TryGetCell(ent, out var cell) ? cell.Value.Comp.Output : 1f;
    }

    private bool TryGetCell(Entity<WarlockExosuitComponent> ent, out Entity<WarlockExoCellComponent>? cell)
    {
        cell = null;
        if (!_cell.TryGetBatteryFromSlot(ent.Owner, out var batteryUid, out _))
            return false;
        if (batteryUid is not { } uid || !TryComp<WarlockExoCellComponent>(uid, out var comp))
            return false;

        cell = (uid, comp);
        return true;
    }

    #endregion

    #region Скорость и сила

    private void OnRefreshSpeed(Entity<WarlockExosuitComponent> ent, ref InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        if (_toggle.IsActivated(ent.Owner))
            args.Args.ModifySpeed(ent.Comp.PoweredWalk, ent.Comp.PoweredSprint);
        else
            args.Args.ModifySpeed(ent.Comp.DeadWalk, ent.Comp.DeadSprint);
    }

    /// <summary>
    /// Множитель урона в ближнем бою. Вызывается из WarlockAttackStrengthSystem:
    /// пара MeleeWeaponComponent + GetMeleeDamageEvent допускает одну подписку
    /// на весь билд, и она занята там.
    /// </summary>
    public float MeleeModifier(EntityUid user, bool unarmed)
    {
        if (!TryComp<WarlockExosuitWearerComponent>(user, out var w))
            return 1f;

        return unarmed ? w.FistBonus : w.ToolBonus;
    }

    /// <summary>
    /// Нагреть раму за удар. Дёргается системой силы удара оттуда же.
    /// </summary>
    public void HeatFromSwing(EntityUid user)
    {
        if (!TryComp<WarlockExosuitWearerComponent>(user, out var w)
            || w.Suit is not { } suit
            || !TryComp<WarlockExosuitComponent>(suit, out var exo)
            || !_toggle.IsActivated(suit))
            return;

        AddHeat((suit, exo), exo.HeatPerSwing);
    }

    #endregion

    #region Жар

    public void AddHeat(Entity<WarlockExosuitComponent> ent, float amount)
    {
        if (TryGetCell(ent, out var cell))
            amount *= cell!.Value.Comp.HeatFactor;

        ent.Comp.Heat = Math.Clamp(ent.Comp.Heat + amount, 0f, ent.Comp.MaxHeat);
        Dirty(ent);
    }

    private void OnExamined(Entity<WarlockExosuitComponent> ent, ref ExaminedEvent args)
    {
        var heat = (int) MathF.Round(ent.Comp.Heat / ent.Comp.MaxHeat * 100f);

        if (_toggle.IsActivated(ent.Owner))
        {
            args.PushMarkup(Loc.GetString("warlock-exosuit-examine-live",
                ("bonus", (int) MathF.Round((ent.Comp.StrengthBonus - 1f) * 100f))));
        }
        else
        {
            args.PushMarkup(Loc.GetString("warlock-exosuit-examine-dead",
                ("slow", (int) MathF.Round((1f - ent.Comp.DeadSprint) * 100f))));
        }

        args.PushMarkup(Loc.GetString("warlock-exosuit-examine-heat", ("heat", heat)));

        if (!ent.Comp.Limiter)
            args.PushMarkup(Loc.GetString("warlock-exosuit-examine-nolimiter"));
    }

    #endregion

    #region Ход времени

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Жар и выброс считает только сервер: предсказанный на клиенте взрыв
        // выглядел бы как вспышка, которой не было.
        if (!_net.IsServer)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WarlockExosuitComponent>();
        while (query.MoveNext(out var uid, out var exo))
        {
            if (now < exo.NextTick)
                continue;

            exo.NextTick = now + TimeSpan.FromSeconds(exo.TickInterval);
            Tick((uid, exo));
        }
    }

    private void Tick(Entity<WarlockExosuitComponent> ent)
    {
        var dt = ent.Comp.TickInterval;
        var active = _toggle.IsActivated(ent.Owner);

        if (active)
        {
            AddHeat(ent, ent.Comp.HeatPerSecond * dt);

            // Активное охлаждение само ест заряд: холод не бесплатный.
            if (ent.Comp.Cooling == WarlockExoCooling.Active)
                _cell.TryUseCharge(ent.Owner, ent.Comp.ActiveCoolDraw * dt);
        }

        var cool = ent.Comp.CoolPerSecond * dt;
        if (ent.Comp.Cooling == WarlockExoCooling.Active)
            cool *= ent.Comp.ActiveCoolFactor;
        ent.Comp.Heat = MathF.Max(0f, ent.Comp.Heat - cool);

        var wearer = GetWearer(ent);

        // Выше порога рама жжёт того, кто в ней.
        if (active && ent.Comp.Heat >= ent.Comp.ScorchAt && wearer is { } victim)
        {
            _damageable.TryChangeDamage(victim,
                new DamageSpecifier { DamageDict = { ["Heat"] = ent.Comp.ScorchDamage * dt } },
                origin: ent.Owner);

            _popup.PopupEntity(Loc.GetString("warlock-exosuit-scorch"), victim, victim,
                PopupType.SmallCaution);
        }

        if (ent.Comp.Heat < ent.Comp.MaxHeat)
        {
            Dirty(ent);
            return;
        }

        // Потолок достигнут. Дальше решает ограничитель — и удача.
        var unstable = ent.Comp.UnstableChance
                       + (TryGetCell(ent, out var cell) ? cell!.Value.Comp.UnstableBonus : 0f);

        if (ent.Comp.Limiter && !_random.Prob(unstable))
        {
            _toggle.TryDeactivate(ent.Owner);
            ent.Comp.Heat = ent.Comp.MaxHeat * 0.6f;

            if (wearer is { } w)
                _popup.PopupEntity(Loc.GetString("warlock-exosuit-limiter"), w, w, PopupType.LargeCaution);
        }
        else
        {
            Discharge(ent, wearer);
        }

        Dirty(ent);
    }

    /// <summary>
    /// Выброс. Рама сбрасывает весь накопленный жар разом — по площади и без разбора,
    /// свой перед ней или чужой.
    /// </summary>
    private void Discharge(Entity<WarlockExosuitComponent> ent, EntityUid? wearer)
    {
        var origin = wearer ?? ent.Owner;
        var coords = Transform(origin).Coordinates;

        foreach (var victim in _lookup.GetEntitiesInRange<DamageableComponent>(coords, ent.Comp.DischargeRange))
        {
            _damageable.TryChangeDamage(victim.Owner,
                new DamageSpecifier
                {
                    DamageDict =
                    {
                        ["Heat"] = ent.Comp.DischargeDamage * 0.6f,
                        ["Shock"] = ent.Comp.DischargeDamage * 0.4f,
                    },
                },
                origin: ent.Owner);
        }

        _cell.TryUseCharge(ent.Owner, ent.Comp.DischargeCost);
        _toggle.TryDeactivate(ent.Owner);
        ent.Comp.Heat = 0f;

        _popup.PopupEntity(Loc.GetString("warlock-exosuit-discharge"), origin, PopupType.LargeCaution);
    }

    private EntityUid? GetWearer(Entity<WarlockExosuitComponent> ent)
    {
        if (!_container.TryGetContainingContainer((ent.Owner, null, null), out var container))
            return null;

        return HasComp<WarlockExosuitWearerComponent>(container.Owner) ? container.Owner : null;
    }

    #endregion
}
