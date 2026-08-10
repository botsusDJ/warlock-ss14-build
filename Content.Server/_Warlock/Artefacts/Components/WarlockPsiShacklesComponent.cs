namespace Content.Server._Warlock.Artefacts.Components;

/// <summary>
/// _Warlock — «Оковы Логики».
/// Проклятый артефакт гильдии Технос: надетый, он глушит дар носителя и не даёт снять себя самостоятельно.
/// Освободить может только кто-то другой.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockPsiShacklesComponent : Component
{
    /// <summary>
    /// Можно ли снять оковы с себя самому. Проклятие как раз в том, что нельзя.
    /// </summary>
    [DataField]
    public bool AllowSelfRemoval;
}
