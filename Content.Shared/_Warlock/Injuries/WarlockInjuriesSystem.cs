using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.HealthExaminable;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Warlock.Injuries;

/// <summary>
/// _Warlock
/// Летопись тела. Ваниль считает урон и смерть — эта система считает следы.
///
/// Ссадины и синяки копятся от ударов и сходят сами. Перелом дорогой: он замедляет,
/// срастается долго и всегда оставляет шрам. Шрамы и клейма не сходят никогда.
///
/// Смотреть летопись можно только в лицо: под шлемом её не разглядеть.
/// </summary>
public sealed partial class WarlockInjuriesSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockInjuriesComponent, DamageDealtEvent>(OnDamageDealt);
        SubscribeLocalEvent<WarlockInjuriesComponent, HealthBeingExaminedEvent>(OnHealthExamined);
        SubscribeLocalEvent<WarlockInjuriesComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<WarlockInjuriesComponent, RefreshStaminaCritThresholdEvent>(OnRefreshStamina);
    }

    #region Публичное API

    public int GetCount(Entity<WarlockInjuriesComponent?> ent, WarlockInjuryType type)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return 0;

        return ent.Comp.Injuries.GetValueOrDefault(type);
    }

    /// <summary>
    /// Добавляет травму. Возвращает false, если по этому виду уже потолок.
    /// </summary>
    public bool TryAddInjury(Entity<WarlockInjuriesComponent?> ent, WarlockInjuryType type, int amount = 1)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        var current = ent.Comp.Injuries.GetValueOrDefault(type);
        if (current >= ent.Comp.MaxPerType)
            return false;

        ent.Comp.Injuries[type] = Math.Min(ent.Comp.MaxPerType, current + amount);
        OnInjuriesChanged((ent, ent.Comp));
        return true;
    }

    /// <summary>
    /// Снимает травму. Шрамы и клейма этим не убрать — они на то и вечные.
    /// </summary>
    public bool TryRemoveInjury(Entity<WarlockInjuriesComponent?> ent, WarlockInjuryType type, int amount = 1)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (type is WarlockInjuryType.Scar or WarlockInjuryType.Brand)
            return false;

        var current = ent.Comp.Injuries.GetValueOrDefault(type);
        if (current <= 0)
            return false;

        var left = current - amount;

        if (left <= 0)
            ent.Comp.Injuries.Remove(type);
        else
            ent.Comp.Injuries[type] = left;

        OnInjuriesChanged((ent, ent.Comp));
        return true;
    }

    /// <summary>
    /// Ставит именное клеймо. Снять нельзя.
    /// </summary>
    public void AddBrand(Entity<WarlockInjuriesComponent?> ent, string brand)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.Brands.Add(brand);
        ent.Comp.Injuries[WarlockInjuryType.Brand] = ent.Comp.Brands.Count;

        OnInjuriesChanged((ent, ent.Comp));
    }

    /// <summary>
    /// Заживляет всё, что вообще способно зажить. Шрамы и клейма остаются.
    /// </summary>
    public void HealAll(Entity<WarlockInjuriesComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.Injuries.Remove(WarlockInjuryType.Abrasion);
        ent.Comp.Injuries.Remove(WarlockInjuryType.Bruise);
        ent.Comp.Injuries.Remove(WarlockInjuryType.Fracture);
        ent.Comp.FractureProgress = 0;

        OnInjuriesChanged((ent, ent.Comp));
    }

    private void OnInjuriesChanged(Entity<WarlockInjuriesComponent> ent)
    {
        Dirty(ent);

        // Оба метода ждут Entity со своим компонентом, а не с нашим: между разными Entity<T>
        // неявного приведения нет, только от голого EntityUid.
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
        _stamina.RefreshStaminaCritThreshold(ent.Owner);
    }

    #endregion

    #region Накопление

    private void OnDamageDealt(Entity<WarlockInjuriesComponent> ent, ref DamageDealtEvent args)
    {
        // Следы оставляет только сервер: иначе клиент насчитает своих переломов.
        if (!_net.IsServer)
            return;

        var dict = args.Damage.DamageDict;

        var blunt = dict.GetValueOrDefault("Blunt").Float();
        var sharp = dict.GetValueOrDefault("Slash").Float() + dict.GetValueOrDefault("Piercing").Float();

        // Сильный тупой удар ломает кость, слабый оставляет синяк.
        if (blunt >= ent.Comp.FractureThreshold)
            TryAddInjury(ent.Owner, WarlockInjuryType.Fracture);
        else if (blunt >= ent.Comp.BruiseThreshold)
            TryAddInjury(ent.Owner, WarlockInjuryType.Bruise);

        if (sharp >= ent.Comp.AbrasionThreshold)
            TryAddInjury(ent.Owner, WarlockInjuryType.Abrasion);
    }

    #endregion

    #region Осмотр

    private void OnHealthExamined(Entity<WarlockInjuriesComponent> ent, ref HealthBeingExaminedEvent args)
    {
        // Под шлемом лица не видно, а следы читают именно по лицу.
        if (_inventory.TryGetSlotEntity(ent.Owner, "head", out _))
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString("warlock-injuries-hidden-by-helmet"));
            return;
        }

        var wrote = false;

        foreach (var type in new[]
                 {
                     WarlockInjuryType.Abrasion,
                     WarlockInjuryType.Bruise,
                     WarlockInjuryType.Fracture,
                     WarlockInjuryType.Scar,
                 })
        {
            var count = ent.Comp.Injuries.GetValueOrDefault(type);
            if (count <= 0)
                continue;

            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString(GetInjuryLoc(type, count)));
            wrote = true;
        }

        foreach (var brand in ent.Comp.Brands)
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString("warlock-injuries-brand", ("brand", Loc.GetString(brand))));
            wrote = true;
        }

        if (!wrote)
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString("warlock-injuries-none"));
        }
    }

    /// <summary>
    /// Подбирает строку по виду травмы и по тому, много её или мало.
    /// </summary>
    private static string GetInjuryLoc(WarlockInjuryType type, int count)
    {
        var severity = count switch
        {
            <= 1 => "light",
            <= 3 => "medium",
            _ => "heavy",
        };

        var name = type switch
        {
            WarlockInjuryType.Abrasion => "abrasion",
            WarlockInjuryType.Bruise => "bruise",
            WarlockInjuryType.Fracture => "fracture",
            _ => "scar",
        };

        return $"warlock-injuries-{name}-{severity}";
    }

    #endregion

    #region Механические последствия

    private void OnRefreshSpeed(Entity<WarlockInjuriesComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var fractures = ent.Comp.Injuries.GetValueOrDefault(WarlockInjuryType.Fracture);
        if (fractures <= 0)
            return;

        var modifier = MathF.Pow(ent.Comp.FractureSlowdown, fractures);
        args.ModifySpeed(modifier);
    }

    private void OnRefreshStamina(Entity<WarlockInjuriesComponent> ent, ref RefreshStaminaCritThresholdEvent args)
    {
        var bruises = ent.Comp.Injuries.GetValueOrDefault(WarlockInjuryType.Bruise);
        if (bruises <= 0)
            return;

        args.Modifier *= MathF.Pow(ent.Comp.BruiseStaminaPenalty, bruises);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WarlockInjuriesComponent>();

        while (query.MoveNext(out var uid, out var injuries))
        {
            if (now < injuries.NextHeal)
                continue;

            injuries.NextHeal = now + injuries.HealInterval;
            Heal((uid, injuries));
        }
    }

    /// <summary>
    /// Один шаг заживления. Ссадины сходят быстро, синяки медленнее,
    /// перелом срастается через несколько шагов и оставляет шрам.
    /// </summary>
    private void Heal(Entity<WarlockInjuriesComponent> ent)
    {
        if (ent.Comp.Injuries.GetValueOrDefault(WarlockInjuryType.Abrasion) > 0)
            TryRemoveInjury(ent.Owner, WarlockInjuryType.Abrasion);

        // Синяк сходит через раз, чтобы держался заметно дольше ссадины.
        if (ent.Comp.Injuries.GetValueOrDefault(WarlockInjuryType.Bruise) > 0 && _random.Prob(0.5f))
            TryRemoveInjury(ent.Owner, WarlockInjuryType.Bruise);

        if (ent.Comp.Injuries.GetValueOrDefault(WarlockInjuryType.Fracture) <= 0)
        {
            ent.Comp.FractureProgress = 0;
            return;
        }

        ent.Comp.FractureProgress++;

        if (ent.Comp.FractureProgress < ent.Comp.FractureTicksToHeal)
            return;

        ent.Comp.FractureProgress = 0;
        TryRemoveInjury(ent.Owner, WarlockInjuryType.Fracture);

        // Сросшаяся кость всегда оставляет след.
        TryAddInjury(ent.Owner, WarlockInjuryType.Scar);
    }
}
