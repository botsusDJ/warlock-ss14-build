using System.Linq;
using Content.Server._Warlock.Artefacts.Components;
using Content.Server._Warlock.Guilds;
using Content.Shared._Warlock.Artefacts;
using Content.Shared._Warlock.Pain;
using Content.Shared._Warlock.Psionics;
using Content.Shared._Warlock.Psionics.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Warlock.Artefacts;

/// <summary>
/// _Warlock
/// Реликвии трёх глав гильдий.
///
/// Все три сознательно не бьют. Главе гильдии оружие ни к чему — ему нужно, чтобы
/// гильдия работала, и каждая реликвия снимает с неё ровно то ограничение, которое
/// эту гильдию душит:
///
///   Кадило Касса        — у Варлока кончается резерв, и молиться становится нечем;
///   Око Фактоса         — у Фактоса находки калечат тех, кто их проверяет;
///   Сердце Механтехиона — у Техноса всё стоит на батареях, и батареи садятся.
///
/// И все три берут плату с самого главы, а не с гильдии. Реликвия — не бонус
/// к должности, а обязанность: работает она ровно настолько, насколько глава готов
/// за неё расплатиться собственным телом.
/// </summary>
public sealed partial class WarlockGuildRelicSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private WarlockGuildSystem _guilds = default!;
    [Dependency] private WarlockPainSystem _pain = default!;
    [Dependency] private WarlockPsionicsSystem _psionics = default!;

    private static readonly SoundPathSpecifier CenserSound = new("/Audio/Effects/lightburn.ogg");
    private static readonly SoundPathSpecifier CrumbleSound = new("/Audio/Effects/poster_broken.ogg");

    /// <summary>
    /// Что Око вырезает из находки. Всё остальное остаётся: приручённый камень
    /// не становится бесполезным, он становится безопасным.
    /// </summary>
    private static readonly HashSet<WarlockArtefactEffect> Harmful =
    [
        WarlockArtefactEffect.Bite,
        WarlockArtefactEffect.Drain,
        WarlockArtefactEffect.Flash,
        WarlockArtefactEffect.Break,
        WarlockArtefactEffect.Mark,
        WarlockArtefactEffect.Toss,
        WarlockArtefactEffect.Rot,
        WarlockArtefactEffect.Kindle,
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockCenserComponent, UseInHandEvent>(OnCenserUsed);
        SubscribeLocalEvent<WarlockCenserComponent, ExaminedEvent>(OnCenserExamined);

        SubscribeLocalEvent<WarlockSeersEyeEvent>(OnSeersEye);
        SubscribeLocalEvent<WarlockSeersEyeComponent, ExaminedEvent>(OnEyeExamined);

        SubscribeLocalEvent<WarlockMachineHeartEvent>(OnMachineHeart);
    }

    #region Кадило Касса

    /// <summary>
    /// Зажечь или погасить. Своим переключателем, а не ванильным ItemToggle:
    /// кадило должно зажигаться одним щелчком в руке и ничем больше — ни осмотром,
    /// ни надеванием, ни открытием какой-нибудь панели.
    /// </summary>
    // По значению и в старой форме: UseInHandEvent — обычный класс, а не ByRefEvent,
    // и ваниль слушает его именно так. Несовпадение формы падает при старте сервера.
    private void OnCenserUsed(EntityUid uid, WarlockCenserComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        comp.Lit = !comp.Lit;
        comp.NextTick = _timing.CurTime;

        _audio.PlayPvs(CenserSound, uid);
        _popup.PopupEntity(
            Loc.GetString(comp.Lit ? "warlock-censer-lit" : "warlock-censer-out"),
            uid,
            args.User,
            PopupType.Medium);
    }

    private void OnCenserExamined(Entity<WarlockCenserComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Lit ? "warlock-censer-examine-lit" : "warlock-censer-examine-out"));
    }

    /// <summary>
    /// Такт воскурения.
    ///
    /// Обслуживаются только свои по гильдии — кадило не лечит чужих и не лечит зверьё.
    /// Епископ обслуживается тоже, но за себя не платит: платить приходится ровно
    /// за паству, и толпа вокруг кадила съедает его быстрее, чем одиночка.
    /// </summary>
    private void Cense(Entity<WarlockCenserComponent> ent, EntityUid bearer)
    {
        var guild = _guilds.GetGuildOf(bearer);

        if (guild is not { } faction)
            return;

        var served = 0;
        var coords = Transform(ent.Owner).Coordinates;

        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(coords, ent.Comp.Range))
        {
            if (_mobState.IsDead(target.Owner) || _guilds.GetGuildOf(target.Owner) != faction)
                continue;

            _psionics.RestoreEnergy(target.Owner, ent.Comp.Energy);
            _pain.AddPain(target.Owner, -ent.Comp.Pain);

            if (ent.Comp.Airloss > 0f)
            {
                _damageable.TryChangeDamage(
                    target.Owner,
                    new DamageSpecifier { DamageDict = { ["Asphyxiation"] = -ent.Comp.Airloss } },
                    origin: bearer);
            }

            served++;
        }

        if (served <= 1)
            return;

        _damageable.TryChangeDamage(
            bearer,
            new DamageSpecifier { DamageDict = { ["Cellular"] = ent.Comp.CostPerHead * (served - 1) } },
            ignoreResistances: true,
            origin: bearer);
    }

    #endregion

    #region Око Фактоса

    private void OnEyeExamined(Entity<WarlockSeersEyeComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("warlock-eye-examine", ("charges", ent.Comp.Charges)));
    }

    /// <summary>
    /// Прочесть находку насквозь и вырезать из неё вредное.
    ///
    /// Правка идёт по самому камню, а не по тому, кто его держит: приручение общее
    /// и постоянное. Фактос может пустить обезвреженную находку по рукам, и это ровно
    /// та роль, которую гильдия себе приписывает.
    /// </summary>
    private void OnSeersEye(WarlockSeersEyeEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;

        if (!TryComp<WarlockRandomArtefactComponent>(args.Target, out var relic))
        {
            _popup.PopupEntity(Loc.GetString("warlock-eye-not-a-relic"), performer, performer);
            return;
        }

        // Реликвия у нас в руках или на поясе — ищем её на самом заклинателе.
        if (!TryFindEye(performer, out var eye))
            return;

        args.Handled = true;

        var before = relic.Effects.Count;
        relic.Effects.RemoveAll(Harmful.Contains);
        var cut = before - relic.Effects.Count;

        // Кулдаун сбрасывается: прочитанный камень сразу готов к делу.
        relic.NextUse = _timing.CurTime;
        Dirty(args.Target, relic);

        var left = relic.Effects.Count == 0
            ? Loc.GetString("warlock-eye-empty")
            : string.Join(", ", relic.Effects.Select(e =>
                Loc.GetString($"warlock-artefact-effect-{e.ToString().ToLowerInvariant()}")));

        _audio.PlayPvs(args.Sound, performer);
        _popup.PopupEntity(
            Loc.GetString("warlock-eye-read", ("cut", cut), ("left", left)),
            performer,
            performer,
            PopupType.Medium);

        // Смотреть сквозь камень приходится своими глазами.
        _damageable.TryChangeDamage(
            performer,
            new DamageSpecifier { DamageDict = { ["Cellular"] = eye.Comp.Cost } },
            ignoreResistances: true,
            origin: performer);

        eye.Comp.Charges--;

        if (eye.Comp.Charges > 0)
            return;

        _audio.PlayPvs(CrumbleSound, performer);
        _popup.PopupEntity(Loc.GetString("warlock-eye-blind"), performer, performer, PopupType.LargeCaution);
        QueueDel(eye.Owner);
    }

    /// <summary>
    /// Найти Око на заклинателе. Действие выдаётся предметом, но само по себе
    /// о предмете не знает: заряды считает предмет, и его надо отыскать.
    /// </summary>
    private bool TryFindEye(EntityUid performer, out Entity<WarlockSeersEyeComponent> eye)
    {
        eye = default;

        var query = EntityQueryEnumerator<WarlockSeersEyeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var xform = Transform(uid);

            // Достаточно того, что Око где-то на заклинателе: в руке, в слоте, в сумке.
            for (var parent = xform.ParentUid; parent.IsValid(); parent = Transform(parent).ParentUid)
            {
                if (parent != performer)
                    continue;

                eye = (uid, comp);
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Сердце Механтехиона

    /// <summary>
    /// Разряд питания. Всё, что держит заряд, заряжается досуха; машины заодно
    /// подлатываются. Плата — ток через самого архитехномага, по счётчику: чем больше
    /// зарядил, тем сильнее приложило.
    /// </summary>
    private void OnMachineHeart(WarlockMachineHeartEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var performer = args.Performer;

        if (!TryFindHeart(performer, out var heart))
            return;

        var coords = Transform(performer).Coordinates;
        var charged = 0;

        foreach (var cell in _lookup.GetEntitiesInRange<BatteryComponent>(coords, heart.Comp.Range))
        {
            if (_battery.GetCharge(cell.Owner) >= cell.Comp.MaxCharge)
                continue;

            _battery.SetCharge(cell.Owner, cell.Comp.MaxCharge);
            charged++;
        }

        // Живое Сердце не чинит: оно работает с железом, а не с мясом.
        foreach (var machine in _lookup.GetEntitiesInRange<DamageableComponent>(coords, heart.Comp.Range))
        {
            if (HasComp<MobStateComponent>(machine.Owner))
                continue;

            _damageable.HealEvenly(machine.Owner, -heart.Comp.Repair, origin: performer);
        }

        _audio.PlayPvs(args.Sound, performer);
        _popup.PopupEntity(
            Loc.GetString("warlock-heart-discharge", ("count", charged)),
            performer,
            performer,
            PopupType.Medium);

        if (charged == 0)
            return;

        _damageable.TryChangeDamage(
            performer,
            new DamageSpecifier { DamageDict = { ["Shock"] = heart.Comp.ShockPerCell * charged } },
            origin: performer);
    }

    private bool TryFindHeart(EntityUid performer, out Entity<WarlockMachineHeartComponent> heart)
    {
        heart = default;

        var query = EntityQueryEnumerator<WarlockMachineHeartComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var xform = Transform(uid);

            for (var parent = xform.ParentUid; parent.IsValid(); parent = Transform(parent).ParentUid)
            {
                if (parent != performer)
                    continue;

                heart = (uid, comp);
                return true;
            }
        }

        return false;
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WarlockCenserComponent>();

        while (query.MoveNext(out var uid, out var censer))
        {
            if (!censer.Lit || now < censer.NextTick)
                continue;

            censer.NextTick = now + TimeSpan.FromSeconds(censer.TickInterval);

            // Кадило работает только в руках у живого: брошенное на пол оно коптит впустую.
            var bearer = Transform(uid).ParentUid;

            if (!bearer.IsValid() || !HasComp<MobStateComponent>(bearer) || _mobState.IsDead(bearer))
                continue;

            Cense((uid, censer), bearer);
        }
    }
}
