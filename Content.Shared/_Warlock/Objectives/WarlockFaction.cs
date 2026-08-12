using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Objectives;

/// <summary>
/// _Warlock — фракции, у которых бывает своя цель на раунд.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockFaction : byte
{
    GuildFactos = 0,
    GuildTechnos = 1,
    GuildWarlock = 2,
    Brotherhood = 3,
    UnathiKingdom = 4,
}

/// <summary>
/// _Warlock — как система понимает, что цель выполнена.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockObjectiveTracking : byte
{
    /// <summary>
    /// Никак. Цель ролевая, её судьбу решают игроки и админы.
    /// Таких целей большинство: механика не должна подменять отыгрыш.
    /// </summary>
    None = 0,

    /// <summary>
    /// Считает предметы с нужным тегом, сданные в терминал своей фракции.
    /// </summary>
    Deliver = 1,

    /// <summary>
    /// Ждёт смерти носителя указанной должности.
    /// </summary>
    Assassinate = 2,
}
