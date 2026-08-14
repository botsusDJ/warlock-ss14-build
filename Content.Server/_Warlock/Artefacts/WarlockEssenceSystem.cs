using Content.Server._Warlock.Artefacts.Components;
using Content.Server.Chat.Systems;
using Content.Shared._Warlock.Psionics;
using Content.Shared._Warlock.Psionics.Components;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Warlock.Artefacts;

/// <summary>
/// _Warlock
/// Пси-эссенция — жёлтая жижа из-под мёртвого скарабея.
///
/// Ересь по меркам Союза: гильдии молятся артефактам и платят резервом, а эссенция
/// предлагает силу без молитвы и без платы. Плата есть, просто она отложена.
///
/// Устроено намеренно нечестно по отношению к игроку: первые две дозы — чистая выгода,
/// без единого намёка на последствия. Расплата включается с третьей, и откатить её нельзя
/// ничем: счётчик доз не убывает никогда, даже после смерти и лечения.
/// </summary>
public sealed partial class WarlockEssenceSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private WarlockPsionicsSystem _psionics = default!;

    private static readonly SoundPathSpecifier DrinkSound = new("/Audio/Items/drink.ogg");

    /// <summary>
    /// Что вырывается наружу, когда язык уже не свой.
    /// </summary>
    private static readonly string[] Babble =
    {
        "warlock-essence-babble-1",
        "warlock-essence-babble-2",
        "warlock-essence-babble-3",
        "warlock-essence-babble-4",
        "warlock-essence-babble-5",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockEssenceSourceComponent, MobStateChangedEvent>(OnSourceDied);

        SubscribeLocalEvent<WarlockPsiEssenceComponent, UseInHandEvent>(OnDrink);
        SubscribeLocalEvent<WarlockPsiEssenceComponent, ExaminedEvent>(OnEssenceExamined);

        SubscribeLocalEvent<WarlockEssenceCorruptionComponent, ExaminedEvent>(OnCorruptionExamined);
        SubscribeLocalEvent<WarlockEssenceCorruptionComponent, AccentGetEvent>(OnCorruptedAccent);

        SubscribeLocalEvent<WarlockEssenceHighComponent, RefreshMovementSpeedModifiersEvent>(OnHighSpeed);
        SubscribeLocalEvent<WarlockEssenceHighComponent, RefreshStaminaCritThresholdEvent>(OnHighStamina);
        SubscribeLocalEvent<WarlockEssenceHighComponent, DamageModifyEvent>(OnHighDamage);
    }

    #region Откуда берётся

    /// <summary>
    /// Из-под мёртвого скарабея натекает лужа. Один труп — одна порция.
    /// </summary>
    private void OnSourceDied(Entity<WarlockEssenceSourceComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || ent.Comp.Spent)
            return;

        ent.Comp.Spent = true;

        var coords = Transform(ent).Coordinates;

        for (var i = 0; i < ent.Comp.Amount; i++)
        {
            Spawn(ent.Comp.Essence, coords);
        }
    }

    #endregion

    #region Доза

    private void OnEssenceExamined(Entity<WarlockPsiEssenceComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("warlock-essence-examine"));
    }

    private void OnDrink(Entity<WarlockPsiEssenceComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;

        if (!HasComp<MobStateComponent>(user))
            return;

        args.Handled = true;

        // Выгода приходит сразу и полностью. В этом весь смысл: сомневаться не в чем.
        _damageable.HealEvenly(user, -ent.Comp.Heal, origin: ent.Owner);
        _psionics.RestoreEnergy(user, ent.Comp.Energy);

        var high = EnsureComp<WarlockEssenceHighComponent>(user);
        high.EndAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.HighDuration);

        _movement.RefreshMovementSpeedModifiers(user);
        _stamina.RefreshStaminaCritThreshold(user);

        // А счёт ведётся молча.
        var corruption = EnsureComp<WarlockEssenceCorruptionComponent>(user);
        corruption.Doses++;
        corruption.NextTick = _timing.CurTime + TimeSpan.FromSeconds(corruption.TickInterval);

        _audio.PlayPvs(DrinkSound, user);
        _popup.PopupEntity(Loc.GetString("warlock-essence-drunk"), user, user, PopupType.Medium);

        ApplyCorruption((user, corruption));

        QueueDel(ent);
    }

    #endregion

    #region Разгон

    private void OnHighSpeed(Entity<WarlockEssenceHighComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.SpeedModifier);
    }

    private void OnHighStamina(Entity<WarlockEssenceHighComponent> ent, ref RefreshStaminaCritThresholdEvent args)
    {
        args.Modifier *= ent.Comp.StaminaModifier;
    }

    private void OnHighDamage(Entity<WarlockEssenceHighComponent> ent, ref DamageModifyEvent args)
    {
        args.Damage *= ent.Comp.DamageModifier;
    }

    private void EndHigh(EntityUid uid)
    {
        RemCompDeferred<WarlockEssenceHighComponent>(uid);

        _movement.RefreshMovementSpeedModifiers(uid);
        _stamina.RefreshStaminaCritThreshold(uid);

        _popup.PopupEntity(Loc.GetString("warlock-essence-high-over"), uid, uid, PopupType.MediumCaution);
    }

    #endregion

    #region Расплата

    /// <summary>
    /// Приводит тело в соответствие числу выпитых доз. Вызывается на каждой дозе
    /// и ничего не снимает: пороги работают только вверх.
    /// </summary>
    private void ApplyCorruption(Entity<WarlockEssenceCorruptionComponent> ent)
    {
        var doses = ent.Comp.Doses;

        if (doses == ent.Comp.BabbleThreshold)
            _popup.PopupEntity(Loc.GetString("warlock-essence-babble-start"), ent, ent, PopupType.LargeCaution);

        // Дар глохнет насовсем. Ирония в том, что эссенцию пьют ради силы,
        // а платят за неё именно той силой, которая была своей.
        if (doses >= ent.Comp.SilenceThreshold && !HasComp<WarlockPsiSuppressedComponent>(ent))
        {
            EnsureComp<WarlockPsiSuppressedComponent>(ent);
            _popup.PopupEntity(Loc.GetString("warlock-essence-silence"), ent, ent, PopupType.LargeCaution);
        }

        if (doses == ent.Comp.PoisonThreshold)
            _popup.PopupEntity(Loc.GetString("warlock-essence-poison-start"), ent, ent, PopupType.LargeCaution);

        if (doses == ent.Comp.LethalThreshold)
            _popup.PopupEntity(Loc.GetString("warlock-essence-lethal"), ent, ent, PopupType.LargeCaution);
    }

    private void TickCorruption(Entity<WarlockEssenceCorruptionComponent> ent)
    {
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.TickInterval);

        var doses = ent.Comp.Doses;

        if (doses < ent.Comp.PoisonThreshold)
            return;

        // Яд растёт с каждой лишней дозой, а после последнего порога перестаёт считаться
        // и просто убивает.
        var poison = doses >= ent.Comp.LethalThreshold
            ? ent.Comp.LethalPoison
            : (doses - ent.Comp.PoisonThreshold + 1) * ent.Comp.PoisonPerDose;

        _damageable.TryChangeDamage(
            ent.Owner,
            new DamageSpecifier { DamageDict = { ["Poison"] = poison } },
            origin: ent.Owner);
    }

    /// <summary>
    /// Речь начинает разъезжаться раньше, чем тело. С третьей дозы говорить внятно
    /// получается через раз, дальше — почти никогда.
    /// </summary>
    private void OnCorruptedAccent(Entity<WarlockEssenceCorruptionComponent> ent, ref AccentGetEvent args)
    {
        if (ent.Comp.Doses < ent.Comp.BabbleThreshold)
            return;

        // Чем больше выпито, тем чаще наружу лезет улей вместо слов.
        var chance = MathF.Min(0.25f * (ent.Comp.Doses - ent.Comp.BabbleThreshold + 1), 0.9f);

        if (!_random.Prob(chance))
            return;

        args.Message = WarlockWildArtefactSystem.Speak(args.Message);
    }

    private void OnCorruptionExamined(Entity<WarlockEssenceCorruptionComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Doses < ent.Comp.BabbleThreshold)
            return;

        args.PushMarkup(Loc.GetString("warlock-essence-examine-corrupted"));
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var highs = EntityQueryEnumerator<WarlockEssenceHighComponent>();
        while (highs.MoveNext(out var uid, out var high))
        {
            if (now >= high.EndAt)
                EndHigh(uid);
        }

        var corrupted = EntityQueryEnumerator<WarlockEssenceCorruptionComponent>();
        while (corrupted.MoveNext(out var uid, out var corruption))
        {
            if (now < corruption.NextTick)
                continue;

            TickCorruption((uid, corruption));

            // Жучий язык вырывается сам, без спроса и без повода.
            if (corruption.Doses < corruption.BabbleThreshold || !_random.Prob(0.2f))
                continue;

            _chat.TrySendInGameICMessage(
                uid,
                Loc.GetString(_random.Pick(Babble)),
                InGameICChatType.Speak,
                ChatTransmitRange.Normal,
                ignoreActionBlocker: true);
        }
    }
}
