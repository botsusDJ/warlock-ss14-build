using Content.Server.Chat.Systems;
using Content.Shared._Warlock.Unathi;
using Content.Shared.Chat;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Warlock.Unathi;

/// <summary>
/// _Warlock
/// Крестовый поход Королевства Унатхи в двух кнопках.
///
/// Клич — чистый отыгрыш с маленьким довеском: унатх орёт в голос, и у тех, кто рядом,
/// сбивается дыхание. Своих клич не задевает.
///
/// Ярость — настоящий размен: пока держится, унатх бьёт в полтора раза сильнее, получает
/// на четверть меньше и почти не устаёт. Когда отпускает — вся отложенная усталость
/// приходит разом.
/// </summary>
public sealed partial class WarlockUnathiSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockWarCryEvent>(OnWarCry);
        SubscribeLocalEvent<WarlockBerserkEvent>(OnBerserk);

        SubscribeLocalEvent<WarlockBerserkComponent, ComponentShutdown>(OnBerserkEnd);
        SubscribeLocalEvent<WarlockBerserkComponent, RefreshStaminaCritThresholdEvent>(OnBerserkStamina);
        SubscribeLocalEvent<WarlockBerserkComponent, DamageModifyEvent>(OnBerserkDamaged);

        // Бонус берсерка к удару живёт в WarlockAttackStrengthSystem: пара
        // MeleeWeaponComponent + GetMeleeDamageEvent допускает только одну подписку на весь билд.
    }

    #region Боевой Клич

    private void OnWarCry(WarlockWarCryEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var performer = args.Performer;

        // Клич — это в первую очередь голос, поэтому он идёт через обычный чат.
        if (_proto.TryIndex(args.Cries, out var dataset) && dataset.Values.Count > 0)
        {
            var cry = Loc.GetString(dataset.Values[_random.Next(dataset.Values.Count)]);
            _chat.TrySendInGameICMessage(performer, cry, InGameICChatType.Speak, hideChat: false);
        }

        _audio.PlayPvs(args.Sound, performer);

        // Врагам рядом сбивает дыхание. Своих поход не касается.
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(performer).Coordinates, args.Radius))
        {
            if (mob.Owner == performer)
                continue;

            if (_faction.IsEntityFriendly(performer, mob.Owner))
                continue;

            _stamina.TakeStaminaDamage(mob.Owner, args.StaminaDamage, source: performer);
        }
    }

    #endregion

    #region Священная Ярость

    private void OnBerserk(WarlockBerserkEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;

        if (HasComp<WarlockBerserkComponent>(performer))
        {
            _popup.PopupEntity(Loc.GetString("warlock-berserk-already"), performer, performer, PopupType.MediumCaution);
            return;
        }

        args.Handled = true;

        var berserk = EnsureComp<WarlockBerserkComponent>(performer);
        berserk.EndAt = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);
        Dirty(performer, berserk);

        _stamina.RefreshStaminaCritThreshold(performer);

        _audio.PlayPvs(args.Sound, performer);
        _popup.PopupEntity(Loc.GetString("warlock-berserk-start"), performer, performer, PopupType.LargeCaution);
    }

    private void OnBerserkEnd(Entity<WarlockBerserkComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return;

        _stamina.RefreshStaminaCritThreshold(ent.Owner);

        // Долг по усталости приходит целиком и сразу.
        _stamina.TakeStaminaDamage(ent.Owner, ent.Comp.Backlash, source: ent.Owner);

        _popup.PopupEntity(Loc.GetString("warlock-berserk-end"), ent.Owner, ent.Owner, PopupType.LargeCaution);
    }

    private void OnBerserkStamina(Entity<WarlockBerserkComponent> ent, ref RefreshStaminaCritThresholdEvent args)
    {
        args.Modifier *= ent.Comp.StaminaModifier;
    }

    private void OnBerserkDamaged(Entity<WarlockBerserkComponent> ent, ref DamageModifyEvent args)
    {
        // Ярость не отменяет урон, она его приглушает.
        args.Damage *= ent.Comp.ResistModifier;
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WarlockBerserkComponent>();

        while (query.MoveNext(out var uid, out var berserk))
        {
            if (now < berserk.EndAt)
                continue;

            RemCompDeferred<WarlockBerserkComponent>(uid);
        }
    }
}
