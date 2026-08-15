using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Containers;

namespace Content.Shared._Warlock.Exosuits;

/// <summary>
/// _Warlock
/// Экзоскелеты Братства Стали.
///
/// Система делает ровно две вещи, которых нет в ванили: держит на носителе посчитанную
/// прибавку к удару и меняет его скорость в зависимости от того, жива ли батарея.
/// Всё остальное — слот батареи, расход заряда, само включение и выключение — работает
/// на ванильных PowerCellSlot, PowerCellDraw и ItemToggle. Переписывать их своими руками
/// значило бы завести второй источник правды о заряде и рано или поздно с ним разойтись.
///
/// Почему скорость считается здесь, а не ванильным ClothingSpeedModifier: тот умеет
/// замедлять только когда вещь включена, а нужно наоборот — включённая рама почти не
/// мешает, а выключенная превращается в груз. Такого режима у него нет.
/// </summary>
public sealed partial class WarlockExosuitSystem : EntitySystem
{
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedContainerSystem _container = default!;

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
        // Рама могла лежать на полу или в ящике — тогда включать нечего.
        if (!_container.TryGetContainingContainer((ent.Owner, null, null), out var container))
            return;

        // И могла лежать в рюкзаке. Владелец контейнера тогда — сам рюкзак, и вешать
        // на него компонент носителя нельзя. Признак того, что рама именно надета, —
        // компонент, поставленный при надевании: если его нет, раму не надевали.
        if (!HasComp<WarlockExosuitWearerComponent>(container.Owner))
            return;

        Apply(ent, container.Owner);
    }

    /// <summary>
    /// Пересчитать прибавку на носителе и обновить его скорость.
    /// </summary>
    private void Apply(Entity<WarlockExosuitComponent> ent, EntityUid wearer)
    {
        var wearerComp = EnsureComp<WarlockExosuitWearerComponent>(wearer);
        wearerComp.Bonus = _toggle.IsActivated(ent.Owner) ? ent.Comp.StrengthBonus : 1f;
        Dirty(wearer, wearerComp);

        _speed.RefreshMovementSpeedModifiers(wearer);
    }

    #endregion

    #region Скорость

    private void OnRefreshSpeed(Entity<WarlockExosuitComponent> ent, ref InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        if (_toggle.IsActivated(ent.Owner))
            args.Args.ModifySpeed(ent.Comp.PoweredWalk, ent.Comp.PoweredSprint);
        else
            args.Args.ModifySpeed(ent.Comp.DeadWalk, ent.Comp.DeadSprint);
    }

    #endregion

    #region Осмотр

    private void OnExamined(Entity<WarlockExosuitComponent> ent, ref ExaminedEvent args)
    {
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
    }

    #endregion
}
