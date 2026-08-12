using Content.Shared._Warlock.Injuries;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Warlock.Injuries;

/// <summary>
/// _Warlock
/// Клеймение. Единственный способ поставить метку, которая не сойдёт никогда,
/// и единственная травма, которую наносят намеренно, а не в бою.
/// </summary>
public sealed partial class WarlockBrandingSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private WarlockInjuriesSystem _injuries = default!;

    private static readonly SoundPathSpecifier BrandSound = new("/Audio/Items/welder_drop.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockBrandingIronComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<WarlockBrandingIronComponent, WarlockBrandingDoAfterEvent>(OnBranded);
    }

    private void OnAfterInteract(Entity<WarlockBrandingIronComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        // Клеймят живых. На трупе клеймо смысла не имеет, на ящике — тем более.
        if (!HasComp<MobStateComponent>(target) || !HasComp<WarlockInjuriesComponent>(target))
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.Delay,
            new WarlockBrandingDoAfterEvent(),
            ent.Owner,
            target: target,
            used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("warlock-branding-start"), target, args.User);
        _popup.PopupEntity(Loc.GetString("warlock-branding-start-victim"), target, target, PopupType.LargeCaution);
    }

    private void OnBranded(Entity<WarlockBrandingIronComponent> ent, ref WarlockBrandingDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        _injuries.AddBrand(target, ent.Comp.Brand);

        _damageable.TryChangeDamage(
            target,
            new DamageSpecifier { DamageDict = { ["Heat"] = ent.Comp.Burn } },
            origin: args.User);

        _audio.PlayPvs(BrandSound, target);

        _popup.PopupEntity(Loc.GetString("warlock-branding-done"), target, args.User);
        _popup.PopupEntity(Loc.GetString("warlock-branding-done-victim"), target, target, PopupType.LargeCaution);
    }
}
