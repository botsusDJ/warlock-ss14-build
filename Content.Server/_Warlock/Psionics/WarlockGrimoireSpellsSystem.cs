using System.Linq;
using System.Numerics;
using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Server._Warlock.Guilds;
using Content.Server._Warlock.Psionics.Components;
using Content.Shared._Warlock.Objectives;
using Content.Shared._Warlock.Psionics.Components;
using Content.Shared._Warlock.Psionics.Events;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Warlock.Psionics;

/// <summary>
/// _Warlock
/// Третий круг — то, что покупают в гримуаре гильдии, а не получают с рождения.
///
/// Здесь намеренно нет ничего, что просто наносит больше урона, чем предыдущее.
/// Каждое заклинание умеет ровно одну вещь, которую другими средствами не сделать:
/// узнать чужую смерть, докричаться через полстанции, поменяться местами с врагом,
/// заглушить рацию. Это инструменты, а не ступеньки силы.
/// </summary>
public sealed partial class WarlockGrimoireSpellsSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private QuickDialogSystem _dialog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private WarlockGuildSystem _guilds = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockDustReadingEvent>(OnDustReading);
        SubscribeLocalEvent<WarlockGuildVoiceEvent>(OnGuildVoice);
        SubscribeLocalEvent<WarlockBeckonEvent>(OnBeckon);
        SubscribeLocalEvent<WarlockRepulseWaveEvent>(OnRepulseWave);
        SubscribeLocalEvent<WarlockGuildMarkEvent>(OnGuildMark);
        SubscribeLocalEvent<WarlockSilenceCircleEvent>(OnSilenceCircle);
        SubscribeLocalEvent<WarlockSwapPlacesEvent>(OnSwapPlaces);
        SubscribeLocalEvent<WarlockGlassSkinEvent>(OnGlassSkin);
        SubscribeLocalEvent<WarlockVoidSpearEvent>(OnVoidSpear);
        SubscribeLocalEvent<WarlockImplosionEvent>(OnImplosion);

        SubscribeLocalEvent<WarlockGlassSkinComponent, DamageModifyEvent>(OnGlassSkinDamage);
        SubscribeLocalEvent<WarlockGlassSkinComponent, UpdateCanMoveEvent>(OnGlassSkinMove);
    }

    #region Ролевые

    /// <summary>
    /// Читает по телу или вещи последнее, что с ней случилось. Только информация,
    /// зато та, которую живой обычно не выдаёт.
    /// </summary>
    private void OnDustReading(WarlockDustReadingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var performer = args.Performer;
        var target = args.Target;

        _audio.PlayPvs(args.Sound, performer);

        if (!TryComp<DamageableComponent>(target, out _))
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-dust-silent"), performer, performer);
            return;
        }

        // Самый крупный вид полученного урона и есть история этой вещи или тела.
        //
        // Перебираем руками, а не через LINQ: ключ DamageDict — это ProtoId<DamageTypePrototype>,
        // невыводимая структура. FirstOrDefault вернул бы не null, а пустой ProtoId, и «чистое»
        // тело было бы не отличить от тела с уроном нулевого типа.
        var damage = _damageable.GetAllDamage((target, null));
        string? worst = null;
        var best = 0f;

        foreach (var (type, value) in damage.DamageDict)
        {
            var amount = value.Float();

            if (amount <= best)
                continue;

            best = amount;
            worst = type.Id;
        }

        if (worst == null)
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-dust-clean"), performer, performer, PopupType.Medium);
            return;
        }

        // Виды урона в билде может добавить кто угодно, а строку под каждый мы не заводим.
        // Если ключа нет, Loc возвращает сам ключ — на это и проверяем, чтобы игрок
        // увидел «что-то, чему нет названия», а не сырой warlock-damage-чтототам.
        var key = $"warlock-damage-{worst.ToLowerInvariant()}";
        var kind = Loc.GetString(key);

        if (kind == key)
            kind = Loc.GetString("warlock-damage-unknown");

        _popup.PopupEntity(
            Loc.GetString("warlock-spell-dust-read", ("kind", kind)),
            performer,
            performer,
            PopupType.Medium);
    }

    /// <summary>
    /// Одна фраза всем своим по гильдии, куда бы их ни занесло.
    /// </summary>
    private void OnGuildVoice(WarlockGuildVoiceEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;

        if (_guilds.GetGuildOf(performer) is not { } guild)
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-voice-no-guild"), performer, performer, PopupType.MediumCaution);
            return;
        }

        if (!_players.TryGetSessionByEntity(performer, out var session))
            return;

        args.Handled = true;

        _dialog.OpenDialog<string>(
            session,
            Loc.GetString("warlock-spell-voice-title"),
            Loc.GetString("warlock-spell-voice-prompt"),
            text => Broadcast(performer, guild, text, args));
    }

    private void Broadcast(EntityUid performer, WarlockFaction guild, string text, WarlockGuildVoiceEvent args)
    {
        text = text.Trim();

        if (string.IsNullOrWhiteSpace(text) || TerminatingOrDeleted(performer))
            return;

        if (text.Length > args.MaxLength)
            text = text[..args.MaxLength];

        var wrapped = Loc.GetString(
            "warlock-spell-voice-message",
            ("name", Name(performer)),
            ("message", text));

        // Слышат только свои по гильдии — и только те, кто сейчас в теле.
        var listeners = _guilds.GetGuildSessions(guild).ToList();

        foreach (var listener in listeners)
        {
            _chat.ChatMessageToOne(
                ChatChannel.Local,
                text,
                wrapped,
                performer,
                false,
                listener.Channel);
        }

        _audio.PlayPvs(args.Sound, performer);
    }

    #endregion

    #region Телекинетические

    /// <summary>
    /// Тянет предмет из точки клика прямо в свободную руку.
    /// </summary>
    private void OnBeckon(WarlockBeckonEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;

        if (FindLooseItem(performer, args.Target, args.Radius) is not { } item)
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-beckon-nothing"), performer, performer, PopupType.MediumCaution);
            return;
        }

        args.Handled = true;

        if (!_hands.TryPickupAnyHand(performer, item))
        {
            // Рук нет или заняты — тогда просто подтащим к ногам.
            var from = _transform.GetMapCoordinates(item);
            var to = _transform.GetMapCoordinates(performer);

            if (from.MapId == to.MapId)
                _throwing.TryThrow(item, to.Position - from.Position, 8f, performer);

            _popup.PopupEntity(Loc.GetString("warlock-spell-beckon-no-hand"), performer, performer);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-beckon-pull"), performer, performer);
        }

        _audio.PlayPvs(args.Sound, performer);
    }

    /// <summary>
    /// Ищет в точке клика предмет, который вообще можно поднять.
    /// </summary>
    private EntityUid? FindLooseItem(EntityUid performer, EntityCoordinates coords, float radius)
    {
        foreach (var candidate in _lookup.GetEntitiesInRange(coords, radius))
        {
            if (candidate == performer || TerminatingOrDeleted(candidate))
                continue;

            if (!HasComp<ItemComponent>(candidate) || HasComp<MobStateComponent>(candidate))
                continue;

            if (Transform(candidate).Anchored || _container.IsEntityInContainer(candidate))
                continue;

            return candidate;
        }

        return null;
    }

    /// <summary>
    /// Расталкивает всё незакреплённое вокруг техномага.
    /// </summary>
    private void OnRepulseWave(WarlockRepulseWaveEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var performer = args.Performer;
        var origin = _transform.GetMapCoordinates(performer);

        foreach (var victim in _lookup.GetEntitiesInRange(Transform(performer).Coordinates, args.Radius))
        {
            if (victim == performer || TerminatingOrDeleted(victim))
                continue;

            if (Transform(victim).Anchored || !HasComp<PhysicsComponent>(victim))
                continue;

            if (_container.IsEntityInContainer(victim))
                continue;

            var here = _transform.GetMapCoordinates(victim);

            if (here.MapId != origin.MapId)
                continue;

            var direction = here.Position - origin.Position;

            // Стоящий вплотную получает случайное направление, иначе делить на ноль.
            if (direction.LengthSquared() < 0.01f)
                direction = new Vector2(1f, 0f);

            _throwing.TryThrow(victim, direction, args.Strength, performer);
        }

        _audio.PlayPvs(args.Sound, performer);
        _popup.PopupEntity(Loc.GetString("warlock-spell-repulse"), performer, performer);
    }

    #endregion

    #region Стратегические

    /// <summary>
    /// Ставит знак гильдии и сообщает своим, что он появился.
    /// </summary>
    private void OnGuildMark(WarlockGuildMarkEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var performer = args.Performer;
        Spawn(args.Mark, args.Target);

        _audio.PlayPvs(args.Sound, performer);
        _popup.PopupEntity(Loc.GetString("warlock-spell-mark-placed"), performer, performer);

        if (_guilds.GetGuildOf(performer) is not { } guild)
            return;

        var wrapped = Loc.GetString("warlock-spell-mark-message", ("name", Name(performer)));

        foreach (var listener in _guilds.GetGuildSessions(guild).ToList())
        {
            _chat.ChatMessageToOne(
                ChatChannel.Local,
                wrapped,
                wrapped,
                performer,
                false,
                listener.Channel);
        }
    }

    /// <summary>
    /// Ставит пятно, внутри которого не работают рации.
    /// </summary>
    private void OnSilenceCircle(WarlockSilenceCircleEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        Spawn(args.Circle, args.Target);

        _audio.PlayPvs(args.Sound, args.Performer);
        _popup.PopupEntity(Loc.GetString("warlock-spell-silence"), args.Performer, args.Performer);
    }

    #endregion

    #region Тактические

    /// <summary>
    /// Меняет техномага и цель местами.
    /// </summary>
    private void OnSwapPlaces(WarlockSwapPlacesEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;
        var target = args.Target;

        if (target == performer || Transform(target).Anchored)
        {
            _popup.PopupEntity(Loc.GetString("warlock-spell-swap-invalid"), performer, performer, PopupType.MediumCaution);
            return;
        }

        args.Handled = true;

        var here = Transform(performer).Coordinates;
        var there = Transform(target).Coordinates;

        _transform.SetCoordinates(performer, there);
        _transform.SetCoordinates(target, here);

        _audio.PlayPvs(args.Sound, performer);

        _popup.PopupEntity(Loc.GetString("warlock-spell-swap"), performer, performer);
        _popup.PopupEntity(Loc.GetString("warlock-spell-swap-victim"), target, target, PopupType.MediumCaution);
    }

    /// <summary>
    /// Несколько секунд неуязвимости в обмен на полную неподвижность.
    /// </summary>
    private void OnGlassSkin(WarlockGlassSkinEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var performer = args.Performer;
        var skin = EnsureComp<WarlockGlassSkinComponent>(performer);
        skin.EndAt = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);

        _audio.PlayPvs(args.Sound, performer);
        _popup.PopupEntity(Loc.GetString("warlock-spell-glass-skin"), performer, performer, PopupType.Medium);
    }

    private void OnGlassSkinDamage(Entity<WarlockGlassSkinComponent> ent, ref DamageModifyEvent args)
    {
        // Пока стоит — не пробить ничем. Вся суть в том, что за это время нельзя уйти.
        args.Damage *= 0f;
    }

    private void OnGlassSkinMove(EntityUid uid, WarlockGlassSkinComponent comp, UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    #endregion

    #region Атакующие

    /// <summary>
    /// Прокол на расстоянии. Бьёт одного и почти не замечает брони.
    /// </summary>
    private void OnVoidSpear(WarlockVoidSpearEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _damageable.TryChangeDamage(
            args.Target,
            new DamageSpecifier { DamageDict = { ["Piercing"] = args.Damage } },
            ignoreResistances: true,
            origin: args.Performer);

        _audio.PlayPvs(args.Sound, args.Target);
        _popup.PopupEntity(Loc.GetString("warlock-spell-spear-hit"), args.Target, args.Target, PopupType.LargeCaution);
    }

    /// <summary>
    /// Стаскивает всё живое к точке и мнёт. Слабее прямого удара, но по всем сразу.
    /// </summary>
    private void OnImplosion(WarlockImplosionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var performer = args.Performer;
        var center = _transform.ToMapCoordinates(args.Target);
        var crush = new DamageSpecifier { DamageDict = { ["Blunt"] = args.Damage } };

        foreach (var victim in _lookup.GetEntitiesInRange(args.Target, args.Radius))
        {
            if (TerminatingOrDeleted(victim) || Transform(victim).Anchored)
                continue;

            if (!HasComp<PhysicsComponent>(victim) || _container.IsEntityInContainer(victim))
                continue;

            var here = _transform.GetMapCoordinates(victim);

            if (here.MapId != center.MapId)
                continue;

            var direction = center.Position - here.Position;

            if (direction.LengthSquared() >= 0.01f)
                _throwing.TryThrow(victim, direction, args.PullStrength, performer, playSound: false);

            if (HasComp<MobStateComponent>(victim))
                _damageable.TryChangeDamage(victim, crush, origin: performer);
        }

        _audio.PlayPvs(args.Sound, performer);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WarlockGlassSkinComponent>();

        while (query.MoveNext(out var uid, out var skin))
        {
            if (now < skin.EndAt)
                continue;

            RemCompDeferred<WarlockGlassSkinComponent>(uid);
            _popup.PopupEntity(Loc.GetString("warlock-spell-glass-skin-end"), uid, uid);
        }
    }
}
