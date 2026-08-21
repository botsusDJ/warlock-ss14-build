using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Warlock.Mechs;
using Content.Shared.DoAfter;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Warlock.Mechs;

/// <summary>
/// _Warlock
/// Износ узлов меха: грязь, оплавление, установка и снятие.
///
/// Узел живёт своей жизнью и вне рамы. Брошенная в песок ступня к концу смены забита
/// песком, а лежавшая в горящем ангаре — оплавлена, и ни то ни другое не видно, пока
/// её не поставят. Из-за этого у Братства появляется склад как проблема: детали надо
/// не просто иметь, а держать в чистом сухом месте.
///
/// Два вида износа устроены нарочно по-разному.
///
/// ГРЯЗЬ обратима. Копится от тайла под узлом — песок сильнее всего, обычный грунт
/// вдвое медленнее, камень не пачкает вовсе, потому что грязи в камне нет. Внутри,
/// на металле и бетоне, набегает пыль, но втрое медленнее песка. Стирается ветошью.
///
/// ОПЛАВЛЕНИЕ необратимо. Копится от жара: на раме — когда по ней бьют огнём,
/// вне рамы — когда узел лежит в горячем воздухе. Не чинится ничем; оплавленный
/// узел меняют. Это делает огонь по мехам дорогим по-настоящему, а не численно.
/// </summary>
public sealed partial class WarlockMechPartSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private static readonly SoundPathSpecifier WipeSound = new("/Audio/Effects/Fluids/watersplash.ogg");
    private static readonly SoundPathSpecifier ClickSound = new("/Audio/Machines/machine_switch.ogg");

    /// <summary>
    /// Сколько грязи даёт секунда на тайле.
    ///
    /// Таблица по подстроке в имени тайла, а не по списку: тайлов в игре под двести,
    /// перечислять их поимённо — гарантированно забыть половину и получить чистые ноги
    /// на новой карте. Правило же читается вслух: песок пачкает сильнее всего,
    /// грунт вдвое слабее, камень не пачкает, в помещении набегает пыль.
    /// </summary>
    private static readonly (string Match, float Rate)[] DirtRates =
    {
        ("Sand", 1.6f),
        ("Desert", 1.6f),
        ("Ironsand", 1.6f),
        ("Dirt", 0.8f),
        ("Grass", 0.8f),
        ("Mud", 1.2f),
        ("Basalt", 0f),
        ("Cave", 0f),
        ("Rock", 0f),
        ("Snow", 0f),
        ("Ice", 0f),
        ("Space", 0f),
    };

    /// <summary>Пыль в помещении: всё, что не попало в таблицу выше.</summary>
    private const float IndoorDirt = 0.25f;

    /// <summary>Выше этой температуры воздух начинает плавить лежащий узел.</summary>
    private const float MeltTemperature = 400f;

    /// <summary>Сколько оплавления даёт секунда в таком воздухе.</summary>
    private const float MeltPerSecond = 1.8f;

    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockMechPartComponent, ExaminedEvent>(OnPartExamined);
        SubscribeLocalEvent<WarlockMechPartComponent, DamageChangedEvent>(OnPartDamaged);

        SubscribeLocalEvent<WarlockMechRagComponent, AfterInteractEvent>(OnRagUsed);
        SubscribeLocalEvent<WarlockMechPartComponent, WarlockMechWipeDoAfterEvent>(OnWipeDoAfter);

        SubscribeLocalEvent<WarlockMechFrameComponent, ComponentStartup>(OnFrameStartup);
        SubscribeLocalEvent<WarlockMechFrameComponent, InteractUsingEvent>(OnFrameInteract);
        SubscribeLocalEvent<WarlockMechFrameComponent, ExaminedEvent>(OnFrameExamined);
        SubscribeLocalEvent<WarlockMechFrameComponent, GetVerbsEvent<AlternativeVerb>>(OnFrameVerbs);

        SubscribeLocalEvent<WarlockMechComponent, InteractUsingEvent>(OnMechInteract);
    }

    #region Осмотр и общий износ

    private void OnPartExamined(Entity<WarlockMechPartComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("warlock-mech-part-examine",
            ("slot", Loc.GetString($"warlock-mech-slot-{ent.Comp.Slot}"))));

        if (ent.Comp.Dirt > 5f)
        {
            args.PushMarkup(Loc.GetString("warlock-mech-part-dirt",
                ("pct", (int) MathF.Round(ent.Comp.Dirt))));
        }

        if (ent.Comp.Melt > 5f)
        {
            args.PushMarkup(Loc.GetString("warlock-mech-part-melt",
                ("pct", (int) MathF.Round(ent.Comp.Melt))));
        }
    }

    /// <summary>
    /// Средний износ установленных узлов, от нуля до единицы.
    /// Грязь и оплавление считаются вместе: раме безразлично, почему сустав не ходит.
    /// </summary>
    public float AverageWear(Container parts)
    {
        var sum = 0f;
        var count = 0;

        foreach (var part in parts.ContainedEntities)
        {
            if (!TryComp<WarlockMechPartComponent>(part, out var comp))
                continue;

            sum += MathF.Min(1f, (comp.Dirt + comp.Melt * 1.5f) / 100f) * comp.Weight;
            count++;
        }

        return count == 0 ? 0f : MathF.Min(1f, sum / count);
    }

    /// <summary>
    /// Стереть случайный установленный узел. Сухая рама делает это сама с собой.
    /// </summary>
    public void WearRandom(Container parts, float amount, IRobustRandom random)
    {
        var installed = parts.ContainedEntities
            .Where(HasComp<WarlockMechPartComponent>)
            .ToList();

        if (installed.Count == 0)
            return;

        var victim = random.Pick(installed);
        var comp = Comp<WarlockMechPartComponent>(victim);

        comp.Dirt = MathF.Min(100f, comp.Dirt + amount);
        Dirty(victim, comp);
    }

    /// <summary>
    /// Огонь по раме плавит её узлы. Вызывается из системы меха.
    /// </summary>
    public void MeltInstalled(Container parts, float amount)
    {
        foreach (var part in parts.ContainedEntities)
        {
            if (!TryComp<WarlockMechPartComponent>(part, out var comp))
                continue;

            comp.Melt = MathF.Min(100f, comp.Melt + amount * comp.MeltRate);
            Dirty(part, comp);
        }
    }

    /// <summary>
    /// Узел лежит отдельно и его жгут. Оплавление считается по фактическому тепловому
    /// урону: так работает и костёр, и плазменный резак, и попадание из огнемёта.
    /// </summary>
    private void OnPartDamaged(Entity<WarlockMechPartComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta is not { } delta || !args.DamageIncreased)
            return;

        var heat = 0f;

        foreach (var (type, value) in delta.DamageDict)
        {
            if (type.Id is "Heat" or "Caustic")
                heat += value.Float();
        }

        if (heat <= 0f)
            return;

        ent.Comp.Melt = MathF.Min(100f, ent.Comp.Melt + heat * 0.5f * ent.Comp.MeltRate);
        Dirty(ent);
    }

    #endregion

    #region Чистка

    private void OnRagUsed(Entity<WarlockMechRagComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        if (!TryComp<WarlockMechPartComponent>(target, out var part))
            return;

        args.Handled = true;

        if (part.Dirt <= 1f)
        {
            _popup.PopupEntity(Loc.GetString("warlock-mech-part-clean-already"), target, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay,
            new WarlockMechWipeDoAfterEvent(), target, target, ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnWipeDoAfter(Entity<WarlockMechPartComponent> ent, ref WarlockMechWipeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } rag)
            return;

        if (!TryComp<WarlockMechRagComponent>(rag, out var comp))
            return;

        args.Handled = true;

        ent.Comp.Dirt = MathF.Max(0f, ent.Comp.Dirt - comp.Clean);
        Dirty(ent);

        comp.Uses--;
        Dirty(rag, comp);

        _audio.PlayPvs(WipeSound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("warlock-mech-part-cleaned"), ent.Owner, args.User);

        if (comp.Uses <= 0)
        {
            _popup.PopupEntity(Loc.GetString("warlock-mech-rag-spent"), rag, args.User);
            QueueDel(rag);
        }
    }

    #endregion

    #region Сборка на раме

    private void OnFrameStartup(Entity<WarlockMechFrameComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.PartContainer = _container.EnsureContainer<Container>(ent.Owner, ent.Comp.PartContainerId);
    }

    private void OnFrameExamined(Entity<WarlockMechFrameComponent> ent, ref ExaminedEvent args)
    {
        var missing = MissingFrameSlots(ent).ToList();

        args.PushMarkup(missing.Count == 0
            ? Loc.GetString("warlock-mech-frame-ready")
            : Loc.GetString("warlock-mech-frame-missing",
                ("slots", string.Join(", ", missing.Select(s => Loc.GetString($"warlock-mech-slot-{s}"))))));
    }

    /// <summary>
    /// Вставка узла в раму. Порядок сборки не важен — важно, чтобы все гнёзда
    /// оказались заняты. Граф конструкции здесь был бы ложной сложностью: у него
    /// строгая цепочка шагов и визуализатор на каждый, а у нас десяток
    /// взаимозаменяемых по порядку узлов на четыре разных шасси.
    /// </summary>
    private void OnFrameInteract(Entity<WarlockMechFrameComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<WarlockMechPartComponent>(args.Used, out var part))
            return;

        args.Handled = true;

        if (part.Chassis != ent.Comp.Chassis)
        {
            _popup.PopupEntity(Loc.GetString("warlock-mech-part-wrong-chassis"), ent.Owner, args.User);
            return;
        }

        if (!ent.Comp.Slots.Contains(part.Slot))
        {
            _popup.PopupEntity(Loc.GetString("warlock-mech-part-no-slot"), ent.Owner, args.User);
            return;
        }

        if (FilledFrameSlots(ent).Contains(part.Slot))
        {
            _popup.PopupEntity(Loc.GetString("warlock-mech-part-slot-taken"), ent.Owner, args.User);
            return;
        }

        if (!_container.Insert(args.Used, ent.Comp.PartContainer))
            return;

        _audio.PlayPvs(ClickSound, ent.Owner);

        var missing = MissingFrameSlots(ent).ToList();

        if (missing.Count > 0)
        {
            _popup.PopupEntity(
                Loc.GetString("warlock-mech-frame-installed", ("left", missing.Count)),
                ent.Owner,
                args.User);
            return;
        }

        Finish(ent, args.User);
    }

    /// <summary>
    /// Рама собрана. Узлы переезжают в готовый мех вместе со всей своей грязью
    /// и оплавлением: собрать машину из хлама можно, но она и поедет как хлам.
    /// </summary>
    private void Finish(Entity<WarlockMechFrameComponent> ent, EntityUid user)
    {
        var coords = Transform(ent.Owner).Coordinates;
        var mech = Spawn(ent.Comp.Result, coords);

        if (TryComp<WarlockMechComponent>(mech, out var warlock))
        {
            foreach (var part in ent.Comp.PartContainer.ContainedEntities.ToList())
            {
                _container.Remove(part, ent.Comp.PartContainer);
                _container.Insert(part, warlock.PartContainer);
            }
        }

        _audio.PlayPvs(ClickSound, mech);
        _popup.PopupEntity(Loc.GetString("warlock-mech-frame-done", ("mech", Name(mech))),
            mech, user, PopupType.Medium);

        QueueDel(ent.Owner);
    }

    private void OnFrameVerbs(Entity<WarlockMechFrameComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        AddRemovalVerbs(ent.Comp.PartContainer, ent.Owner, args);
    }

    #endregion

    #region Установка и снятие на готовом мехе

    private void OnMechInteract(Entity<WarlockMechComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<WarlockMechPartComponent>(args.Used, out var part))
            return;

        args.Handled = true;

        if (part.Chassis != ChassisOf(ent))
        {
            _popup.PopupEntity(Loc.GetString("warlock-mech-part-wrong-chassis"), ent.Owner, args.User);
            return;
        }

        foreach (var installed in ent.Comp.PartContainer.ContainedEntities)
        {
            if (TryComp<WarlockMechPartComponent>(installed, out var comp) && comp.Slot == part.Slot)
            {
                _popup.PopupEntity(Loc.GetString("warlock-mech-part-slot-taken"), ent.Owner, args.User);
                return;
            }
        }

        if (!_container.Insert(args.Used, ent.Comp.PartContainer))
            return;

        _audio.PlayPvs(ClickSound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("warlock-mech-part-installed",
            ("part", Name(args.Used))), ent.Owner, args.User);
    }

    /// <summary>
    /// Шасси готового меха берётся с любого установленного узла: своего поля
    /// у меха нет, а узлы одного шасси всё равно все одинаковые в этом смысле.
    /// Пустой мех принимает что угодно — иначе его нечем было бы чинить.
    /// </summary>
    private string ChassisOf(Entity<WarlockMechComponent> ent)
    {
        foreach (var part in ent.Comp.PartContainer.ContainedEntities)
        {
            if (TryComp<WarlockMechPartComponent>(part, out var comp))
                return comp.Chassis;
        }

        return string.Empty;
    }

    /// <summary>
    /// Пункты «снять узел» для меню. Общие для рамы и готового меха.
    /// </summary>
    public void AddRemovalVerbs(Container parts, EntityUid owner, GetVerbsEvent<AlternativeVerb> args)
    {
        foreach (var part in parts.ContainedEntities.ToList())
        {
            var target = part;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("warlock-mech-verb-remove", ("part", Name(target))),
                Act = () =>
                {
                    if (!_container.Remove(target, parts))
                        return;

                    _transform.SetCoordinates(target, Transform(owner).Coordinates);
                    _audio.PlayPvs(ClickSound, owner);
                },
            });
        }
    }

    #endregion

    private IEnumerable<string> FilledFrameSlots(Entity<WarlockMechFrameComponent> ent)
    {
        foreach (var part in ent.Comp.PartContainer.ContainedEntities)
        {
            if (TryComp<WarlockMechPartComponent>(part, out var comp))
                yield return comp.Slot;
        }
    }

    private IEnumerable<string> MissingFrameSlots(Entity<WarlockMechFrameComponent> ent)
    {
        var filled = FilledFrameSlots(ent).ToHashSet();
        return ent.Comp.Slots.Where(slot => !filled.Contains(slot));
    }

    /// <summary>
    /// Сколько грязи даёт тайл под узлом.
    /// </summary>
    private float DirtRateAt(EntityUid part)
    {
        if (_turf.GetTileRef(Transform(part).Coordinates) is not { } tileRef)
            return 0f;

        var id = _tileDefs[tileRef.Tile.TypeId].ID;

        foreach (var (match, rate) in DirtRates)
        {
            if (id.Contains(match, StringComparison.OrdinalIgnoreCase))
                return rate;
        }

        return IndoorDirt;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        if (now < _nextTick)
            return;

        _nextTick = now + TimeSpan.FromSeconds(2);

        var query = EntityQueryEnumerator<WarlockMechPartComponent>();

        while (query.MoveNext(out var uid, out var part))
        {
            // Узел в контейнере — на раме, в ящике, в рюкзаке — не пачкается:
            // грязь берётся с пола, а не из воздуха.
            if (_container.IsEntityInContainer(uid))
                continue;

            var dirtied = false;

            var rate = DirtRateAt(uid) * part.DirtRate;

            if (rate > 0f && part.Dirt < 100f)
            {
                part.Dirt = MathF.Min(100f, part.Dirt + rate * 2f);
                dirtied = true;
            }

            // Лежит в горячем воздухе — плавится. Работает и в пожаре, и в лаве.
            var air = _atmos.GetTileMixture((uid, null));

            if (air != null && air.Temperature > MeltTemperature && part.Melt < 100f)
            {
                var over = MathF.Min(4f, (air.Temperature - MeltTemperature) / 200f);
                part.Melt = MathF.Min(100f, part.Melt + MeltPerSecond * 2f * over * part.MeltRate);
                dirtied = true;
            }

            if (dirtied)
                Dirty(uid, part);
        }
    }
}

