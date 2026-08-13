using System.Linq;
using Content.Shared._Warlock.Objectives;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Warlock.Guilds;

/// <summary>
/// _Warlock
/// Кто к какой фракции принадлежит. Одно место на весь билд, где это выясняется.
///
/// Принадлежность определяется должностью, а не расой и не одеждой: техномаг без работы
/// не состоит ни в какой гильдии, а человек в краденой мантии Варлока — тем более.
/// Гильдия читается из департамента, в котором числится должность, так что добавление
/// новой роли в departments.yml подхватывается само.
/// </summary>
public sealed partial class WarlockGuildSystem : EntitySystem
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _roles = default!;

    /// <summary>
    /// Департамент каждой фракции. Ключ — то, что лежит в departments.yml.
    /// </summary>
    private static readonly (string Department, WarlockFaction Faction)[] Departments =
    {
        ("WarlockGuildFactos", WarlockFaction.GuildFactos),
        ("WarlockGuildTechnos", WarlockFaction.GuildTechnos),
        ("WarlockGuildWarlock", WarlockFaction.GuildWarlock),
        ("WarlockBrotherhood", WarlockFaction.Brotherhood),
        ("WarlockUnathi", WarlockFaction.UnathiKingdom),
    };

    /// <summary>
    /// Какая должность у этого тела. Без разума и без роли — никакой.
    /// </summary>
    public ProtoId<JobPrototype>? GetJobOf(EntityUid uid)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return null;

        if (!_roles.MindHasRole<JobRoleComponent>(mindId, out var role))
            return null;

        return role.Value.Comp1.JobPrototype;
    }

    /// <summary>
    /// К какой фракции относится должность.
    /// </summary>
    public WarlockFaction? GetFactionOfJob(ProtoId<JobPrototype> job)
    {
        foreach (var (department, faction) in Departments)
        {
            if (!_proto.TryIndex<DepartmentPrototype>(department, out var proto))
                continue;

            if (proto.Roles.Contains(job))
                return faction;
        }

        return null;
    }

    /// <summary>
    /// К какой фракции относится это тело.
    /// </summary>
    public WarlockFaction? GetGuildOf(EntityUid uid)
    {
        return GetJobOf(uid) is { } job ? GetFactionOfJob(job) : null;
    }

    /// <summary>
    /// Все живые игроки этой фракции. Используется телепатическими заклинаниями:
    /// адресат должен сидеть в теле, гхосты чужую связь не слушают.
    /// </summary>
    public IEnumerable<ICommonSession> GetGuildSessions(WarlockFaction faction)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } body)
                continue;

            if (GetGuildOf(body) == faction)
                yield return session;
        }
    }
}
