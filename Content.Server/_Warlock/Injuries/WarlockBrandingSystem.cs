using Content.Server.Administration;
using Content.Shared._Warlock.Injuries;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
// ActorComponent живёт в Robust.Shared.Player, а не в Robust.Server.GameObjects,
// как было в старых версиях движка.
using Robust.Shared.Player;

namespace Content.Server._Warlock.Injuries;

/// <summary>
/// _Warlock
/// Клеймение. Единственный способ поставить метку, которая не сойдёт никогда,
/// и единственная травма, которую наносят намеренно, а не в бою.
///
/// У настраиваемого клейма надпись и место выбираются через меню взаимодействия:
/// текст набирается в диалоге, место — отдельным подменю.
/// </summary>
public sealed partial class WarlockBrandingSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private QuickDialogSystem _dialog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private WarlockInjuriesSystem _injuries = default!;
    [Dependency] private WarlockMagicBrandSystem _magic = default!;

    private static readonly SoundPathSpecifier BrandSound = new("/Audio/Items/welder_drop.ogg");

    /// <summary>
    /// Места, куда вообще можно поставить клеймо.
    /// </summary>
    private static readonly WarlockBodyPart[] BrandableParts =
    {
        WarlockBodyPart.Head,
        WarlockBodyPart.Torso,
        WarlockBodyPart.LeftArm,
        WarlockBodyPart.RightArm,
        WarlockBodyPart.LeftLeg,
        WarlockBodyPart.RightLeg,
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockBrandingIronComponent, AfterInteractEvent>(OnAfterInteract);

        // Направленно: DoAfter поднимает событие на EventTarget без broadcast,
        // так что широковещательная подписка сюда просто не дойдёт.
        SubscribeLocalEvent<WarlockBrandingIronComponent, WarlockBrandingDoAfterEvent>(OnBranded);
        SubscribeLocalEvent<WarlockBrandingIronComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<WarlockBrandingIronComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    /// <summary>
    /// Что сейчас выжжет это клеймо: набранный текст, если он есть, иначе фракционный оттиск.
    /// </summary>
    private static string GetBrandText(WarlockBrandingIronComponent comp)
    {
        return string.IsNullOrWhiteSpace(comp.CustomText) ? comp.Brand : comp.CustomText;
    }

    private void OnExamined(Entity<WarlockBrandingIronComponent> ent, ref ExaminedEvent args)
    {
        var text = GetBrandText(ent.Comp);

        args.PushMarkup(Loc.GetString("warlock-branding-examine",
            ("brand", Loc.GetString(text)),
            ("part", Loc.GetString(WarlockInjuriesSystem.GetPartLoc(ent.Comp.TargetPart)))));
    }

    #region Меню взаимодействия

    private void OnGetVerbs(Entity<WarlockBrandingIronComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        // Надпись меняется только у настраиваемого клейма: фракционный оттиск отлит намертво.
        if (ent.Comp.Adjustable && TryComp<ActorComponent>(user, out var actor))
        {
            var session = actor.PlayerSession;

            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("warlock-branding-verb-text"),
                Act = () =>
                {
                    _dialog.OpenDialog<string>(
                        session,
                        Loc.GetString("warlock-branding-dialog-title"),
                        Loc.GetString("warlock-branding-dialog-prompt"),
                        text => SetText(ent, user, text));
                },
            });
        }

        // Место выбирается у любого клейма: куда ставить, решает тот, кто держит железо.
        foreach (var part in BrandableParts)
        {
            var chosen = part;

            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(WarlockInjuriesSystem.GetPartLoc(part)),
                Category = VerbCategory.SelectType,
                Priority = ent.Comp.TargetPart == part ? 1 : 0,
                Act = () =>
                {
                    ent.Comp.TargetPart = chosen;
                    Dirty(ent);

                    _popup.PopupEntity(
                        Loc.GetString("warlock-branding-part-set",
                            ("part", Loc.GetString(WarlockInjuriesSystem.GetPartLoc(chosen)))),
                        ent,
                        user);
                },
            });
        }
    }

    private void SetText(Entity<WarlockBrandingIronComponent> ent, EntityUid user, string text)
    {
        text = text.Trim();

        if (text.Length > ent.Comp.MaxLength)
            text = text[..ent.Comp.MaxLength];

        ent.Comp.CustomText = string.IsNullOrWhiteSpace(text) ? null : text;
        Dirty(ent);

        _popup.PopupEntity(
            Loc.GetString("warlock-branding-text-set", ("brand", Loc.GetString(GetBrandText(ent.Comp)))),
            ent,
            user);
    }

    #endregion

    #region Клеймение

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

        _injuries.AddBrand(target, GetBrandText(ent.Comp), ent.Comp.TargetPart);

        _damageable.TryChangeDamage(
            target,
            new DamageSpecifier { DamageDict = { ["Heat"] = ent.Comp.Burn } },
            origin: args.User);

        _audio.PlayPvs(BrandSound, target);

        _popup.PopupEntity(Loc.GetString("warlock-branding-done"), target, args.User);
        _popup.PopupEntity(Loc.GetString("warlock-branding-done-victim"), target, target, PopupType.LargeCaution);

        if (ent.Comp.Effect == WarlockBrandEffect.None)
            return;

        // Магическое клеймо кладёт приговор поверх надписи. Снять его нельзя ничем:
        // ни хирургией, ни Семенем Рузута, ни смертью с последующим клонированием.
        _magic.Apply(target, ent.Comp.Effect);

        _popup.PopupEntity(
            Loc.GetString(GetEffectMessage(ent.Comp.Effect)),
            target,
            target,
            PopupType.LargeCaution);

        // Заряды считаем только у магических: обычным клеймом можно клеймить хоть весь отсек.
        if (ent.Comp.Uses < 0)
            return;

        ent.Comp.Uses--;

        if (ent.Comp.Uses > 0)
            return;

        _popup.PopupEntity(Loc.GetString("warlock-brand-magic-spent"), target, args.User, PopupType.Medium);
        QueueDel(ent);
    }

    /// <summary>
    /// Что жертва чувствует, когда клеймо оказывается не просто железом.
    /// </summary>
    private static string GetEffectMessage(WarlockBrandEffect effect)
    {
        return effect switch
        {
            WarlockBrandEffect.Shackles => "warlock-brand-shackles-applied",
            WarlockBrandEffect.Roots => "warlock-brand-roots-applied",
            _ => "warlock-brand-ashes-applied",
        };
    }

    #endregion
}
