namespace Content.Server._Warlock.Psionics.Components;

/// <summary>
/// _Warlock — «Стеклянная Кожа» держится.
///
/// Пока компонент висит, по технмагу нельзя попасть ничем — и он не может сойти с места.
/// Это не щит, а пауза: пережить залп можно, воспользоваться передышкой нельзя.
/// </summary>
[RegisterComponent]
public sealed partial class WarlockGlassSkinComponent : Component
{
    /// <summary>
    /// Когда кожа снова станет обычной.
    /// </summary>
    [DataField]
    public TimeSpan EndAt;
}
