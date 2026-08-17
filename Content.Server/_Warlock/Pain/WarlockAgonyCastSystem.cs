using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared._Warlock.Pain;
using Content.Shared._Warlock.Psionics.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Warlock.Pain;

/// <summary>
/// _Warlock
/// Срыв дара в агонии.
///
/// Псионик держит силу усилием воли. Боль это усилие ломает: на грани отключения
/// рука сама уходит в жест, и приём срабатывает без спроса — не тот, который нужен,
/// и не туда, куда надо.
///
/// Зачем это в бою. Раненый техномаг перестаёт быть просто ослабленным бойцом
/// и становится опасен для всех вокруг, включая своих. Добивать его в упор —
/// решение с ценой, и это ровно тот выбор, ради которого система боли и делалась.
///
/// Осторожность в реализации. Срываются ТОЛЬКО мгновенные приёмы — те, что не
/// требуют цели. Нацеленные заклинания пришлось бы наводить самим, и «случайный
/// каст» превратился бы в снайперский выстрел по случайному прохожему: движок
/// подставил бы координаты по умолчанию, а не то, что имел в виду игрок.
/// </summary>
public sealed partial class WarlockAgonyCastSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockPsionicComponent, WarlockPainLevelChangedEvent>(OnPainLevel);
    }

    /// <summary>
    /// Срыв проверяется в момент перехода на ступень, а не каждый тик.
    ///
    /// Иначе получилась бы лотерея на выживание: лежащий в затмении псионик
    /// разряжал бы весь запас приёмов за десяток секунд. Срыв должен быть
    /// событием, которое запоминают, а не фоновым шумом.
    /// </summary>
    private void OnPainLevel(Entity<WarlockPsionicComponent> ent, ref WarlockPainLevelChangedEvent args)
    {
        if (!args.Rose || args.Level < WarlockPainLevel.Agony)
            return;

        if (!TryComp<WarlockPainComponent>(ent.Owner, out var pain))
            return;

        // В затмении срывает вдвое чаще, чем в агонии.
        var chance = args.Level == WarlockPainLevel.Blackout ? 0.55f : 0.28f;
        if (!_random.Prob(chance))
            return;

        if (!TryPickSpell(ent.Owner, out var spell))
            return;

        _popup.PopupEntity(Loc.GetString("warlock-pain-spell-slip"), ent.Owner, ent.Owner,
            PopupType.LargeCaution);

        // Окружающие должны понимать, что произошло: иначе выглядит как читерство.
        _chat.TrySendInGameICMessage(ent.Owner, Loc.GetString("warlock-pain-spell-slip-emote"),
            InGameICChatType.Emote, ChatTransmitRange.Normal, ignoreActionBlocker: true);

        _actions.PerformAction(ent.Owner, spell.Value, predicted: false);
    }

    /// <summary>
    /// Выбрать случайный мгновенный приём из тех, что у носителя есть и что
    /// сейчас не на перезарядке.
    /// </summary>
    private bool TryPickSpell(EntityUid uid, out Entity<ActionComponent>? spell)
    {
        spell = null;

        var candidates = new List<Entity<ActionComponent>>();
        foreach (var action in _actions.GetActions(uid))
        {
            // Приём, а не любое действие: заклинания в билде помечены платой за дар.
            if (!HasComp<WarlockPsiCostComponent>(action.Owner))
                continue;

            // Только мгновенные, см. пояснение в шапке класса.
            if (!HasComp<InstantActionComponent>(action.Owner))
                continue;

            if (!action.Comp.Enabled)
                continue;

            if (action.Comp.Cooldown is { } cd && cd.End > _timing.CurTime)
                continue;

            candidates.Add(action);
        }

        if (candidates.Count == 0)
            return false;

        spell = _random.Pick(candidates);
        return true;
    }
}
