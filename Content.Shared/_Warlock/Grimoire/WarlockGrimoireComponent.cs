using Content.Shared._Warlock.Objectives;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Grimoire;

/// <summary>
/// _Warlock
/// Книга заклинаний гильдии. Три штуки на Союз, по одной на гильдию.
///
/// Книга не библиотека, а расходник: в ней есть запас очков, каждое заклинание стоит
/// сколько-то из них, и когда запас кончается — книга рассыпается. Поэтому одну книгу
/// нельзя пустить по кругу, зато у неё можно отнять остаток, если успеть.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockGrimoireComponent : Component
{
    /// <summary>
    /// Чья это книга. Определяет, какие строки каталога в ней вообще есть.
    /// </summary>
    [DataField(required: true)]
    public WarlockFaction Guild;

    /// <summary>
    /// Сколько очков осталось. На нуле книга рассыпается.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Points = 6;

    /// <summary>
    /// Что из книги уже вычитали. Одно и то же заклинание дважды не берут.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> Taken = new();
}

/// <summary>
/// _Warlock — ключ интерфейса гримуара.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockGrimoireUiKey : byte
{
    Key,
}

/// <summary>
/// _Warlock — одна строка в открытой книге, как её видит клиент.
/// </summary>
[Serializable, NetSerializable]
public sealed class WarlockGrimoireEntry
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public WarlockSpellSection Section;
    public int Cost;

    /// <summary>
    /// Уже вычитано из этой книги.
    /// </summary>
    public bool Taken;

    /// <summary>
    /// Должность позволяет читать этот раздел.
    /// </summary>
    public bool Allowed;
}

/// <summary>
/// _Warlock — состояние открытой книги.
/// </summary>
[Serializable, NetSerializable]
public sealed class WarlockGrimoireState(int points, List<WarlockGrimoireEntry> entries) : BoundUserInterfaceState
{
    public readonly int Points = points;
    public readonly List<WarlockGrimoireEntry> Entries = entries;
}

/// <summary>
/// _Warlock — игрок выбрал строку в книге.
/// </summary>
[Serializable, NetSerializable]
public sealed class WarlockGrimoireLearnMessage(string entry) : BoundUserInterfaceMessage
{
    public readonly string Entry = entry;
}
