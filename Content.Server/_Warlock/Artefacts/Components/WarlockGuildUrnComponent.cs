namespace Content.Server._Warlock.Artefacts.Components;

/// <summary>
/// _Warlock — «Урна Трёх Гильдий».
/// Расщепляет вложенные предметы в чистый заряд дара. Гильдия Фактос спорит с гильдией Варлок
/// о том, кощунство это или таинство, а гильдия Технос просто кормит урну мусором.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockGuildUrnComponent : Component
{
    /// <summary>
    /// Текущий накопленный заряд.
    /// </summary>
    [DataField]
    public float Charge;

    /// <summary>
    /// Потолок накопления.
    /// </summary>
    [DataField]
    public float MaxCharge = 60f;

    /// <summary>
    /// Сколько заряда даёт один поглощённый предмет.
    /// </summary>
    [DataField]
    public float ChargePerItem = 10f;
}
