using System.Linq;
using System.Numerics;
using Content.Shared._Warlock.Artefacts;
using Content.Shared._Warlock.Injuries;
using Content.Shared._Warlock.Psionics;
using Content.Shared._Warlock.Unathi;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Server.Atmos.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Warlock.Artefacts;

/// <summary>
/// _Warlock
/// Малые артефакты со случайным содержимым — замена ванильной ксеноархеологии.
///
/// Претензия к ванильной системе была не в том, что она случайная, а в том, что она
/// молчаливая: игрок тыкает камень, узел срабатывает где-то в отчёте, и понять, что
/// именно произошло, можно только по консоли исследователя. Здесь наоборот — каждый
/// эффект виден сразу и почти каждый что-то делает лично с тем, кто держит камень.
///
/// Второе отличие: содержимое катится один раз при появлении и больше не меняется.
/// Два одинаковых с виду камня всегда делают одно и то же, поэтому находки запоминают
/// и о них договариваются.
/// </summary>
public sealed partial class WarlockRandomArtefactSystem : EntitySystem
{
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private WarlockInjuriesSystem _injuries = default!;
    [Dependency] private WarlockPsionicsSystem _psionics = default!;

    private static readonly SoundPathSpecifier UseSound = new("/Audio/Magic/staff_chaos.ogg");

    /// <summary>
    /// Из чего собираются имена. Прилагательное плюс существительное — так находка звучит
    /// как находка, а не как «артефакт №417», и её удобно называть вслух.
    /// </summary>
    private static readonly string[] Adjectives =
    {
        "warlock-relic-adj-1", "warlock-relic-adj-2", "warlock-relic-adj-3",
        "warlock-relic-adj-4", "warlock-relic-adj-5", "warlock-relic-adj-6",
        "warlock-relic-adj-7", "warlock-relic-adj-8", "warlock-relic-adj-9",
        "warlock-relic-adj-10", "warlock-relic-adj-11", "warlock-relic-adj-12",
    };

    private static readonly string[] Nouns =
    {
        "warlock-relic-noun-1", "warlock-relic-noun-2", "warlock-relic-noun-3",
        "warlock-relic-noun-4", "warlock-relic-noun-5", "warlock-relic-noun-6",
        "warlock-relic-noun-7", "warlock-relic-noun-8", "warlock-relic-noun-9",
        "warlock-relic-noun-10",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockRandomArtefactComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<WarlockRandomArtefactComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<WarlockRandomArtefactComponent, ExaminedEvent>(OnExamined);
    }

    #region Раскатка

    private void OnMapInit(Entity<WarlockRandomArtefactComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Rolled)
            return;

        ent.Comp.Rolled = true;

        // Эффекты без повторов: два одинаковых в одном камне выглядели бы как ошибка.
        var pool = Enum.GetValues<WarlockArtefactEffect>().ToList();
        _random.Shuffle(pool);

        var count = _random.Next(ent.Comp.MinEffects, ent.Comp.MaxEffects + 1);
        ent.Comp.Effects = pool.Take(count).ToList();

        // Чем больше умеет, тем реже срабатывает. Иначе трёхэффектный камень был бы
        // строго лучше одноэффектного, и выбор пропал бы.
        var delay = _random.NextFloat(ent.Comp.MinDelay, ent.Comp.MaxDelay) * (0.7f + 0.35f * count);
        ent.Comp.Delay = delay;
        ent.Comp.NextUse = _timing.CurTime;

        Dirty(ent);

        // Внешность катает ванильный RandomSprite прямо в прототипе: SpriteComponent
        // существует только на клиенте, и трогать его отсюда нельзя. Здесь остаётся
        // только свет — он серверный — и имя.
        if (_light.TryGetLight(ent.Owner, out var light))
            _light.SetRadius(ent.Owner, 1.2f + 0.4f * count, light);

