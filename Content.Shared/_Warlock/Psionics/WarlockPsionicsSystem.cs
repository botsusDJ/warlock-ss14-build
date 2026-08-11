using Content.Shared._Warlock.Psionics.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Alert;
using Content.Shared.Alert.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared._Warlock.Psionics;

/// <summary>
/// _Warlock
/// Ядро псионики техномагов: хранение и регенерация псионической энергии, проверка и списание
/// стоимости заклинаний, отображение запаса в алертах.
///
/// Магия волшебника из ванилы устроена иначе — там ограничителем служит только кулдаун и мантия.
/// Здесь ограничитель ресурсный: техномаг может выпустить всё в один бурст и остаться пустым.
/// </summary>
public sealed class WarlockPsionicsSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockPsionicComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<WarlockPsionicComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<WarlockPsionicComponent, GetGenericAlertCounterAmountEvent>(OnGetCounterAmount);

        SubscribeLocalEvent<WarlockPsiCostComponent, ActionAttemptEvent>(OnPsiActionAttempt);
        SubscribeLocalEvent<WarlockPsiCostComponent, ActionPerformedEvent>(OnPsiActionPerformed);

        SubscribeLocalEvent<WarlockPsiSuppressedComponent, ComponentStartup>(OnSuppressionStartup);
        SubscribeLocalEvent<WarlockPsiSuppressedComponent, ComponentShutdown>(OnSuppressionShutdown);
    }

    private void OnMapInit(Entity<WarlockPsionicComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Energy = ent.Comp.MaxEnergy;
        UpdateAlert(ent);
    }

    private void OnShutdown(Entity<WarlockPsionicComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
    }

    private void OnSuppressionStartup(Entity<WarlockPsiSuppressedComponent> ent, ref ComponentStartup args)
    {
        RefreshSuppressedAlert(ent);
    }

    private void OnSuppressionShutdown(Entity<WarlockPsiSuppressedComponent> ent, ref ComponentShutdown args)
    {
        RefreshSuppressedAlert(ent);
    }

    private void RefreshSuppressedAlert(EntityUid uid)
    {
        if (TryComp<WarlockPsionicComponent>(uid, out var psionic))
            UpdateAlert((uid, psionic));
    }

    private void OnGetCounterAmount(Entity<WarlockPsionicComponent> ent, ref GetGenericAlertCounterAmountEvent args)
    {
        if (args.Alert.ID != ent.Comp.Alert.Id)
            return;

        args.Amount = ent.Comp.Energy.Int();
    }

    #region Стоимость заклинаний

    private void OnPsiActionAttempt(Entity<WarlockPsiCostComponent> ent, ref ActionAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var user = args.User;

        if (HasComp<WarlockPsiSuppressedComponent>(user))
        {
            args.Cancelled = true;
            _popup.PopupEntity(Loc.GetString("warlock-psionics-suppressed"), user, user, PopupType.MediumCaution);
            return;
        }

        if (!TryComp<WarlockPsionicComponent>(user, out var psionic))
        {
            // Артефакты могут разрешать каст без дара — тогда просто пропускаем проверку ресурса.
            if (!ent.Comp.RequiresPsionics)
                return;

            args.Cancelled = true;
            _popup.PopupEntity(Loc.GetString("warlock-psionics-no-gift"), user, user, PopupType.MediumCaution);
            return;
        }

        if (psionic.Energy < ent.Comp.Cost)
        {
            args.Cancelled = true;
            _popup.PopupEntity(
                Loc.GetString("warlock-psionics-not-enough-energy", ("cost", ent.Comp.Cost.Int())),
                user,
                user,
                PopupType.MediumCaution);
        }
    }

    private void OnPsiActionPerformed(Entity<WarlockPsiCostComponent> ent, ref ActionPerformedEvent args)
    {
        TryUseEnergy(args.Performer, ent.Comp.Cost);
    }

    #endregion

    #region Публичное API

    /// <summary>
    /// Хватает ли у существа энергии на указанную сумму.
    /// </summary>
    public bool HasEnergy(EntityUid uid, FixedPoint2 amount)
    {
        return TryComp<WarlockPsionicComponent>(uid, out var psionic) && psionic.Energy >= amount;
    }

    /// <summary>
    /// Списывает энергию, если её хватает. Возвращает false, если списать не удалось.
    /// </summary>
    public bool TryUseEnergy(EntityUid uid, FixedPoint2 amount)
    {
        if (!TryComp<WarlockPsionicComponent>(uid, out var psionic) || psionic.Energy < amount)
            return false;

        SetEnergy((uid, psionic), psionic.Energy - amount);
        return true;
    }

    /// <summary>
    /// Возвращает энергию носителю, не превышая максимум. Возвращает фактически восстановленное количество.
    /// </summary>
    public FixedPoint2 RestoreEnergy(EntityUid uid, FixedPoint2 amount)
    {
        if (!TryComp<WarlockPsionicComponent>(uid, out var psionic))
            return FixedPoint2.Zero;

        var restored = FixedPoint2.Min(amount, psionic.MaxEnergy - psionic.Energy);
        if (restored <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        SetEnergy((uid, psionic), psionic.Energy + restored);
        return restored;
    }

    public void SetEnergy(Entity<WarlockPsionicComponent> ent, FixedPoint2 value)
    {
        var clamped = FixedPoint2.Clamp(value, FixedPoint2.Zero, ent.Comp.MaxEnergy);
        if (clamped == ent.Comp.Energy)
            return;

        ent.Comp.Energy = clamped;
        Dirty(ent);
        UpdateAlert(ent);
    }

    #endregion

    private void UpdateAlert(Entity<WarlockPsionicComponent> ent)
    {
        // Алерты обновляем только там, где есть кому смотреть; на клиенте они перерисуются от сетевого стейта.
        _alerts.ShowAlert(ent.Owner, ent.Comp.Alert);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Регенерацию считаем на сервере, клиент получает готовое значение через AutoNetworkedField.
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<WarlockPsionicComponent>();
        while (query.MoveNext(out var uid, out var psionic))
        {
            if (psionic.Energy >= psionic.MaxEnergy)
                continue;

            if (HasComp<WarlockPsiSuppressedComponent>(uid))
                continue;

            psionic.RegenAccumulator += frameTime;
            if (psionic.RegenAccumulator < 1f)
                continue;

            var seconds = (int) psionic.RegenAccumulator;
            psionic.RegenAccumulator -= seconds;

            SetEnergy((uid, psionic), psionic.Energy + psionic.RegenPerSecond * seconds);
        }
    }
}
