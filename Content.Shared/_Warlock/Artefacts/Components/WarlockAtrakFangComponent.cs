using Robust.Shared.GameStates;

namespace Content.Shared._Warlock.Artefacts.Components;

/// <summary>
/// _Warlock — «Клык Атрака».
/// Оружие, которое кормится. Каждое убийство им лечит владельца и делает клык злее,
/// но если владелец давно никого не убил, клык начинает есть его самого.
///
/// Компонент лежит в Shared, хотя вся его логика серверная: прибавку к урону считает
/// <c>WarlockAttackStrengthSystem</c>, а он общий. Пара MeleeWeaponComponent +
/// GetMeleeDamageEvent допускает одну подписку на весь билд, так что своей завести нельзя.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarlockAtrakFangComponent : Component
{
    /// <summary>
    /// Сколько убийств засчитано.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Kills;

    /// <summary>
    /// Прибавка к урону за каждое убийство.
    /// </summary>
    [DataField]
    public float DamagePerKill = 0.12f;

    /// <summary>
    /// Потолок прибавки, чтобы клык не превратился в кнопку победы.
    /// </summary>
    [DataField]
    public float MaxBonus = 1.5f;

    /// <summary>
    /// Сколько урона снимается с владельца за убийство.
    /// </summary>
    [DataField]
    public float HealOnKill = 25f;

    /// <summary>
    /// Через сколько секунд без крови клык начинает голодать.
    /// </summary>
    [DataField]
    public float HungerDelay = 120f;

    /// <summary>
    /// Когда клык последний раз ел.
    /// </summary>
    [DataField]
    public TimeSpan LastKill;

    /// <summary>
    /// Сколько урона голодный клык берёт с владельца за тик.
    /// </summary>
    [DataField]
    public float HungerDamage = 4f;

    [DataField]
    public TimeSpan NextTick;
}
