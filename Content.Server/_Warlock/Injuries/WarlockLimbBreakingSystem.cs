using Content.Shared._Warlock.Injuries;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Warlock.Injuries;

/// <summary>
/// _Warlock
/// Намеренный слом конечности. Не боевой приём: ломать можно только того, кто уже лежит
/// и не может встать. Это пытка или казнь, а не способ выиграть драку.
///
/// Ломается ровно то, что выбрали в подменю. Перелом дальше живёт по общим правилам
/// летописи тела: срастается за несколько шагов заживления и оставляет шрам на том же месте.
/// </summary>
public sealed partial class WarlockLimbBreakingSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private WarlockInjuriesSystem _injuries = default!;

    private static readonly SoundPathSpecifier BreakSound = new("/Audio/Effects/metal_crunch.ogg");

    /// <summary>
    /// Сколько ломают. Долго намеренно: жертву должны успеть отбить.
    /// </summary>
    private const float BreakDelay = 6f;

    /// <summary>
    /// Что вообще можно сломать. Торс и голову не ломаем: это уже не травма, а смерть.
    /// </summary>
    private static readonly WarlockBodyPart[] Breakable =
    {
        WarlockBodyPart.LeftArm,
        WarlockBodyPart.RightArm,
        WarlockBodyPart.LeftLeg,
        WarlockBodyPart.RightLeg,
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockInjuriesComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<WarlockInjuriesComponent, WarlockLimbBreakDoAfterEvent>(OnBroken);
    }

    private void OnGetVerbs(Entity<WarlockInjuriesComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User == ent.Owner)
            return;

        // Ломают только лежачему. Стоящий должен сначала оказаться на полу — своими силами
        // или чужими, но драка обязана случиться до пытки, а не вместо неё.
        if (!_standing.IsDown(ent.Owner))
            return;

        var user = args.User;

        foreach (var part in Breakable)
        {
            // Копия в локальную переменную: лямбда захватывает переменную, а не значение,
            // и без этого все четыре пункта ломали бы последнюю конечность из списка.
            var target = part;

            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(WarlockInjuriesSystem.GetPartLoc(target)),
                Category = VerbCategory.SelectType,
                Act = () => Start(ent, user, target),
            });
        }
    }

    private void Start(Entity<WarlockInjuriesComponent> ent, EntityUid user, WarlockBodyPart part)
    {
        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            BreakDelay,
            new WarlockLimbBreakDoAfterEvent(part),
            ent.Owner,
            target: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            // Ломать одну и ту же ногу вдвоём одновременно смысла нет.
            DuplicateCondition = DuplicateConditions.SameTarget,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(
            Loc.GetString("warlock-limb-break-start", ("part", Loc.GetString(WarlockInjuriesSystem.GetPartLoc(part)))),
            ent,
            user);

        _popup.PopupEntity(Loc.GetString("warlock-limb-break-start-victim"), ent, ent, PopupType.LargeCaution);
    }

    private void OnBroken(Entity<WarlockInjuriesComponent> ent, ref WarlockLimbBreakDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        // Потолок переломов общий: если конечностей в летописи уже некуда писать, ничего не выйдет.
        if (!_injuries.TryAddInjury(ent.Owner, WarlockInjuryType.Fracture, args.Part))
            return;

        _audio.PlayPvs(BreakSound, ent);

        var part = Loc.GetString(WarlockInjuriesSystem.GetPartLoc(args.Part));

        _popup.PopupEntity(Loc.GetString("warlock-limb-break-done", ("part", part)), ent, args.User);
        _popup.PopupEntity(Loc.GetString("warlock-limb-break-done-victim", ("part", part)), ent, ent, PopupType.LargeCaution);
    }
}
