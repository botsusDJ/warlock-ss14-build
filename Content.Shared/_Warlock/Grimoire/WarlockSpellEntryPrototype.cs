using Content.Shared._Warlock.Objectives;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Grimoire;

/// <summary>
/// _Warlock — раздел гримуара.
///
/// Разделы отличаются не силой, а тем, кому позволено их читать. Рядовые открыты всем,
/// боевые тоже, а два нижних заперты должностью: жрец не полезет в командирский раздел,
/// даже если украдёт книгу лорда.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockSpellSection : byte
{
    /// <summary>
    /// Рядовые. Бытовое псайкерство, доступное любому члену гильдии.
    /// </summary>
    Common = 0,

    /// <summary>
    /// Боевые. Тоже открыты всем: гильдия хочет, чтобы её умели защищать.
    /// </summary>
    Combat = 1,

    /// <summary>
    /// Капелланские. Только жрецам и ритуалистам.
    /// </summary>
    Chaplain = 2,

    /// <summary>
    /// Командирские. Только лордам и архимагам.
    /// </summary>
    Command = 3,
}

/// <summary>
/// _Warlock
/// Одна строка в гримуаре: какое заклинание, в каком разделе, почём и кому.
///
/// Заведено отдельным прототипом, а не полем в книге, чтобы одно и то же заклинание
/// могло стоять в книгах разных гильдий по разной цене — и чтобы новое добавлялось
/// одним YAML-блоком, без правки кода.
/// </summary>
// Имя в YAML — warlockSpellEntry. Явно его писать не надо: Robust выводит его из имени
// класса сам, а дубликат ловится анализатором как RA0042.
[Prototype]
public sealed partial class WarlockSpellEntryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Какое действие выдаётся.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Action;

    /// <summary>
    /// В каком разделе книги оно лежит.
    /// </summary>
    [DataField]
    public WarlockSpellSection Section = WarlockSpellSection.Common;

    /// <summary>
    /// Сколько очков стоит.
    /// </summary>
    [DataField]
    public int Cost = 1;

    /// <summary>
    /// В книгах каких гильдий встречается. Пусто — во всех трёх.
    /// </summary>
    [DataField]
    public List<WarlockFaction> Guilds = new();

    /// <summary>
    /// Минимальный ранг читателя. Ноль — открыто всем.
    ///
    /// Основной способ запереть раздел. Ранг лежит компонентом на теле и читается
    /// одним TryComp, поэтому не зависит от разума и ролей — в отличие от списка
    /// должностей ниже, на котором эта проверка когда-то и сломалась.
    /// </summary>
    [DataField]
    public int MinRank;

    /// <summary>
    /// Только духовенству. Ранг тут не помогает: сан — отдельная лестница.
    /// </summary>
    [DataField]
    public bool Clergy;

    /// <summary>
    /// Должности, которым открыт этот раздел. Пусто — ограничения по должности нет.
    ///
    /// Запасной путь: если ранг на теле почему-то не выставлен, доступ ещё раз
    /// проверяется по должности из разума. Одного из двух достаточно.
    /// </summary>
    [DataField]
    public List<ProtoId<JobPrototype>> Jobs = new();
}
