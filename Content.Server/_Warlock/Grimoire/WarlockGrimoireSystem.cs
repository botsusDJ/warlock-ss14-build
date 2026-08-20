using System.Linq;
using Content.Server._Warlock.Guilds;
using Content.Shared._Warlock.Grimoire;
using Content.Shared._Warlock.Guilds;
using Content.Shared.Actions;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Warlock.Grimoire;

/// <summary>
/// _Warlock
/// Книга заклинаний гильдии.
///
/// Единственный способ получить псайкерство: раса больше не выдаёт ничего. Книга держит
/// запас очков, каждая строка каталога стоит сколько-то, и на нуле книга рассыпается.
/// Из этого вытекает всё остальное: делиться книгой можно, но чем больше взял ты,
/// тем меньше достанется соседу, а украденная книга откроет вору только два верхних раздела.
/// </summary>
public sealed partial class WarlockGrimoireSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private WarlockGuildSystem _guilds = default!;

    private static readonly SoundPathSpecifier LearnSound = new("/Audio/Magic/staff_animation.ogg");
    private static readonly SoundPathSpecifier CrumbleSound = new("/Audio/Effects/poster_broken.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockGrimoireComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<WarlockGrimoireComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<WarlockGrimoireComponent, WarlockGrimoireLearnMessage>(OnLearn);
    }

    private void OnExamined(Entity<WarlockGrimoireComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("warlock-grimoire-examine", ("points", ent.Comp.Points)));
    }

    private void OnUiOpened(Entity<WarlockGrimoireComponent> ent, ref BoundUIOpenedEvent args)
    {
        Refresh(ent, args.Actor);
    }

    /// <summary>
    /// Пересобирает содержимое книги под конкретного читателя: доступ к разделам
    /// зависит от его должности, а не от книги.
    /// </summary>
    private void Refresh(Entity<WarlockGrimoireComponent> ent, EntityUid reader)
    {
        var entries = new List<WarlockGrimoireEntry>();

        foreach (var entry in GetEntriesFor(ent.Comp))
        {
            if (!_proto.TryIndex(entry.Action, out var action))
                continue;

            entries.Add(new WarlockGrimoireEntry
            {
                Id = entry.ID,
                Name = action.Name,
                Description = action.Description,
                Section = entry.Section,
                Cost = entry.Cost,
                Taken = ent.Comp.Taken.Contains(entry.ID),
                Allowed = IsAllowed(entry, reader),
            });
        }

        entries.Sort((a, b) => a.Section != b.Section
            ? a.Section.CompareTo(b.Section)
            : a.Cost.CompareTo(b.Cost));

        _ui.SetUiState(ent.Owner, WarlockGrimoireUiKey.Key, new WarlockGrimoireState(ent.Comp.Points, entries));
    }

    /// <summary>
    /// Все строки каталога, которые вообще есть в книге этой гильдии.
    /// </summary>
    private IEnumerable<WarlockSpellEntryPrototype> GetEntriesFor(WarlockGrimoireComponent comp)
    {
        foreach (var entry in _proto.EnumeratePrototypes<WarlockSpellEntryPrototype>())
        {
            // Пустой список гильдий означает «во всех трёх книгах».
            if (entry.Guilds.Count == 0 || entry.Guilds.Contains(comp.Guild))
                yield return entry;
        }
    }

    /// <summary>
    /// Открыт ли раздел этому читателю.
    ///
    /// Проверок две, и достаточно любой. Основная — ранг компонентом на теле: он
    /// выдаётся должностью при спавне и читается одним TryComp. Запасная — список
    /// должностей из разума: раньше она была единственной, и когда цепочка
    /// «тело → разум → сущность роли → прототип» где-то рвалась, архимаг терял
    /// собственные разделы. Теперь порванная цепочка перестала быть приговором.
    /// </summary>
    private bool IsAllowed(WarlockSpellEntryPrototype entry, EntityUid reader)
    {
        // Раздел без ограничений открыт всем без всяких проверок.
        if (entry.MinRank <= 0 && !entry.Clergy && entry.Jobs.Count == 0)
            return true;

        if (TryComp<WarlockRankComponent>(reader, out var rank)
            && rank.Rank >= entry.MinRank
            && (!entry.Clergy || rank.Clergy))
        {
            return true;
        }

        return entry.Jobs.Count > 0
               && _guilds.GetJobOf(reader) is { } job
               && entry.Jobs.Contains(job);
    }

    private void OnLearn(Entity<WarlockGrimoireComponent> ent, ref WarlockGrimoireLearnMessage args)
    {
        var reader = args.Actor;

        if (!_proto.TryIndex<WarlockSpellEntryPrototype>(args.Entry, out var entry))
            return;

        // Клиент присылает только идентификатор, поэтому все проверки повторяем здесь:
        // подделанное сообщение не должно выдать командирское заклинание адепту.
        if (entry.Guilds.Count > 0 && !entry.Guilds.Contains(ent.Comp.Guild))
            return;

        if (ent.Comp.Taken.Contains(entry.ID))
            return;

        if (!IsAllowed(entry, reader))
        {
            _popup.PopupEntity(Loc.GetString("warlock-grimoire-forbidden"), ent, reader, PopupType.MediumCaution);
            return;
        }

        if (ent.Comp.Points < entry.Cost)
        {
            _popup.PopupEntity(Loc.GetString("warlock-grimoire-too-expensive"), ent, reader, PopupType.MediumCaution);
            return;
        }

        // Второй раз одно и то же действие человеку не нужно.
        if (HasAction(reader, entry.Action))
        {
            _popup.PopupEntity(Loc.GetString("warlock-grimoire-already-known"), ent, reader, PopupType.Medium);
            return;
        }

        ent.Comp.Points -= entry.Cost;
        ent.Comp.Taken.Add(entry.ID);
        Dirty(ent);

        EntityUid? action = null;
        _actions.AddAction(reader, ref action, entry.Action);

        _audio.PlayPvs(LearnSound, ent);
        _popup.PopupEntity(Loc.GetString("warlock-grimoire-learned"), ent, reader, PopupType.Medium);

        if (ent.Comp.Points > 0)
        {
            Refresh(ent, reader);
            return;
        }

        // Запас кончился. Книга не пустеет — она перестаёт существовать.
        _audio.PlayPvs(CrumbleSound, ent);
        _popup.PopupEntity(Loc.GetString("warlock-grimoire-crumbles"), ent, reader, PopupType.LargeCaution);
        QueueDel(ent);
    }

    /// <summary>
    /// Есть ли уже у читателя такое действие.
    /// </summary>
    private bool HasAction(EntityUid reader, EntProtoId proto)
    {
        foreach (var action in _actions.GetActions(reader))
        {
            if (MetaData(action.Owner).EntityPrototype?.ID == proto.Id)
                return true;
        }

        return false;
    }
}
