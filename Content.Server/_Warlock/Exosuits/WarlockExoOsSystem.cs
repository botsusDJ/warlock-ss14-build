using Content.Shared._Warlock.Exosuits;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server._Warlock.Exosuits;

/// <summary>
/// _Warlock
/// ОС экзоскелета.
///
/// Рама настраивается заранее. В бою переключаться поздно: пока листаешь меню,
/// тебя уже бьют, — поэтому весь смысл ОС в преднастройке. Отсюда и решение открывать
/// её не только надетой, но и в руках: собрался лезть в пещеры — распредели мощность
/// до того, как влез.
///
/// Настроек три, и каждая — размен, а не улучшение:
///
///   перекос      — доводит кулаки или кисти поверх базы, ниже базы не роняет ничего;
///   охлаждение    — активное остывает вдвое быстрее и само ест заряд;
///   ограничитель  — снятый даёт доработать на пределе ценой выброса по своим.
///
/// Клиент не решает ничего: он присылает три числа, а сервер их проверяет и применяет.
/// Сообщение от клиента можно подделать, поэтому доля мощности зажимается здесь.
/// </summary>
public sealed partial class WarlockExoOsSystem : EntitySystem
{
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private PowerCellSystem _cell = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private WarlockExosuitSystem _exosuit = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockExosuitComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<WarlockExosuitComponent, WarlockExoOsSetMessage>(OnSet);
        SubscribeLocalEvent<WarlockExosuitComponent, WarlockExoOsToggleMessage>(OnToggleRequest);
    }

    /// <summary>
    /// Пуск и останов приводов из ОС.
    ///
    /// Это единственный способ включить раму, и так задумано. Ванильные
    /// onActivate и onUse у ItemToggle отключены нарочно: активация открывает ОС,
    /// а не дёргает приводы втихую. Рама — не фонарик, её включают осознанно
    /// и с открытой панелью, где видно нагрев и заряд.
    /// </summary>
    private void OnToggleRequest(Entity<WarlockExosuitComponent> ent, ref WarlockExoOsToggleMessage args)
    {
        if (_toggle.IsActivated(ent.Owner))
            _toggle.TryDeactivate(ent.Owner, user: args.Actor);
        else
            _toggle.TryActivate(ent.Owner, user: args.Actor);

        if (TryGetWearer(ent, out var wearer))
            _exosuit.Apply(ent, wearer);

        Refresh(ent);
    }

    private void OnUiOpened(Entity<WarlockExosuitComponent> ent, ref BoundUIOpenedEvent args)
    {
        Refresh(ent);
    }

    private void OnSet(Entity<WarlockExosuitComponent> ent, ref WarlockExoOsSetMessage args)
    {
        // Зажимаем на сервере: клиент мог прислать что угодно, включая долю
        // в две единицы, и тогда рама выдала бы обе прибавки разом.
        ent.Comp.FistShare = Math.Clamp(args.FistShare, 0f, 1f);
        ent.Comp.Limiter = args.Limiter;
        ent.Comp.Cooling = args.Cooling;
        Dirty(ent);

        // Если рама надета, пересчитать носителю прибавки прямо сейчас.
        if (TryGetWearer(ent, out var wearer))
            _exosuit.Apply(ent, wearer);

        Refresh(ent);
    }

    private bool TryGetWearer(Entity<WarlockExosuitComponent> ent, out EntityUid wearer)
    {
        wearer = default;

        if (!_container.TryGetContainingContainer((ent.Owner, null, null), out var container))
            return false;

        if (!HasComp<WarlockExosuitWearerComponent>(container.Owner))
            return false;

        wearer = container.Owner;
        return true;
    }

    private void Refresh(Entity<WarlockExosuitComponent> ent)
    {
        var charge = 0f;
        var maxCharge = 0f;
        if (_cell.TryGetBatteryFromSlot(ent.Owner, out var battery) && battery is { } b)
        {
            // Заряд читается системой, а не полем: в компоненте лежит LastCharge,
            // то есть снимок на момент последнего пересчёта, а не текущее значение.
            charge = _battery.GetCharge(b.Owner);
            maxCharge = b.Comp.MaxCharge;
        }

        _ui.SetUiState(ent.Owner, WarlockExoOsUiKey.Key, new WarlockExoOsState(
            ent.Comp.FistShare,
            ent.Comp.Limiter,
            ent.Comp.Cooling,
            ent.Comp.Heat,
            ent.Comp.MaxHeat,
            charge,
            maxCharge,
            ent.Comp.Frame,
            _toggle.IsActivated(ent.Owner)));
    }
}
