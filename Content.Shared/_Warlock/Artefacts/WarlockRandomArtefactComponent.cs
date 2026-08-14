using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Warlock.Artefacts;

/// <summary>
/// _Warlock — что умеет случайный артефакт.
///
/// Список нарочно короткий и весь состоит из вещей, которые видно с первого применения.
/// Ванильная ксеноархеология страдает тем, что половина её узлов ничего не показывает
/// игроку: он тыкает камень и не понимает, сработало или нет. Здесь каждый эффект
/// либо что-то делает с тем, кто держит, либо с тем, что вокруг, и это заметно сразу.
/// </summary>
[Serializable, NetSerializable]
public enum WarlockArtefactEffect : byte
{
    /// <summary>Затягивает раны держащего.</summary>
    Mend = 0,

    /// <summary>Бьёт держащего. Реликвии не обязаны быть полезными.</summary>
    Bite = 1,

    /// <summary>Возвращает псионический резерв.</summary>
    Feed = 2,

    /// <summary>Выпивает резерв досуха.</summary>
    Drain = 3,

    /// <summary>Расталкивает всё вокруг.</summary>
    Shove = 4,

    /// <summary>Тянет всё вокруг к себе.</summary>
    Pull = 5,

    /// <summary>Слепит всех рядом, включая держащего.</summary>
    Flash = 6,

    /// <summary>Гасит свет вокруг и глушит зрение мягче вспышки.</summary>
    Dim = 7,

    /// <summary>Ломает держащему кость.</summary>
    Break = 8,

    /// <summary>Ставит на держащем клеймо — след остаётся навсегда.</summary>
    Mark = 9,

    /// <summary>Швыряет держащего в случайную сторону.</summary>
    Toss = 10,

    /// <summary>Травит всё живое рядом.</summary>
    Rot = 11,

    /// <summary>Поджигает всё рядом, включая держащего.</summary>
    Kindle = 12,

    /// <summary>Роняет из ниоткуда обломок древней расы.</summary>
    Shed = 13,
}

/// <summary>
/// _Warlock
/// Малый артефакт со случайным содержимым.
///
/// Внешность, имя, набор эффектов и задержка выбираются один раз при появлении на карте
/// и дальше не меняются: два одинаковых с виду камня всегда делают одно и то же,
/// поэтому находки можно запоминать и обсуждать. Прототип при этом один.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockRandomArtefactComponent : Component
{
    /// <summary>
    /// Что этот камень умеет. Заполняется при появлении.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<WarlockArtefactEffect> Effects = new();

    /// <summary>
    /// Сколько эффектов катить. Один — простая находка, три — то, за чем лезут вглубь.
    /// </summary>
    [DataField]
    public int MinEffects = 1;

    [DataField]
    public int MaxEffects = 3;

    /// <summary>
    /// Задержка между применениями. Тоже случайная: медленный артефакт с сильным набором
    /// ценнее быстрого со слабым.
    /// </summary>
    [DataField]
    public float MinDelay = 20f;

    [DataField]
    public float MaxDelay = 90f;

    /// <summary>
    /// Когда камнем можно будет воспользоваться снова.
    /// </summary>
    [DataField]
    public TimeSpan NextUse;

    /// <summary>
    /// Выбранная задержка.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Delay;

    /// <summary>
    /// Сколько раз артефактом уже пользовались. Нужно только для осмотра.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Uses;

    /// <summary>
    /// Уже раскатан. Второй раз при перезаходе на карту катать нельзя,
    /// иначе сохранённый артефакт менял бы содержимое.
    /// </summary>
    [DataField]
    public bool Rolled;
}
