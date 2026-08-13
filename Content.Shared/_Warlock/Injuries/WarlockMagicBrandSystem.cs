using Content.Shared.ActionBlocker;
// RefreshStaminaCritThresholdEvent живёт в Damage.Events, а сама SharedStaminaSystem —
// в Damage.Systems. Нужны оба пространства имён.
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared._Warlock.Injuries;

/// <summary>
/// _Warlock
/// Три магических клейма. Ставятся обычным клеймением — тем же прижиманием железа к телу,
/// только железо не простое, — и после этого не снимаются ничем.
///
/// Намеренно не убивают и не выключают персонажа целиком: каждое отнимает одну возможность.
/// Скованный не возьмёт оружие, но дойдёт куда угодно и всё расскажет. Укоренённый стоит
/// на месте, но полностью боеспособен в упор. Выжженный ходит и дерётся ровно один раз.
/// </summary>
public sealed partial class WarlockMagicBrandSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockMagicBrandComponent, ComponentStartup>(OnStartup);

        // Оковы.
        SubscribeLocalEvent<WarlockMagicBrandComponent, PickupAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<WarlockMagicBrandComponent, UseAttemptEvent>(OnUseAttempt);
        SubscribeLocalEvent<WarlockMagicBrandComponent, AttackAttemptEvent>(OnAttackAttempt);

        // Корни.
        SubscribeLocalEvent<WarlockMagicBrandComponent, UpdateCanMoveEvent>(OnMoveAttempt);

        // Пепел.
        SubscribeLocalEvent<WarlockMagicBrandComponent, RefreshStaminaCritThresholdEvent>(OnRefreshStamina);
    }

    /// <summary>
    /// Ставит клеймо. Повторное того же вида ничего не меняет.
    /// </summary>
    public void Apply(EntityUid uid, WarlockBrandEffect effect)
    {
        if (effect == WarlockBrandEffect.None)
            return;

        var comp = EnsureComp<WarlockMagicBrandComponent>(uid);

        if (!comp.Effects.Add(effect))
            return;

        Dirty(uid, comp);
        Enforce((uid, comp));
    }

    public bool Has(Entity<WarlockMagicBrandComponent?> ent, WarlockBrandEffect effect)
    {
        return Resolve(ent, ref ent.Comp, false) && ent.Comp.Effects.Contains(effect);
    }

    private void OnStartup(Entity<WarlockMagicBrandComponent> ent, ref ComponentStartup args)
    {
        Enforce(ent);
    }

    /// <summary>
    /// Приводит тело в соответствие клеймам сразу после того, как они появились.
    /// </summary>
    private void Enforce(Entity<WarlockMagicBrandComponent> ent)
    {
        // Порог усталости пересчитывается всегда: это чистая арифметика и она предсказуема.
        _stamina.RefreshStaminaCritThreshold(ent.Owner);

        // UpdateCanMoveEvent кэшируется, и без этого вызова корни не подействуют
        // до следующего события, которое случайно сбросит кэш. Так прямо написано
        // в комментарии к самому событию.
        _blocker.UpdateCanMove(ent.Owner);

        // Ронять предметы имеет право только сервер, иначе клиент будет их дёргать.
        if (!_net.IsServer)
            return;

        if (!ent.Comp.Effects.Contains(WarlockBrandEffect.Shackles))
            return;

        if (!TryComp<HandsComponent>(ent, out var hands))
            return;

        foreach (var id in _hands.EnumerateHands((ent.Owner, hands)))
        {
            if (_hands.TryGetHeldItem((ent.Owner, hands), id, out _))
                _hands.TryDrop((ent.Owner, hands), id, checkActionBlocker: false);
        }
    }

    #region Оковы

    // Все четыре обработчика ниже — по значению и в старой форме (uid, comp, args).
    // Это обычные классы-события, и ваниль слушает их именно так: AdminFrozenSystem,
    // SharedCuffableSystem, SharedBuckleSystem. Robust требует, чтобы все подписчики
    // одного события были одного вида, и на несовпадении ref/по значению падает при старте.

    private void OnPickupAttempt(EntityUid uid, WarlockMagicBrandComponent comp, PickupAttemptEvent args)
    {
        if (args.Cancelled || !comp.Effects.Contains(WarlockBrandEffect.Shackles))
            return;

        args.Cancel();

        if (args.ShowPopup)
            _popup.PopupEntity(Loc.GetString("warlock-brand-shackles-blocked"), uid, uid);
    }

    private void OnUseAttempt(EntityUid uid, WarlockMagicBrandComponent comp, UseAttemptEvent args)
    {
        if (args.Cancelled || !comp.Effects.Contains(WarlockBrandEffect.Shackles))
            return;

        args.Cancel();
    }

    private void OnAttackAttempt(EntityUid uid, WarlockMagicBrandComponent comp, AttackAttemptEvent args)
    {
        if (args.Cancelled || !comp.Effects.Contains(WarlockBrandEffect.Shackles))
            return;

        args.Cancel();
    }

    #endregion

    #region Корни

    private void OnMoveAttempt(EntityUid uid, WarlockMagicBrandComponent comp, UpdateCanMoveEvent args)
    {
        if (args.Cancelled || !comp.Effects.Contains(WarlockBrandEffect.Roots))
            return;

        args.Cancel();
    }

    #endregion

    #region Пепел

    private void OnRefreshStamina(Entity<WarlockMagicBrandComponent> ent, ref RefreshStaminaCritThresholdEvent args)
    {
        if (!ent.Comp.Effects.Contains(WarlockBrandEffect.Ashes))
            return;

        args.Modifier *= ent.Comp.AshesStaminaPenalty;
    }

    #endregion
}
