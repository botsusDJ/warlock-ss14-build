using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Injuries;

/// <summary>
/// _Warlock — что тело помнит о полученном.
///
/// Это не вторая система здоровья: урон и смерть считает ваниль. Травмы — летопись,
/// которая копится поверх урона, читается в осмотре и немного мешает жить.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockInjuryType : byte
{
    /// <summary>
    /// Ссадина. От режущего и колющего, сходит быстро, ни на что не влияет.
    /// </summary>
    Abrasion = 0,

    /// <summary>
    /// Синяк. От тупого, сходит медленно, режет запас выносливости.
    /// </summary>
    Bruise = 1,

    /// <summary>
    /// Перелом. Срастается долго и всегда оставляет шрам.
    /// В ноге замедляет, в остальном — выматывает.
    /// </summary>
    Fracture = 2,

    /// <summary>
    /// Шрам. Не сходит никогда.
    /// </summary>
    Scar = 3,

    /// <summary>
    /// Клеймо. Тоже навсегда, но ставится намеренно и означает принадлежность.
    /// </summary>
    Brand = 4,

    /// <summary>
    /// Выбитый зуб. Только на голове, только от тупого. Косметика, но заметная.
    /// </summary>
    MissingTooth = 5,

    /// <summary>
    /// Выбитый глаз. Редчайшее, только на голове и только от очень сильного удара.
    /// Навсегда портит зрение.
    /// </summary>
    MissingEye = 6,
}

/// <summary>
/// _Warlock — куда именно пришлось.
///
/// Локационного урона в ванили нет, поэтому часть тела выбирается броском с весами:
/// в торс попадают чаще всего, в голову — реже, и именно поэтому зубы и глаза
/// выбиваются так редко.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockBodyPart : byte
{
    Head = 0,
    Torso = 1,
    LeftArm = 2,
    RightArm = 3,
    LeftLeg = 4,
    RightLeg = 5,
}
