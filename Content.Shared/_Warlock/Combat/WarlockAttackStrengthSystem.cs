using Content.Shared._Warlock.Artefacts.Components;
using Content.Shared._Warlock.Combat.Components;
using Content.Shared._Warlock.Combat.Events;
using Content.Shared._Warlock.Exosuits;
using Content.Shared._Warlock.Pain;
using Content.Shared._Warlock.Unathi;
using Content.Shared.Alert;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Warlock.Combat;

/// <summary>
/// _Warlock
/// Три силы ближнего удара вместо одной.
///
/// Ванильная боёвка знает только точный клик и широкий замах, и оба бьют одинаково.
/// Здесь боец сам выбирает, чем платит: слабый удар быстрый и почти безвредный,
/// средний остаётся ванильным, сильный бьёт вдвое больнее, но медленный и жрёт выносливость.
///
/// Модификатор считается на самом оружии (там же, где ванила считает урон),
/// а режим и плата берутся у того, кто держит оружие. Для безоружного удара
/// "оружием" выступает сам моб, поэтому кулаки работают через ту же ветку.
/// </summary>
public sealed partial class WarlockAttackStrengthSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private WarlockCasteSystem _caste = default!;
    [Dependency] private WarlockExosuitSystem _exosuit = default!;
    [Dependency] private WarlockPainSystem _pain = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockAttackStrengthComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<WarlockAttackStrengthComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<WarlockAttackStrengthComponent, WarlockCycleAttackStrengthEvent>(OnCycle);

        // Подписываемся на само оружие: ванила поднимает эти события именно на нём.
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeAttackRateEvent>(OnGetMeleeAttackRate);
        SubscribeLocalEvent<MeleeWeaponComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMapInit(Entity<WarlockAttackStrengthComponent> ent, ref MapInitEvent args)
    {
        UpdateAlert(ent);
    }

    private void OnShutdown(Entity<WarlockAttackStrengthComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
    }

    #region Переключение

    private void OnCycle(Entity<WarlockAttackStrengthComponent> ent, ref WarlockCycleAttackStrengthEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var next = ent.Comp.Strength switch
        {
            WarlockAttackStrength.Weak => WarlockAttackStrength.Normal,
            WarlockAttackStrength.Normal => WarlockAttackStrength.Strong,
            _ => WarlockAttackStrength.Weak,
        };

        SetStrength(ent, next);

        _popup.PopupEntity(Loc.GetString(GetStrengthMessage(next)), ent.Owner, ent.Owner);
    }

    public void SetStrength(Entity<WarlockAttackStrengthComponent> ent, WarlockAttackStrength strength)
    {
        if (ent.Comp.Strength == strength)
            return;

        ent.Comp.Strength = strength;
        Dirty(ent);
        UpdateAlert(ent);
    }

    private static string GetStrengthMessage(WarlockAttackStrength strength)
    {
        return strength switch
        {
            WarlockAttackStrength.Weak => "warlock-attack-strength-weak",
            WarlockAttackStrength.Strong => "warlock-attack-strength-strong",
            _ => "warlock-attack-strength-normal",
        };
    }

    private void UpdateAlert(Entity<WarlockAttackStrengthComponent> ent)
    {
        // Severity алерта начинается с 1, а перечисление — с 0.
        _alerts.ShowAlert(ent.Owner, ent.Comp.Alert, (short) ((short) ent.Comp.Strength + 1));
    }

    #endregion

    #region Модификаторы боя

    private void OnGetMeleeDamage(Entity<MeleeWeaponComponent> ent, ref GetMeleeDamageEvent args)
    {
        var modifier = 1f;

        if (TryComp<WarlockAttackStrengthComponent>(args.User, out var strength))
        {
            modifier *= strength.Strength switch
            {
                WarlockAttackStrength.Weak => strength.WeakDamage,
                WarlockAttackStrength.Strong => strength.StrongDamage,
                _ => 1f,
            };
        }

        // Берсеркство унатхов живёт здесь же, а не в своей системе: пара
        // MeleeWeaponComponent + GetMeleeDamageEvent допускает одну подписку на весь билд.
        if (TryComp<WarlockBerserkComponent>(args.User, out var berserk))
            modifier *= berserk.DamageModifier;

        // По той же причине здесь считается и голод Клыка Атрака. Прибавка висит на
        // самом оружии, а не на бойце: клык помнит свои убийства и при смене хозяина.
        if (TryComp<WarlockAtrakFangComponent>(ent.Owner, out var fang))
            modifier *= 1f + MathF.Min(fang.Kills * fang.DamagePerKill, fang.MaxBonus);

        // И приводы экзоскелета — сюда же. Какой канал рамы работает, зависит от того,
        // чем бьют: если оружие и есть сам боец, то это кулаки, и мощность берётся
        // из кулачного канала. Иначе работает кисть, держащая предмет.
        var unarmed = ent.Owner == args.User;
        modifier *= _exosuit.MeleeModifier(args.User, unarmed);
        _exosuit.HeatFromSwing(args.User);

        // Ярость легионера: чем хуже ему самому, тем сильнее он бьёт.
        modifier *= _caste.MeleeModifier(args.User);

        // Боль. Считается здесь по той же причине, что берсерк и клык: пара
        // MeleeWeaponComponent + GetMeleeDamageEvent допускает одну подписку
        // на весь билд, и она занята этим методом.
        modifier *= _pain.MeleeModifier(args.User);

        if (MathHelper.CloseTo(modifier, 1f))
            return;

        args.Damage *= modifier;
    }

    private void OnGetMeleeAttackRate(Entity<MeleeWeaponComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        if (!TryComp<WarlockAttackStrengthComponent>(args.User, out var strength))
            return;

        args.Multipliers *= strength.Strength switch
        {
            WarlockAttackStrength.Weak => strength.WeakAttackRate,
            WarlockAttackStrength.Strong => strength.StrongAttackRate,
            _ => 1f,
        };
    }

    private void OnMeleeHit(Entity<MeleeWeaponComponent> ent, ref MeleeHitEvent args)
    {
        // IsHit == false — это осмотр оружия, а не удар. Платить за осмотр не надо.
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        if (!TryComp<WarlockAttackStrengthComponent>(args.User, out var strength))
            return;

        var cost = strength.Strength switch
        {
            WarlockAttackStrength.Weak => strength.WeakStaminaCost,
            WarlockAttackStrength.Strong => strength.StrongStaminaCost,
            _ => strength.NormalStaminaCost,
        };

        if (cost <= 0f || !HasComp<StaminaComponent>(args.User))
            return;

        _stamina.TakeStaminaDamage(args.User, cost, visual: false, source: args.User);
    }

    #endregion
}
