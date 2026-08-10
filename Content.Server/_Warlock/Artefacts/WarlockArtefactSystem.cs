using Content.Server._Warlock.Artefacts.Components;
using Content.Shared._Warlock.Artefacts.Events;
using Content.Shared._Warlock.Psionics;
using Content.Shared._Warlock.Psionics.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Dice;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Warlock.Artefacts;

/// <summary>
/// _Warlock
/// Артефакты вымершей планетарной расы. Это не ванильная ксеноархеология: у каждого предмета
/// свой чёткий эффект, своя цена и своя подлость, ближе к именным артефактам настолок,
/// чем к случайным аномалиям.
/// </summary>
public sealed class WarlockArtefactSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDiceSystem _dice = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly WarlockPsionicsSystem _psionics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockFateDieComponent, UseInHandEvent>(OnFateDieUsed);

        SubscribeLocalEvent<WarlockPsiShacklesComponent, GotEquippedEvent>(OnShacklesEquipped);
        SubscribeLocalEvent<WarlockPsiShacklesComponent, GotUnequippedEvent>(OnShacklesUnequipped);
        SubscribeLocalEvent<WarlockPsiShacklesComponent, BeingUnequippedAttemptEvent>(OnShacklesUnequipAttempt);

        SubscribeLocalEvent<WarlockDeadgodCandleComponent, ItemToggledEvent>(OnCandleToggled);

        SubscribeLocalEvent<WarlockGuildUrnComponent, InteractUsingEvent>(OnUrnInteractUsing);
        SubscribeLocalEvent<WarlockGuildUrnComponent, UseInHandEvent>(OnUrnUsedInHand);

        SubscribeLocalEvent<WarlockThousandHandsEvent>(OnThousandHands);
    }

    #region Кость Судьбы

    private void OnFateDieUsed(Entity<WarlockFateDieComponent> ent, ref UseInHandEvent args)
    {
        // Бросок считаем здесь, а не в ванильной системе кубиков, чтобы результат был серверным
        // и совпадал с применённым эффектом. Спрайт подтянется сам через DiceComponent.
        var roll = _random.Next(1, 21);

        if (TryComp<DiceComponent>(ent, out var dice))
            _dice.SetCurrentValue((ent.Owner, dice), roll);

        args.Handled = true;

        var user = args.User;

        switch (roll)
        {
            // Критический провал: артефакт разряжается прямо в руку владельца.
            case 1:
                _damageable.TryChangeDamage(user, new DamageSpecifier { DamageDict = { ["Shock"] = ent.Comp.CritFailureShock } }, origin: ent.Owner);
                _stamina.TakeStaminaDamage(user, ent.Comp.FailureStamina * 2, source: ent.Owner);
                _popup.PopupEntity(Loc.GetString("warlock-artefact-die-crit-fail"), user, user, PopupType.LargeCaution);
                break;

            // Неудача: кость забирает силы, но ничего не ломает.
            case >= 2 and <= 5:
                _stamina.TakeStaminaDamage(user, ent.Comp.FailureStamina, source: ent.Owner);
                _popup.PopupEntity(Loc.GetString("warlock-artefact-die-fail"), user, user, PopupType.MediumCaution);
                break;

            // Пустой бросок.
            case >= 6 and <= 10:
                _popup.PopupEntity(Loc.GetString("warlock-artefact-die-nothing"), user, user);
                break;

            // Успех: немного дара и немного здоровья.
            case >= 11 and <= 19:
            {
                var restored = _psionics.RestoreEnergy(user, roll * ent.Comp.EnergyPerPip);
                _damageable.TryChangeDamage(user, MakeHeal(roll), true, origin: ent.Owner);
                _popup.PopupEntity(
                    Loc.GetString("warlock-artefact-die-success", ("roll", roll), ("amount", restored.Int())),
                    user,
                    user);
                break;
            }

            // Двадцатка: артефакт признаёт владельца своим.
            default:
            {
                if (TryComp<WarlockPsionicComponent>(user, out var psionic))
                    _psionics.SetEnergy((user, psionic), psionic.MaxEnergy);

                // Отрицательное значение снимает усталость; система сама зажимает результат в ноль.
                if (TryComp<StaminaComponent>(user, out var stamina))
                    _stamina.TakeStaminaDamage(user, -stamina.StaminaDamage, stamina, visual: false);

                _damageable.TryChangeDamage(user, MakeHeal(40), true, origin: ent.Owner);
                _popup.PopupEntity(Loc.GetString("warlock-artefact-die-crit-success"), user, user, PopupType.Large);
                break;
            }
        }
    }

    private static DamageSpecifier MakeHeal(float amount)
    {
        return new DamageSpecifier
        {
            DamageDict =
            {
                ["Blunt"] = -amount,
                ["Slash"] = -amount,
                ["Piercing"] = -amount,
                ["Heat"] = -amount,
            },
        };
    }

    #endregion

    #region Оковы Логики

    private void OnShacklesEquipped(Entity<WarlockPsiShacklesComponent> ent, ref GotEquippedEvent args)
    {
        EnsureComp<WarlockPsiSuppressedComponent>(args.EquipTarget);
        _popup.PopupEntity(Loc.GetString("warlock-artefact-shackles-locked"), args.EquipTarget, args.EquipTarget, PopupType.LargeCaution);
    }

    private void OnShacklesUnequipped(Entity<WarlockPsiShacklesComponent> ent, ref GotUnequippedEvent args)
    {
        RemComp<WarlockPsiSuppressedComponent>(args.EquipTarget);
        _popup.PopupEntity(Loc.GetString("warlock-artefact-shackles-released"), args.EquipTarget, args.EquipTarget);
    }

    private void OnShacklesUnequipAttempt(Entity<WarlockPsiShacklesComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        if (ent.Comp.AllowSelfRemoval)
            return;

        // Проклятие оков: снять их с себя нельзя, нужен кто-то ещё.
        if (args.User != args.UnEquipTarget)
            return;

        args.Cancel();
        args.Reason = "warlock-artefact-shackles-stuck";
    }

    #endregion

    #region Свеча Мёртвого Бога

    private void OnCandleToggled(Entity<WarlockDeadgodCandleComponent> ent, ref ItemToggledEvent args)
    {
        if (!args.Activated)
            return;

        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.TickInterval);
    }

    private void UpdateCandle(Entity<WarlockDeadgodCandleComponent> ent)
    {
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.TickInterval);

        // Свеча питается от того, кто её держит или носит.
        var bearer = _container.TryGetContainingContainer((ent.Owner, null, null), out var container)
            ? container.Owner
            : EntityUid.Invalid;

        if (bearer == EntityUid.Invalid)
            return;

        if (HasComp<WarlockPsionicComponent>(bearer))
        {
            if (!_psionics.TryUseEnergy(bearer, ent.Comp.EnergyPerTick))
            {
                _popup.PopupEntity(Loc.GetString("warlock-artefact-candle-drained"), bearer, bearer, PopupType.MediumCaution);
                return;
            }
        }
        else
        {
            // Без дара свеча берёт плату теплом самого носителя.
            _damageable.TryChangeDamage(
                bearer,
                new DamageSpecifier { DamageDict = { ["Heat"] = ent.Comp.BurnPerTick } },
                origin: ent.Owner);
        }

        var heal = new DamageSpecifier
        {
            DamageDict =
            {
                ["Blunt"] = -ent.Comp.HealPerTick,
                ["Slash"] = -ent.Comp.HealPerTick,
                ["Piercing"] = -ent.Comp.HealPerTick,
                ["Asphyxiation"] = -ent.Comp.HealPerTick,
            },
        };

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(ent).Coordinates, ent.Comp.Radius))
        {
            // Свеча держит только тех, кто уже за краем — здоровых она не лечит.
            if (!_mobState.IsCritical(mob.Owner, mob.Comp))
                continue;

            _damageable.TryChangeDamage(mob.Owner, heal, true, origin: ent.Owner);
        }
    }

    #endregion

    #region Урна Трёх Гильдий

    private void OnUrnInteractUsing(Entity<WarlockGuildUrnComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<ItemComponent>(args.Used))
            return;

        args.Handled = true;

        if (ent.Comp.Charge >= ent.Comp.MaxCharge)
        {
            _popup.PopupEntity(Loc.GetString("warlock-artefact-urn-full"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        ent.Comp.Charge = Math.Min(ent.Comp.MaxCharge, ent.Comp.Charge + ent.Comp.ChargePerItem);

        QueueDel(args.Used);

        _popup.PopupEntity(
            Loc.GetString("warlock-artefact-urn-consumed", ("charge", (int) ent.Comp.Charge)),
            ent,
            args.User);
    }

    private void OnUrnUsedInHand(Entity<WarlockGuildUrnComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Charge <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("warlock-artefact-urn-empty"), ent, args.User);
            return;
        }

        if (!HasComp<WarlockPsionicComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("warlock-artefact-urn-no-gift"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        var restored = _psionics.RestoreEnergy(args.User, ent.Comp.Charge);
        ent.Comp.Charge = 0f;

        _popup.PopupEntity(
            Loc.GetString("warlock-artefact-urn-drained", ("amount", restored.Int())),
            ent,
            args.User);
    }

    #endregion

    #region Перчатка Тысячи Рук

    private void OnThousandHands(WarlockThousandHandsEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        var performer = args.Performer;

        if (!HasComp<ItemComponent>(target) || _container.IsEntityInContainer(target))
        {
            _popup.PopupEntity(Loc.GetString("warlock-artefact-gauntlet-invalid"), performer, performer, PopupType.MediumCaution);
            return;
        }

        args.Handled = true;

        if (!_hands.TryPickupAnyHand(performer, target))
        {
            _popup.PopupEntity(Loc.GetString("warlock-artefact-gauntlet-no-hand"), performer, performer, PopupType.MediumCaution);
            return;
        }

        _audio.PlayPvs(args.Sound, performer);
        _popup.PopupEntity(Loc.GetString("warlock-artefact-gauntlet-pull"), performer, performer);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var candles = EntityQueryEnumerator<WarlockDeadgodCandleComponent, ItemToggleComponent>();
        while (candles.MoveNext(out var uid, out var candle, out var toggle))
        {
            if (!toggle.Activated || now < candle.NextTick)
                continue;

            UpdateCandle((uid, candle));
        }
    }
}