        _meta.SetEntityName(ent.Owner, Loc.GetString(
            "warlock-relic-name",
            ("adj", Loc.GetString(_random.Pick(Adjectives))),
            ("noun", Loc.GetString(_random.Pick(Nouns)))));
    }

    #endregion

    #region Осмотр

    private void OnExamined(Entity<WarlockRandomArtefactComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("warlock-relic-examine", ("uses", ent.Comp.Uses)));

        // Обычному игроку осмотр содержимого не выдаёт: узнать можно только применив,
        // и в этом весь риск находки. Но тот, кто умеет читать камень, видит всё сразу —
        // высшая каста унатхов, ритуалисты, архимаги и Фактос целиком. Это и есть
        // их главная ценность в отряде: они превращают лотерею в работу.
        if (HasComp<WarlockArtefactSightComponent>(args.Examiner))
        {
            foreach (var effect in ent.Comp.Effects)
            {
                args.PushMarkup(Loc.GetString("warlock-relic-sight",
                    ("effect", Loc.GetString($"warlock-relic-{effect.ToString().ToLowerInvariant()}"))));
            }

            if (ent.Comp.Effects.Count == 0)
                args.PushMarkup(Loc.GetString("warlock-relic-sight-empty"));
        }

        if (_timing.CurTime < ent.Comp.NextUse)
            args.PushMarkup(Loc.GetString("warlock-relic-examine-cooling"));
    }

    #endregion

    #region Применение

    private void OnUse(Entity<WarlockRandomArtefactComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (_timing.CurTime < ent.Comp.NextUse)
        {
            _popup.PopupEntity(Loc.GetString("warlock-relic-cooling"), ent, args.User);
            return;
        }

        args.Handled = true;

        ent.Comp.NextUse = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Delay);
        ent.Comp.Uses++;
        Dirty(ent);

        _audio.PlayPvs(UseSound, ent);

        foreach (var effect in ent.Comp.Effects)
        {
            Apply(ent, args.User, effect);
        }
    }

    private void Apply(Entity<WarlockRandomArtefactComponent> ent, EntityUid user, WarlockArtefactEffect effect)
    {
        switch (effect)
        {
            case WarlockArtefactEffect.Mend:
                // HealEvenly, а не отрицательный урон по списку типов: он снимает ровно
                // двадцать и размазывает их по тем типам, которые у цели действительно есть.
                // Отрицательный урон в тип, которого нет, просто пропадёт.
                _damageable.HealEvenly(user, -20f, origin: ent.Owner);
                Say(ent, user, "warlock-relic-mend");
                break;

            case WarlockArtefactEffect.Bite:
                Hurt(user, "Piercing", 14f, ent);
                Say(ent, user, "warlock-relic-bite", PopupType.MediumCaution);
                break;

            case WarlockArtefactEffect.Feed:
                _psionics.RestoreEnergy(user, 35);
                Say(ent, user, "warlock-relic-feed");
                break;

            case WarlockArtefactEffect.Drain:
                _psionics.TryUseEnergy(user, 100);
                Say(ent, user, "warlock-relic-drain", PopupType.MediumCaution);
                break;

            case WarlockArtefactEffect.Shove:
                Sweep(ent, user, away: true);
                Say(ent, user, "warlock-relic-shove");
                break;

            case WarlockArtefactEffect.Pull:
                Sweep(ent, user, away: false);
                Say(ent, user, "warlock-relic-pull");
                break;

            case WarlockArtefactEffect.Flash:
                foreach (var victim in Nearby(ent, 5f))
                    _blindable.AdjustEyeDamage(victim, 6);
                Say(ent, user, "warlock-relic-flash", PopupType.LargeCaution);
                break;

            case WarlockArtefactEffect.Dim:
                foreach (var victim in Nearby(ent, 7f))
                    _blindable.AdjustEyeDamage(victim, 2);
                Say(ent, user, "warlock-relic-dim");
                break;

            case WarlockArtefactEffect.Break:
                _injuries.TryAddInjury(user, WarlockInjuryType.Fracture, RollLimb());
                Say(ent, user, "warlock-relic-break", PopupType.LargeCaution);
                break;

            case WarlockArtefactEffect.Mark:
                _injuries.AddBrand(user, "warlock-relic-brand", WarlockBodyPart.Torso);
                Say(ent, user, "warlock-relic-mark", PopupType.MediumCaution);
                break;

            case WarlockArtefactEffect.Toss:
                Toss(ent, user);
                Say(ent, user, "warlock-relic-toss");
                break;

            case WarlockArtefactEffect.Rot:
                foreach (var victim in Nearby(ent, 4f))
                    Hurt(victim, "Poison", 10f, ent);
                Say(ent, user, "warlock-relic-rot", PopupType.MediumCaution);
                break;

            case WarlockArtefactEffect.Kindle:
                // Через систему, а не правкой поля: поля FlammableComponent закрыты
                // песочницей, и ignite надо поджигать штатно.
                foreach (var victim in Nearby(ent, 3f))
                    _flammable.AdjustFireStacks(victim, 3f, ignite: true);
                Say(ent, user, "warlock-relic-kindle", PopupType.LargeCaution);
                break;

            case WarlockArtefactEffect.Shed:
                Spawn("WarlockKhritCarapacePlate", Transform(ent).Coordinates);
                Say(ent, user, "warlock-relic-shed");
                break;
        }
    }

    #endregion

    #region Мелочи

    private void Say(EntityUid artefact, EntityUid user, string key, PopupType type = PopupType.Medium)
    {
        _popup.PopupEntity(Loc.GetString(key), artefact, user, type);
    }

    private void Hurt(EntityUid target, string kind, float amount, EntityUid origin)
    {
        _damageable.TryChangeDamage(
            target,
            new DamageSpecifier { DamageDict = { [kind] = amount } },
            origin: origin);
    }

    /// <summary>
    /// Всё живое вокруг артефакта, включая того, кто его держит.
    /// </summary>
    private IEnumerable<EntityUid> Nearby(EntityUid artefact, float radius)
    {
        foreach (var candidate in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(artefact).Coordinates, radius))
        {
            if (!_mobState.IsDead(candidate.Owner))
                yield return candidate.Owner;
        }
    }

    /// <summary>
    /// Раскидать или стянуть всё незакреплённое вокруг.
    /// </summary>
    private void Sweep(EntityUid artefact, EntityUid user, bool away)
    {
        var origin = _transform.GetMapCoordinates(artefact);

        foreach (var victim in _lookup.GetEntitiesInRange(Transform(artefact).Coordinates, 4f))
        {
            if (victim == artefact || victim == user || TerminatingOrDeleted(victim))
                continue;

            if (Transform(victim).Anchored || !HasComp<PhysicsComponent>(victim))
                continue;

            if (_container.IsEntityInContainer(victim))
                continue;

            var here = _transform.GetMapCoordinates(victim);

            if (here.MapId != origin.MapId)
                continue;

            var delta = away ? here.Position - origin.Position : origin.Position - here.Position;

            if (delta.LengthSquared() < 0.01f)
                delta = new Vector2(1f, 0f);

            _throwing.TryThrow(victim, delta, 9f, artefact, playSound: false);
        }
    }

    private void Toss(EntityUid artefact, EntityUid user)
    {
        var direction = new Vector2(_random.NextFloat(-1f, 1f), _random.NextFloat(-1f, 1f));

        if (direction.LengthSquared() < 0.01f)
            direction = new Vector2(1f, 0f);

        _throwing.TryThrow(user, direction * 6f, 10f, artefact);
    }

    private WarlockBodyPart RollLimb()
    {
        return _random.Pick(new[]
        {
            WarlockBodyPart.LeftArm,
            WarlockBodyPart.RightArm,
            WarlockBodyPart.LeftLeg,
            WarlockBodyPart.RightLeg,
        });
    }

    #endregion
}
