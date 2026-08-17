using Content.Shared._Warlock.Exosuits;
using Content.Shared._Warlock.Injuries;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
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
    [Dependency] private DamageableSystem _damageable = default!;
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
        SubscribeLocalEvent<WarlockInjuriesComponent, WarlockLimbTearDoAfterEvent>(OnTorn);
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

        // Рама на руках превращает пытку в казнь: то же меню, но конечность отрывают.
        // Обе ветки живут в одной системе намеренно — пара
        // WarlockInjuriesComponent + GetVerbsEvent<AlternativeVerb> допускает одну
        // подписку на весь билд, и разносить их по системам нельзя.
        var tearing = TryComp<WarlockExosuitWearerComponent>(user, out var exo)
                      && exo.TearStrength > 0f;

        foreach (var part in Breakable)
        {
            // Копия в локальную переменную: лямбда захватывает переменную, а не значение,
            // и без этого все четыре пункта ломали бы последнюю конечность из списка.
            var target = part;
            var partName = Loc.GetString(WarlockInjuriesSystem.GetPartLoc(target));

            args.Verbs.Add(new AlternativeVerb
            {
                Text = partName,
                Category = VerbCategory.SelectType,
                Act = () => Start(ent, user, target),
            });

            if (!tearing)
                continue;

            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("warlock-limb-tear-verb", ("part", partName)),
                Category = VerbCategory.SelectType,
                Act = () => StartTear(ent, user, target),
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

    #region Отрывание

    /// <summary>
    /// Сколько тянут конечность. Быстрее, чем ломают: рама делает это рывком,
    /// а не выкручиванием. Отбить жертву всё ещё можно, но времени вдвое меньше.
    /// </summary>
    private const float TearDelay = 3f;

    private void StartTear(Entity<WarlockInjuriesComponent> ent, EntityUid user, WarlockBodyPart part)
    {
        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            TearDelay,
            new WarlockLimbTearDoAfterEvent(part),
            ent.Owner,
            target: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameTarget,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        var partName = Loc.GetString(WarlockInjuriesSystem.GetPartLoc(part));
        _popup.PopupEntity(Loc.GetString("warlock-limb-tear-start", ("part", partName)), ent, user);
        _popup.PopupEntity(Loc.GetString("warlock-limb-tear-start-victim"), ent, ent, PopupType.LargeCaution);
    }

    private void OnTorn(Entity<WarlockInjuriesComponent> ent, ref WarlockLimbTearDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var partName = Loc.GetString(WarlockInjuriesSystem.GetPartLoc(args.Part));

        // Рама могла сесть или перегреться за те три секунды, что шёл рывок.
        if (!TryComp<WarlockExosuitWearerComponent>(args.User, out var exo) || exo.TearStrength <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("warlock-limb-tear-nopower"), ent, args.User);
            return;
        }

        // Сопротивление жертвы. Целая конечность держится куда крепче уже сломанной:
        // добить надломленную руку проще, чем оторвать здоровую, и это единственный
        // надёжный способ провернуть отрыв малой рамой.
        var resistance = 1.4f;
        if (_injuries.Count(ent.Owner, WarlockInjuryType.Fracture) > 0)
            resistance -= 0.5f;

        _audio.PlayPvs(BreakSound, ent);

        if (exo.TearStrength < resistance)
        {
            // Силы не хватило. Конечность не отрывается, но ломается — рывок
            // впустую не проходит, и вторая попытка будет заметно легче.
            _injuries.TryAddInjury(ent.Owner, WarlockInjuryType.Fracture, args.Part);

            _popup.PopupEntity(Loc.GetString("warlock-limb-tear-fail", ("part", partName)), ent, args.User);
            _popup.PopupEntity(Loc.GetString("warlock-limb-tear-fail-victim", ("part", partName)),
                ent, ent, PopupType.LargeCaution);
            return;
        }

        // Оторвали. В летописи это перелом плюс шрам на том же месте: отдельного
        // «нет конечности» в билде нет, а заводить его ради одного приёма —
        // значит переписать половину системы травм.
        _injuries.TryAddInjury(ent.Owner, WarlockInjuryType.Fracture, args.Part);
        _injuries.TryAddInjury(ent.Owner, WarlockInjuryType.Scar, args.Part);

        _damageable.TryChangeDamage(ent.Owner,
            new DamageSpecifier
            {
                DamageDict =
                {
                    ["Slash"] = 25f,
                    ["Bloodloss"] = 15f,
                },
            },
            origin: args.User);

        _popup.PopupEntity(Loc.GetString("warlock-limb-tear-done", ("part", partName)), ent, args.User);
        _popup.PopupEntity(Loc.GetString("warlock-limb-tear-done-victim", ("part", partName)),
            ent, ent, PopupType.LargeCaution);
    }

    #endregion
}
