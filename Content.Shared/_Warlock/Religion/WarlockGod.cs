using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Religion;

/// <summary>
/// _Warlock — божества, у которых в этой части галактики есть живые культы.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockGod : byte
{
    /// <summary>
    /// Механтехион — бог-механизм Братства Стали. Требует исправности, презирает плоть и ксеносов.
    /// Молитва ему чинит машины и тех, кто сделан из металла.
    /// </summary>
    Mechantechion = 0,

    /// <summary>
    /// Касс — божество вымершей планетарной расы, создатель артефактов.
    /// Гильдия Варлок поклоняется ему напрямую; молитва возвращает псионическую энергию.
    /// </summary>
    Kass = 1,
}
