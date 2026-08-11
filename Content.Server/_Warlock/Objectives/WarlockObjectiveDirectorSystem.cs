using System.Linq;
using Content.Server._Warlock.Objectives.Components;
using Content.Server.GameTicking.Events;
using Content.Shared._Warlock.Objectives;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Warlock.Objectives;

/// <summary>
/// _Warlock
/// Раздаёт фракциям цели на раунд и держит их состояние.
///
/// Цели намеренно НЕ взаимоисключающие: революция может выпасть всем трём гильдиям сразу,
/// две фракции могут одновременно охотиться за одним и тем же лордом. Это нормальный сюжет,
/// а не поломка раскатки.
///
/// Большинство целей ролевые и ничем не отслеживаются — механика считает только там,
/// где считать действительно есть что: сданные в терминал трофеи и смерть конкретной должности.
/// </summary>
public sealed partial class WarlockObjectiveDirectorSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly SoundPathSpecifier AcceptSound = new("/Audio/Machines/machine_switch.ogg");

    /// <summary>
    /// Текущее задание каждой фракции. Живёт ровно один раунд.
    /// </summary>
    private readonly Dictionary<WarlockFaction, WarlockFactionObjectiveState> _objectives = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<MobStateComponent, MobStateChangedEvent>(OnMobStateChanged);

        SubscribeLocalEvent<WarlockObjectiveTerminalComponent, ActivateInWorldEvent>(OnTerminalActivated);
        SubscribeLocalEvent<WarlockObjectiveTerminalComponent, InteractUsingEvent>(OnTerminalInteractUsing);
        SubscribeLocalEvent<WarlockObjectiveTerminalComponent, ExaminedEvent>(OnTerminalExamined);
    }

    #region Раскатка

    private void OnRoundStarting(RoundStartingEvent args)
    {
        RollObjectives();
    }

    /// <summary>
    /// Выдаёт каждой фракции по одной цели из подходящих ей.
    /// </summary>
    public void RollObjectives()
    {
        _objectives.Clear();

        foreach (var faction in Enum.GetValues<WarlockFaction>())
        {
            var candidates = _proto.EnumeratePrototypes<WarlockFactionObjectivePrototype>()
                .Where(p => p.Factions.Contains(faction))
                .ToList();

            if (candidates.Count == 0)
                continue;

            if (PickWeighted(candidates) is not { } picked)
                continue;

            _objectives[faction] = new WarlockFactionObjectiveState(picked.ID);
        }
    }

    /// <summary>
    /// Выбор с учётом веса. Свой, потому что нужен именно список прототипов, а не словарь.
    /// </summary>
    private WarlockFactionObjectivePrototype? PickWeighted(List<WarlockFactionObjectivePrototype> candidates)
    {
        var total = 0f;
        foreach (var candidate in candidates)
        {
            total += MathF.Max(0f, candidate.Weight);
        }

        if (total <= 0f)
            return _random.Pick(candidates);

        var roll = _random.NextFloat() * total;

        foreach (var candidate in candidates)
        {
            roll -= MathF.Max(0f, candidate.Weight);
            if (roll <= 0f)
                return candidate;
        }

        return candidates[^1];
    }

    /// <summary>
    /// Текущее задание фракции, если оно уже раскатано.
    /// </summary>
    public WarlockFactionObjectiveState? GetObjective(WarlockFaction faction)
    {
        return _objectives.GetValueOrDefault(faction);
    }

    #endregion

    #region Терминал

    private void OnTerminalExamined(Entity<WarlockObjectiveTerminalComponent> ent, ref ExaminedEvent args)
    {
        if (GetObjective(ent.Comp.Faction) is { } state && state.Complete)
            args.PushMarkup(Loc.GetString("warlock-objective-examine-complete"));
    }

    private void OnTerminalActivated(Entity<WarlockObjectiveTerminalComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;

        if (GetObjective(ent.Comp.Faction) is not { } state
            || !_proto.TryIndex<WarlockFactionObjectivePrototype>(state.Objective, out var proto))
        {
            _popup.PopupEntity(Loc.GetString("warlock-objective-terminal-empty"), ent, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString(proto.Title), ent, args.User, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString(proto.Description), ent, args.User);

        // Отдельной строкой показываем счётчик — только там, где есть что считать.
        switch (proto.Tracking)
        {
            case WarlockObjectiveTracking.Deliver:
                _popup.PopupEntity(
                    Loc.GetString("warlock-objective-progress-deliver",
                        ("current", state.Progress),
                        ("total", proto.DeliverCount)),
                    ent,
                    args.User);
                break;

            case WarlockObjectiveTracking.Assassinate:
                _popup.PopupEntity(
                    Loc.GetString(state.Complete
                        ? "warlock-objective-progress-assassinate-done"
                        : "warlock-objective-progress-assassinate-pending"),
                    ent,
                    args.User);
                break;

            case WarlockObjectiveTracking.None:
                _popup.PopupEntity(Loc.GetString("warlock-objective-progress-roleplay"), ent, args.User);
                break;
        }
    }

    private void OnTerminalInteractUsing(Entity<WarlockObjectiveTerminalComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (GetObjective(ent.Comp.Faction) is not { } state
            || !_proto.TryIndex<WarlockFactionObjectivePrototype>(state.Objective, out var proto))
            return;

        if (proto.Tracking != WarlockObjectiveTracking.Deliver || proto.DeliverTag is not { } tag)
            return;

        if (!_tag.HasTag(args.Used, tag))
            return;

        args.Handled = true;

        if (state.Complete)
        {
            _popup.PopupEntity(Loc.GetString("warlock-objective-already-complete"), ent, args.User);
            return;
        }

        QueueDel(args.Used);
        state.Progress++;

        _audio.PlayPvs(AcceptSound, ent);

        if (state.Progress >= proto.DeliverCount)
        {
            state.Complete = true;
            _popup.PopupEntity(Loc.GetString("warlock-objective-completed"), ent, args.User, PopupType.Large);
            return;
        }

        _popup.PopupEntity(
            Loc.GetString("warlock-objective-progress-deliver",
                ("current", state.Progress),
                ("total", proto.DeliverCount)),
            ent,
            args.User);
    }

    #endregion

    #region Отслеживание убийства

    private void OnMobStateChanged(Entity<MobStateComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (GetJobOf(ent.Owner) is not { } job)
            return;

        foreach (var (_, state) in _objectives)
        {
            if (state.Complete)
                continue;

            if (!_proto.TryIndex<WarlockFactionObjectivePrototype>(state.Objective, out var proto))
                continue;

            if (proto.Tracking != WarlockObjectiveTracking.Assassinate || proto.TargetJob != job)
                continue;

            state.Complete = true;
        }
    }

    /// <summary>
    /// Достаёт должность существа из его разума. Без разума должности нет.
    /// </summary>
    private ProtoId<JobPrototype>? GetJobOf(EntityUid uid)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return null;

        if (!_roles.MindHasRole<JobRoleComponent>(mindId, out var role))
            return null;

        return role.Value.Comp1.JobPrototype;
    }

    #endregion
}

/// <summary>
/// _Warlock — состояние цели одной фракции в текущем раунде.
/// </summary>
public sealed class WarlockFactionObjectiveState(ProtoId<WarlockFactionObjectivePrototype> objective)
{
    /// <summary>
    /// Какая цель выпала.
    /// </summary>
    public readonly ProtoId<WarlockFactionObjectivePrototype> Objective = objective;

    /// <summary>
    /// Сколько уже сдано, если цель это считает.
    /// </summary>
    public int Progress;

    /// <summary>
    /// Закрыта ли цель.
    /// </summary>
    public bool Complete;
}
