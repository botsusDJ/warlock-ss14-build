using Content.Server._Warlock.Religion.Components;
using Content.Shared._Warlock.Psionics;
using Content.Shared._Warlock.Psionics.Components;
using Content.Shared._Warlock.Religion;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._Warlock.Religion;

/// <summary>
/// _Warlock
/// Молитва двум живым культам этой части галактики.
///
/// Механтехион — бог Братства Стали: его волнует исправность, а не жизнь, поэтому молитва
/// у капища чинит всё неживое вокруг, включая тех, кто сделан из металла, и не трогает плоть.
///
/// Касс — божество вымершей планетарной расы и создатель артефактов. Гильдия Варлок молится
/// ему напрямую, и он отвечает единственным, что у него есть: возвращает дар. Тем, у кого
/// дара нет, обелиск не отвечает вовсе.
///
/// Переносная атрибутика (свод, скрижаль, знаки) сама по себе бесполезна — она усиливает
/// и ускоряет молитву у святилища своего бога, и мешает молиться чужому.
/// </summary>
public sealed class WarlockReligionSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly WarlockPsionicsSystem _psionics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockShrineComponent, ActivateInWorldEvent>(OnShrineActivated);
        SubscribeLocalEvent<WarlockShrineComponent, WarlockPrayerDoAfterEvent>(OnPrayerFinished);
        SubscribeLocalEvent<WarlockShrineComponent, ExaminedEvent>(OnShrineExamined);
    }

    private void OnShrineExamined(Entity<WarlockShrineComponent> ent, ref ExaminedEvent args)
    {
        if (_timing.CurTime < ent.Comp.NextPrayer)
            args.PushMarkup(Loc.GetString("warlock-shrine-examine-silent"));
    }

    private void OnShrineActivated(Entity<WarlockShrineComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;

        if (_timing.CurTime < ent.Comp.NextPrayer)
        {
            _popup.PopupEntity(Loc.GetString("warlock-shrine-on-cooldown"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        var symbol = GetHeldSymbol(args.User);
        var time = ent.Comp.PrayerTime;

        if (symbol is { } held && held.Comp.God == ent.Comp.God)
            time *= held.Comp.TimeMultiplier;

        var doAfter = new DoAfterArgs(EntityManager, args.User, time, new WarlockPrayerDoAfterEvent(), ent.Owner, target: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            // Руки нужны свободными не обязательно, но отвлекаться нельзя.
            RequireCanInteract = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString(GetStartMessage(ent.Comp.God)), ent, args.User);
    }

    private void OnPrayerFinished(Entity<WarlockShrineComponent> ent, ref WarlockPrayerDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var user = args.User;
        var power = 1f;

        // Свой символ усиливает просьбу, чужой — оскорбляет адресата.
        if (GetHeldSymbol(user) is { } symbol)
        {
            power = symbol.Comp.God == ent.Comp.God
                ? symbol.Comp.PowerMultiplier
                : 0.5f;
        }

        ent.Comp.NextPrayer = _timing.CurTime + ent.Comp.Cooldown;

        switch (ent.Comp.God)
        {
            case WarlockGod.Kass:
                AnswerKass(ent, user, power);
                break;

            case WarlockGod.Mechantechion:
                AnswerMechantechion(ent, user, power);
                break;
        }
    }

    /// <summary>
    /// Касс отвечает только тем, в ком есть дар: возвращает псионическую энергию.
    /// </summary>
    private void AnswerKass(Entity<WarlockShrineComponent> ent, EntityUid user, float power)
    {
        if (!HasComp<WarlockPsionicComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("warlock-shrine-kass-no-gift"), ent, user, PopupType.MediumCaution);
            return;
        }

        var restored = _psionics.RestoreEnergy(user, ent.Comp.EnergyRestored * power);

        _popup.PopupEntity(
            Loc.GetString("warlock-shrine-kass-answer", ("amount", restored.Int())),
            ent,
            user,
            PopupType.Medium);
    }

    /// <summary>
    /// Механтехион чинит всё неживое в радиусе. Плоть его не интересует,
    /// поэтому цели с кровеносной системой он пропускает — а вот силиконов лечит.
    /// </summary>
    private void AnswerMechantechion(Entity<WarlockShrineComponent> ent, EntityUid user, float power)
    {
        var amount = ent.Comp.RepairAmount * power;

        var repaired = 0;
        foreach (var target in _lookup.GetEntitiesInRange<DamageableComponent>(Transform(ent).Coordinates, ent.Comp.Radius))
        {
            if (target.Owner == ent.Owner || HasComp<BloodstreamComponent>(target.Owner))
                continue;

            if (target.Comp.TotalDamage <= 0)
                continue;

            var heal = new DamageSpecifier();
            foreach (var (type, value) in target.Comp.Damage.DamageDict)
            {
                if (value <= 0)
                    continue;

                heal.DamageDict[type] = -amount;
            }

            if (heal.Empty)
                continue;

            _damageable.TryChangeDamage(target.Owner, heal, true, origin: ent.Owner);
            repaired++;
        }

        _popup.PopupEntity(
            Loc.GetString("warlock-shrine-mechantechion-answer", ("count", repaired)),
            ent,
            user,
            PopupType.Medium);
    }

    /// <summary>
    /// Ищет культовый символ в руках молящегося.
    /// </summary>
    private Entity<WarlockHolySymbolComponent>? GetHeldSymbol(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (TryComp<WarlockHolySymbolComponent>(held, out var symbol))
                return (held, symbol);
        }

        return null;
    }

    private static string GetStartMessage(WarlockGod god)
    {
        return god switch
        {
            WarlockGod.Mechantechion => "warlock-shrine-mechantechion-start",
            _ => "warlock-shrine-kass-start",
        };
    }
}
