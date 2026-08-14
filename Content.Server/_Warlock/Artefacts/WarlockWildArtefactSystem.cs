using System.Text;
using Content.Server._Warlock.Artefacts.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Warlock.Artefacts;

/// <summary>
/// _Warlock
/// Дикие реликвии фанатиков Касса и биотехнологии к-хритов.
///
/// Фанатики строили вещи, которые что-то отнимают навсегда: голос, зрение, место в мире.
/// К-хриты строили инструменты улья — они работают, но каждый тянет владельца в сторону
/// насекомого. Ни те, ни другие не рассчитаны на человека, и это чувствуется при первом же
/// применении.
/// </summary>
public sealed partial class WarlockWildArtefactSystem : EntitySystem
{
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly SoundPathSpecifier UseSound = new("/Audio/Magic/staff_chaos.ogg");
    private static readonly SoundPathSpecifier BellSound = new("/Audio/Magic/rumble.ogg");

    /// <summary>
    /// Панцирь, который прививает «Панцирный Нарост». Тот же, что у самого скорпиона:
    /// прививка не улучшенная версия, а буквально кусок скарабея на человеке.
    /// </summary>
    private static readonly ProtoId<DamageModifierSetPrototype> Carapace = "WarlockKhritCarapace";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockKassThroatComponent, UseInHandEvent>(OnThroatUsed);
        SubscribeLocalEvent<WarlockSecondHeartComponent, UseInHandEvent>(OnHeartUsed);
        SubscribeLocalEvent<WarlockSilenceBellComponent, UseInHandEvent>(OnBellUsed);
        SubscribeLocalEvent<WarlockBlindChoirComponent, UseInHandEvent>(OnChoirUsed);
        SubscribeLocalEvent<WarlockSwapSealComponent, UseInHandEvent>(OnSealUsed);

        SubscribeLocalEvent<WarlockGoldenSwarmComponent, UseInHandEvent>(OnSwarmUsed);
        SubscribeLocalEvent<WarlockGoldenSwarmComponent, ExaminedEvent>(OnSwarmExamined);
        SubscribeLocalEvent<WarlockHiveInHandComponent, UseInHandEvent>(OnHiveUsed);
        SubscribeLocalEvent<WarlockAmberEyeComponent, UseInHandEvent>(OnAmberEyeUsed);
        SubscribeLocalEvent<WarlockCarapaceGraftComponent, UseInHandEvent>(OnGraftUsed);

