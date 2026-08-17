using System.Linq;
using Content.Shared._Warlock.Injuries;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.Standing;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Warlock.Pain;

/// <summary>
/// _Warlock
/// Боль.
///
/// Ванильная боёвка знает только «цел» и «выключен»: пока полоска не кончилась,
/// боец в полном порядке, а на нуле мгновенно падает. Промежутка между этими
/// состояниями там нет, и бой сводится к гонке полосок.
///
/// Боль — это и есть тот промежуток. Она копится от урона и от переломов, спадает
/// сама и по дороге отбирает ровно то, чем боец воюет: скорость, силу удара,
/// внятную речь, способность держать предмет. Раненый не выключается, он
/// становится хуже — и решает, отступить или дожать.
///
/// Боль сознательно НЕ убивает и не роняет в крит. Это не вторая полоска здоровья,
/// а модификатор к возможностям. Умирают по-прежнему от урона.
///
/// Что где считается:
///   спад            — каждый кадр, плавно;
///   переломы, кубики — раз в TickInterval, чтобы не бросать их шестьдесят раз в секунду;
///   скорость        — по запросу движка, на смене ступени;
///   сила удара      — в WarlockAttackStrengthSystem, потому что пара
///                     MeleeWeaponComponent + GetMeleeDamageEvent допускает
///                     ровно одну подписку на весь билд.
/// </summary>
public sealed partial class WarlockPainSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockPainComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<WarlockPainComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<WarlockPainComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<WarlockPainComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<WarlockPainComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<WarlockPainComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    #region Учёт

    private void OnMapInit(Entity<WarlockPainComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextTick = _timing.CurTime;
        UpdateAlert(ent);
    }

    private void OnShutdown(Entity<WarlockPainComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
    }

    private void OnMobStateChanged(Entity<WarlockPainComponent> ent, ref MobStateChangedEvent args)
    {
        // Мёртвым не больно. Обнуляем, иначе поднятый дефибриллятором встаёт
        // сразу в затмении и тут же падает обратно.
        if (args.NewMobState == MobState.Dead)
            SetPain(ent, 0f);
    }

    private void OnDamageChanged(Entity<WarlockPainComponent> ent, ref DamageChangedEvent args)
    {
        // Лечение боли не снимает: заживить рану быстрее, чем она перестанет
        // болеть, — это нормально и даёт бинтам смысл, отличный от аптечки.
        if (args.DamageDelta is not { } delta || !args.DamageIncreased)
            return;

        var add = 0f;
        foreach (var (type, amount) in delta.DamageDict)
        {
            if (amount <= 0)
                continue;
            add += amount.Float() * (ent.Comp.PerDamage.TryGetValue(type.Id, out var w) ? w : 0.5f);
        }

        if (add > 0f)
            AddPain(ent.Owner, add);
    }

    /// <summary>
    /// Добавить боли с учётом чувствительности носителя.
    /// </summary>
    public void AddPain(Entity<WarlockPainComponent?> ent, float amount)
    {
        if (!Resolve(ent, ref ent.Comp, false) || _mobState.IsDead(ent.Owner))
            return;

        SetPain((ent.Owner, ent.Comp), ent.Comp.Pain + amount * ent.Comp.Sensitivity);
    }

    public void SetPain(Entity<WarlockPainComponent> ent, float value)
    {
        var clamped = Math.Clamp(value, 0f, ent.Comp.Max);
        if (MathHelper.CloseTo(clamped, ent.Comp.Pain))
            return;

        ent.Comp.Pain = clamped;

        var level = LevelOf(ent.Comp);
        if (level != ent.Comp.Level)
        {
            var rose = level > ent.Comp.Level;
            ent.Comp.Level = level;
            Announce(ent, rose);
            // Скорость пересчитывается только на смене ступени: дёргать её
            // на каждое очко боли дорого и незачем.
            _movement.RefreshMovementSpeedModifiers(ent.Owner);
        }

        Dirty(ent);
        UpdateAlert(ent);
    }

    public WarlockPainLevel LevelOf(WarlockPainComponent c)
    {
        if (c.Pain >= c.BlackoutAt) return WarlockPainLevel.Blackout;
        if (c.Pain >= c.AgonyAt)    return WarlockPainLevel.Agony;
        if (c.Pain >= c.SharpAt)    return WarlockPainLevel.Sharp;
        if (c.Pain >= c.AcheAt)     return WarlockPainLevel.Ache;
        return WarlockPainLevel.None;
    }

    /// <summary>
    /// Сообщение о переходе. Ровно одно на порог: иначе экран заливает
    /// одинаковыми строками и игрок перестаёт их читать.
    /// </summary>
    private void Announce(Entity<WarlockPainComponent> ent, bool rose)
    {
        if (_net.IsClient)
            return;

        var key = ent.Comp.Level switch
        {
            WarlockPainLevel.Ache => "warlock-pain-ache",
            WarlockPainLevel.Sharp => "warlock-pain-sharp",
            WarlockPainLevel.Agony => "warlock-pain-agony",
            WarlockPainLevel.Blackout => "warlock-pain-blackout",
            _ => "warlock-pain-relief",
        };

        var type = ent.Comp.Level switch
        {
            WarlockPainLevel.Blackout => PopupType.LargeCaution,
            WarlockPainLevel.Agony => PopupType.MediumCaution,
            _ => PopupType.Medium,
        };

        _popup.PopupEntity(Loc.GetString(key), ent.Owner, ent.Owner, type);

        var ev = new WarlockPainLevelChangedEvent(ent.Comp.Level, rose);
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    private void UpdateAlert(Entity<WarlockPainComponent> ent)
    {
        if (ent.Comp.Level == WarlockPainLevel.None)
        {
            _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
            return;
        }

        _alerts.ShowAlert(ent.Owner, ent.Comp.Alert, (short) ent.Comp.Level);
    }

    #endregion

    #region Последствия

    private void OnRefreshSpeed(Entity<WarlockPainComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var k = ent.Comp.Level switch
        {
            WarlockPainLevel.Sharp => ent.Comp.SharpSlow,
            WarlockPainLevel.Agony => ent.Comp.AgonySlow,
            WarlockPainLevel.Blackout => ent.Comp.BlackoutSlow,
            _ => 1f,
        };

        if (k < 1f)
            args.ModifySpeed(k, k);
    }

    private void OnAccent(Entity<WarlockPainComponent> ent, ref AccentGetEvent args)
    {
        // Речь рвётся начиная с агонии. На «режет» человек ещё говорит связно:
        // иначе половина боя превращается в нечитаемый текст.
        if (ent.Comp.Level < WarlockPainLevel.Agony)
            return;

        args.Message = Gasp(args.Message, ent.Comp.Level == WarlockPainLevel.Blackout, _random);
    }

    /// <summary>
    /// Множитель урона в ближнем бою от боли. Вызывается из
    /// WarlockAttackStrengthSystem: пара MeleeWeaponComponent + GetMeleeDamageEvent
    /// уже занята там, а второй подписки на неё движок не допускает.
    /// </summary>
    public float MeleeModifier(EntityUid user)
    {
        if (!TryComp<WarlockPainComponent>(user, out var pain))
            return 1f;

        return pain.Level switch
        {
            WarlockPainLevel.Agony => pain.AgonyDamage,
            WarlockPainLevel.Blackout => pain.BlackoutDamage,
            _ => 1f,
        };
    }

    /// <summary>
    /// Сбить речь на вдохи: в агонии рывками, в затмении почти не говорит.
    /// </summary>
    public static string Gasp(string message, bool heavy, IRobustRandom random)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        var words = message.Split(' ');
        var breakChance = heavy ? 0.55f : 0.28f;
        var gaspChance = heavy ? 0.35f : 0.15f;
        var result = new List<string>(words.Length * 2);

        foreach (var word in words)
        {
            if (word.Length > 3 && random.Prob(breakChance))
            {
                var cut = Math.Max(2, word.Length / 2);
                result.Add(word[..cut] + "-");
                result.Add("..." + word[cut..]);
            }
            else
            {
                result.Add(word);
            }

            if (random.Prob(gaspChance))
                result.Add(heavy ? "...х-ха..." : "...кха");
        }

        return string.Join(' ', result);
    }

    #endregion

    #region Ход времени

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Кубики бросает только сервер: предсказанное на клиенте падение
        // выглядело бы дёрганьем персонажа туда-сюда.
        var server = _net.IsServer;
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<WarlockPainComponent>();
        while (query.MoveNext(out var uid, out var pain))
        {
            if (_mobState.IsDead(uid))
                continue;

            if (pain.Pain > 0f)
                SetPain((uid, pain), pain.Pain - pain.Decay * frameTime);

            if (!server || now < pain.NextTick)
                continue;

            pain.NextTick = now + TimeSpan.FromSeconds(pain.TickInterval);
            Tick((uid, pain));
        }
    }

    private void Tick(Entity<WarlockPainComponent> ent)
    {
        // Переломы болят сами, пока не срослись.
        if (TryComp<WarlockInjuriesComponent>(ent.Owner, out var injuries))
        {
            var fractures = injuries.Injuries.Count(i => i.Type == WarlockInjuryType.Fracture);
            if (fractures > 0)
                AddPain(ent.Owner, fractures * ent.Comp.PerFracture * ent.Comp.TickInterval);
        }

        if (ent.Comp.Level >= WarlockPainLevel.Agony
            && _random.Prob(ent.Comp.DropChance)
            && TryComp<HandsComponent>(ent.Owner, out var hands))
        {
            foreach (var id in _hands.EnumerateHands((ent.Owner, hands)))
            {
                if (!_hands.TryGetHeldItem((ent.Owner, hands), id, out _))
                    continue;

                _hands.TryDrop((ent.Owner, hands), id, checkActionBlocker: false);
                _popup.PopupEntity(Loc.GetString("warlock-pain-drop"), ent.Owner, ent.Owner,
                    PopupType.MediumCaution);
                break;
            }
        }

        if (ent.Comp.Level < WarlockPainLevel.Blackout)
            return;

        _stamina.TakeStaminaDamage(ent.Owner, ent.Comp.BlackoutStamina, source: ent.Owner);

        if (_random.Prob(ent.Comp.FallChance) && !_standing.IsDown(ent.Owner))
        {
            _standing.Down(ent.Owner);
            _popup.PopupEntity(Loc.GetString("warlock-pain-fall"), ent.Owner, ent.Owner,
                PopupType.LargeCaution);
        }
    }

    #endregion
}

/// <summary>
/// _Warlock — боль перешла на другую ступень.
///
/// На это событие вешается всё, что должно случаться в момент перехода, а не
/// постоянно: срыв заклинания у техномага, крик, реакции каст унатхов.
/// </summary>
[ByRefEvent]
public readonly record struct WarlockPainLevelChangedEvent(WarlockPainLevel Level, bool Rose);