        // Последствия, которые живут на теле, а не на предмете.
        SubscribeLocalEvent<WarlockKassThroatSpeakerComponent, AccentGetEvent>(OnKassAccent);
        SubscribeLocalEvent<WarlockMutedComponent, SpeakAttemptEvent>(OnMutedSpeakAttempt);
    }

    #region Гортань Касса

    private void OnThroatUsed(Entity<WarlockKassThroatComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (HasComp<WarlockKassThroatSpeakerComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("warlock-wild-throat-already"), args.User, args.User);
            return;
        }

        EnsureComp<WarlockKassThroatSpeakerComponent>(args.User);

        _audio.PlayPvs(UseSound, args.User);
        _popup.PopupEntity(Loc.GetString("warlock-wild-throat-taken"), args.User, args.User, PopupType.LargeCaution);

        QueueDel(ent);
    }

    /// <summary>
    /// Слова остаются, язык становится чужим. Согласные сдвигаются по кругу,
    /// гласные тянутся — получается речь, в которой слышно ритм, но не смысл.
    /// </summary>
    private void OnKassAccent(Entity<WarlockKassThroatSpeakerComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Speak(args.Message);
    }

    public static string Speak(string message)
    {
        const string from = "бвгджзклмнпрстфхцчшщ";
        const string to = "квзгбщрмтнфдхлпжцшсчй";

        var sb = new StringBuilder(message.Length);

        foreach (var c in message)
        {
            var lower = char.ToLowerInvariant(c);
            var index = from.IndexOf(lower);

            if (index < 0)
            {
                sb.Append(c);
                continue;
            }

            var replaced = to[index];
            sb.Append(char.IsUpper(c) ? char.ToUpperInvariant(replaced) : replaced);
        }

        return sb.ToString();
    }

    #endregion

    #region Второе Сердце

    private void OnHeartUsed(Entity<WarlockSecondHeartComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!HasComp<MobStateComponent>(args.User))
            return;

        if (HasComp<WarlockSecondHeartHostComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("warlock-wild-heart-already"), args.User, args.User);
            return;
        }

        var host = EnsureComp<WarlockSecondHeartHostComponent>(args.User);
        host.NextTick = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.TickInterval);
        host.TickInterval = ent.Comp.TickInterval;
        host.Mend = ent.Comp.Mend;
        host.Bleed = ent.Comp.Bleed;

        _audio.PlayPvs(UseSound, args.User);
        _popup.PopupEntity(Loc.GetString("warlock-wild-heart-taken"), args.User, args.User, PopupType.LargeCaution);

        QueueDel(ent);
    }

    private void TickSecondHeart(Entity<WarlockSecondHeartHostComponent> ent)
    {
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.TickInterval);

        // Побои закрывает, кровь берёт. В сумме это не лечение, а отсрочка.
        _damageable.TryChangeDamage(
            ent.Owner,
            new DamageSpecifier
            {
                DamageDict =
                {
                    ["Blunt"] = -ent.Comp.Mend,
                    ["Slash"] = -ent.Comp.Mend,
                    ["Piercing"] = -ent.Comp.Mend,
                },
            },
            origin: ent.Owner);

        _damageable.TryChangeDamage(
            ent.Owner,
            new DamageSpecifier { DamageDict = { ["Bloodloss"] = ent.Comp.Bleed } },
            origin: ent.Owner);
    }

    #endregion

    #region Колокол Немоты

    private void OnBellUsed(Entity<WarlockSilenceBellComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var end = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Duration);
        var silenced = 0;

        foreach (var victim in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(ent).Coordinates, ent.Comp.Radius))
        {
            // Звонарь не исключение. Колокол не различает, кто держит верёвку.
            var muted = EnsureComp<WarlockMutedComponent>(victim.Owner);
            muted.EndAt = end;
            silenced++;

            _popup.PopupEntity(Loc.GetString("warlock-wild-bell-victim"), victim.Owner, victim.Owner, PopupType.LargeCaution);
        }

        _audio.PlayPvs(BellSound, ent);
        _popup.PopupEntity(Loc.GetString("warlock-wild-bell-rung", ("count", silenced)), ent, args.User);

        QueueDel(ent);
    }

    private void OnMutedSpeakAttempt(Entity<WarlockMutedComponent> ent, ref SpeakAttemptEvent args)
    {
        args.Cancel();
    }

    #endregion

    #region Слепой Хор

    private void OnChoirUsed(Entity<WarlockBlindChoirComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        foreach (var victim in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(ent).Coordinates, ent.Comp.Radius))
        {
            if (_mobState.IsDead(victim.Owner))
                continue;

            // Закрытое лицо помогает вдвое. Фанатики строили это против непокрытых голов,
            // и Братство в глухих шлемах пережило бы такую вспышку заметно легче.
            var covered = _inventory.TryGetSlotEntity(victim.Owner, "head", out _)
                || _inventory.TryGetSlotEntity(victim.Owner, "mask", out _);

            var damage = covered ? ent.Comp.EyeDamage / 2 : ent.Comp.EyeDamage;

            _blindable.AdjustEyeDamage(victim.Owner, damage);

            _popup.PopupEntity(
                Loc.GetString(covered ? "warlock-wild-choir-shielded" : "warlock-wild-choir-blinded"),
                victim.Owner,
                victim.Owner,
                PopupType.LargeCaution);
        }

        _audio.PlayPvs(UseSound, ent);
        QueueDel(ent);
    }

    #endregion

    #region Печать Обмена

    private void OnSealUsed(Entity<WarlockSwapSealComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        var candidates = new List<EntityUid>();
        var query = EntityQueryEnumerator<MobStateComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            if (uid == user || _mobState.IsDead(uid) || Transform(uid).Anchored)
                continue;

            candidates.Add(uid);
        }

        if (candidates.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("warlock-wild-seal-alone"), user, user, PopupType.MediumCaution);
            return;
        }

        args.Handled = true;

        var target = _random.Pick(candidates);

        var here = Transform(user).Coordinates;
        var there = Transform(target).Coordinates;

        _transform.SetCoordinates(user, there);
        _transform.SetCoordinates(target, here);

        _audio.PlayPvs(UseSound, user);

        _popup.PopupEntity(Loc.GetString("warlock-wild-seal-used"), user, user, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("warlock-wild-seal-victim"), target, target, PopupType.LargeCaution);

        QueueDel(ent);
    }

    #endregion

    #region Золотой Рой

    private void OnSwarmExamined(Entity<WarlockGoldenSwarmComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("warlock-khrit-swarm-examine", ("uses", ent.Comp.Uses)));
    }

    private void OnSwarmUsed(Entity<WarlockGoldenSwarmComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Uses <= 0)
            return;

        args.Handled = true;
        ent.Comp.Uses--;

        Spawn(ent.Comp.Scarab, Transform(args.User).Coordinates);

        _audio.PlayPvs(UseSound, args.User);
        _popup.PopupEntity(Loc.GetString("warlock-khrit-swarm-woken"), args.User, args.User, PopupType.LargeCaution);

        if (ent.Comp.Uses > 0)
            return;

        _popup.PopupEntity(Loc.GetString("warlock-khrit-swarm-empty"), args.User, args.User);
        QueueDel(ent);
    }

    #endregion

    #region Улей в Ладони

    private void OnHiveUsed(Entity<WarlockHiveInHandComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;

        // Улей не лечит, улей перераспределяет. Без чужого тела рядом делиться нечем.
        EntityUid? victim = null;
        var closest = float.MaxValue;
        var origin = _transform.GetWorldPosition(user);

        foreach (var candidate in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(user).Coordinates, ent.Comp.Radius))
        {
            if (candidate.Owner == user || _mobState.IsDead(candidate.Owner))
                continue;

            var distance = (_transform.GetWorldPosition(candidate.Owner) - origin).Length();

            if (distance >= closest)
                continue;

            closest = distance;
            victim = candidate.Owner;
        }

        if (victim is not { } target)
        {
            _popup.PopupEntity(Loc.GetString("warlock-khrit-hive-alone"), user, user, PopupType.MediumCaution);
            return;
        }

        args.Handled = true;

        _damageable.HealEvenly(user, -ent.Comp.Amount, origin: ent.Owner);

        _damageable.TryChangeDamage(
            target,
            new DamageSpecifier { DamageDict = { ["Cellular"] = ent.Comp.Amount } },
            origin: user);

        _audio.PlayPvs(UseSound, user);

        _popup.PopupEntity(Loc.GetString("warlock-khrit-hive-used"), user, user, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("warlock-khrit-hive-victim"), target, target, PopupType.LargeCaution);
    }

    #endregion

    #region Янтарный Глаз

    private void OnAmberEyeUsed(Entity<WarlockAmberEyeComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var user = args.User;
        var origin = _transform.GetWorldPosition(user);
        var count = 0;
        var closest = float.MaxValue;
        EntityUid? nearest = null;

        foreach (var candidate in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(user).Coordinates, ent.Comp.Radius))
        {
            if (candidate.Owner == user || _mobState.IsDead(candidate.Owner))
                continue;

            count++;

            var distance = (_transform.GetWorldPosition(candidate.Owner) - origin).Length();

            if (distance >= closest)
                continue;

            closest = distance;
            nearest = candidate.Owner;
        }

        _audio.PlayPvs(UseSound, user);

        if (nearest is not { } target)
        {
            _popup.PopupEntity(Loc.GetString("warlock-khrit-eye-empty"), user, user);
            return;
        }

        var direction = GetDirectionName(_transform.GetWorldPosition(target) - origin);

        _popup.PopupEntity(
            Loc.GetString("warlock-khrit-eye-count",
                ("count", count),
                ("direction", Loc.GetString(direction)),
                ("distance", (int) closest)),
            user,
            user,
            PopupType.Medium);
    }

    /// <summary>
    /// Восьмушка круга словами. Отдельная копия, а не общий помощник: у ритуалов свой,
    /// и связывать две несвязанные системы ради восьми строк не стоит.
    /// </summary>
    private static string GetDirectionName(System.Numerics.Vector2 delta)
    {
        var angle = MathF.Atan2(delta.Y, delta.X);
        var octant = (int) MathF.Round(angle / (MathF.PI / 4f));
        var index = ((octant % 8) + 8) % 8;

        return index switch
        {
            0 => "warlock-direction-east",
            1 => "warlock-direction-northeast",
            2 => "warlock-direction-north",
            3 => "warlock-direction-northwest",
            4 => "warlock-direction-west",
            5 => "warlock-direction-southwest",
            6 => "warlock-direction-south",
            _ => "warlock-direction-southeast",
        };
    }

    #endregion

    #region Панцирный Нарост

    private void OnGraftUsed(Entity<WarlockCarapaceGraftComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var user = args.User;

        if (!HasComp<DamageableComponent>(user))
            return;

        if (HasComp<WarlockCarapaceHostComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("warlock-khrit-graft-already"), user, user);
            return;
        }

        if (!_proto.HasIndex(Carapace))
            return;

        EnsureComp<WarlockCarapaceHostComponent>(user);
        _damageable.SetDamageModifierSetId(user, Carapace);

        _audio.PlayPvs(UseSound, user);
        _popup.PopupEntity(Loc.GetString("warlock-khrit-graft-taken"), user, user, PopupType.LargeCaution);

        QueueDel(ent);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var hearts = EntityQueryEnumerator<WarlockSecondHeartHostComponent>();
        while (hearts.MoveNext(out var uid, out var heart))
        {
            if (now >= heart.NextTick)
                TickSecondHeart((uid, heart));
        }

        var muted = EntityQueryEnumerator<WarlockMutedComponent>();
        while (muted.MoveNext(out var uid, out var mute))
        {
            if (now < mute.EndAt)
                continue;

            RemCompDeferred<WarlockMutedComponent>(uid);
            _popup.PopupEntity(Loc.GetString("warlock-wild-bell-over"), uid, uid);
        }
    }
}
