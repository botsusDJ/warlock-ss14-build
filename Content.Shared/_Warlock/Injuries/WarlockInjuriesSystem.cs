using System.Linq;
using System.Text;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.HealthExaminable;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.Standing;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Warlock.Injuries;

/// <summary>
/// _Warlock
/// Летопись тела. Ваниль считает урон и смерть — эта система считает следы и помнит,
/// где именно они остались.
///
/// Локационного урона в билде нет, поэтому часть тела выбирается броском с весами:
/// в торс прилетает чаще всего, в голову заметно реже. Отсюда и редкость выбитых зубов,
/// а глаз теряется совсем редко — это должно быть событием на смену.
///
/// Смотреть летопись можно только в лицо: под шлемом её не разглядеть.
/// </summary>
public sealed partial class WarlockInjuriesSystem : EntitySystem
{
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    /// <summary>
    /// Веса попадания по частям тела. Торс самый крупный, голова самая мелкая.
    /// </summary>
    private static readonly (WarlockBodyPart Part, float Weight)[] PartWeights =
    {
        (WarlockBodyPart.Torso, 35f),
        (WarlockBodyPart.Head, 15f),
        (WarlockBodyPart.LeftArm, 12.5f),
        (WarlockBodyPart.RightArm, 12.5f),
        (WarlockBodyPart.LeftLeg, 12.5f),
        (WarlockBodyPart.RightLeg, 12.5f),
    };

    /// <summary>
    /// Травмы, которые не сходят никогда.
    /// </summary>
    private static bool IsPermanent(WarlockInjuryType type) => type
        is WarlockInjuryType.Scar
        or WarlockInjuryType.Brand
        or WarlockInjuryType.MissingTooth
        or WarlockInjuryType.MissingEye;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarlockInjuriesComponent, DamageDealtEvent>(OnDamageDealt);
        SubscribeLocalEvent<WarlockInjuriesComponent, HealthBeingExaminedEvent>(OnHealthExamined);
        SubscribeLocalEvent<WarlockInjuriesComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<WarlockInjuriesComponent, RefreshStaminaCritThresholdEvent>(OnRefreshStamina);

        // Выбитые зубы слышно.
        SubscribeLocalEvent<WarlockInjuriesComponent, AccentGetEvent>(OnAccent);

        // Сломанные конечности мешают жить: руки не держат, ноги не носят.
        SubscribeLocalEvent<WarlockInjuriesComponent, PickupAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<WarlockInjuriesComponent, StandAttemptEvent>(OnStandAttempt);
    }

    #region Публичное API

    public int Count(Entity<WarlockInjuriesComponent?> ent, WarlockInjuryType type)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return 0;

        return ent.Comp.Injuries.Count(i => i.Type == type);
    }

    /// <summary>
    /// Добавляет травму на указанную часть тела. Возвращает false, если по этому виду уже потолок.
    /// </summary>
    public bool TryAddInjury(Entity<WarlockInjuriesComponent?> ent, WarlockInjuryType type, WarlockBodyPart part, string? text = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        var cap = type switch
        {
            WarlockInjuryType.MissingTooth => ent.Comp.MaxTeeth,
            // Глаза ровно два, и второй отнимать нельзя: слепой персонаж — это уже не травма,
            // а конец игры за него.
            WarlockInjuryType.MissingEye => 1,
            _ => ent.Comp.MaxPerType,
        };

        if (Count(ent, type) >= cap)
            return false;

        ent.Comp.Injuries.Add(new WarlockInjury(type, part, text));
        OnInjuriesChanged((ent, ent.Comp));
        return true;
    }

    /// <summary>
    /// Снимает одну травму указанного вида. Вечные травмы этим не убрать.
    /// </summary>
    public bool TryRemoveInjury(Entity<WarlockInjuriesComponent?> ent, WarlockInjuryType type)
    {
        if (!Resolve(ent, ref ent.Comp, false) || IsPermanent(type))
            return false;

        var index = ent.Comp.Injuries.FindIndex(i => i.Type == type);
        if (index < 0)
            return false;

        ent.Comp.Injuries.RemoveAt(index);
        OnInjuriesChanged((ent, ent.Comp));
        return true;
    }

    /// <summary>
    /// Ставит именное клеймо на выбранное место. Снять нельзя.
    /// </summary>
    public void AddBrand(Entity<WarlockInjuriesComponent?> ent, string brand, WarlockBodyPart part)
    {
        TryAddInjury(ent, WarlockInjuryType.Brand, part, brand);
    }

    /// <summary>
    /// Заживляет всё, что вообще способно зажить. Шрамы, клейма, зубы и глаза остаются.
    /// </summary>
    public void HealAll(Entity<WarlockInjuriesComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.Injuries.RemoveAll(i => !IsPermanent(i.Type));
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

        ApplyLimbConsequences(ent);
    }

    private WarlockBodyPart RollPart()
    {
        var total = PartWeights.Sum(p => p.Weight);
        var roll = _random.NextFloat() * total;

        foreach (var (part, weight) in PartWeights)
        {
            roll -= weight;
            if (roll <= 0f)
                return part;
        }

        return WarlockBodyPart.Torso;
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

        if (blunt >= ent.Comp.FractureThreshold)
        {
            var part = RollPart();
            TryAddInjury(ent.Owner, WarlockInjuryType.Fracture, part);

            // Перелом черепа — единственный способ лишиться глаза.
            if (part == WarlockBodyPart.Head && _random.Prob(ent.Comp.EyeChance))
                LoseEye(ent);
        }
        else if (blunt >= ent.Comp.BruiseThreshold)
        {
            var part = RollPart();
            TryAddInjury(ent.Owner, WarlockInjuryType.Bruise, part);

            // Крепкий удар в челюсть стоит зуба.
            if (part == WarlockBodyPart.Head && _random.Prob(ent.Comp.ToothChance))
                TryAddInjury(ent.Owner, WarlockInjuryType.MissingTooth, WarlockBodyPart.Head);
        }

        if (sharp >= ent.Comp.AbrasionThreshold)
            TryAddInjury(ent.Owner, WarlockInjuryType.Abrasion, RollPart());
    }

    private void LoseEye(Entity<WarlockInjuriesComponent> ent)
    {
        if (!TryAddInjury(ent.Owner, WarlockInjuryType.MissingEye, WarlockBodyPart.Head))
            return;

        _blindable.AdjustEyeDamage(ent.Owner, ent.Comp.EyeDamagePerEye);
    }

    #endregion

    #region Осмотр

    /// <summary>
    /// Какая одежда закрывает какое место. Хватает любого занятого слота из списка:
    /// клеймо на торсе не видно и под комбинезоном, и под мантией поверх него.
    /// </summary>
    private static readonly Dictionary<WarlockBodyPart, string[]> CoveringSlots = new()
    {
        [WarlockBodyPart.Head] = new[] { "head", "mask" },
        [WarlockBodyPart.Torso] = new[] { "jumpsuit", "outerClothing" },
        [WarlockBodyPart.LeftArm] = new[] { "jumpsuit", "outerClothing", "gloves" },
        [WarlockBodyPart.RightArm] = new[] { "jumpsuit", "outerClothing", "gloves" },
        [WarlockBodyPart.LeftLeg] = new[] { "jumpsuit", "outerClothing", "shoes" },
        [WarlockBodyPart.RightLeg] = new[] { "jumpsuit", "outerClothing", "shoes" },
    };

    /// <summary>
    /// Закрыто ли это место одеждой.
    /// </summary>
    private bool IsCovered(EntityUid uid, WarlockBodyPart part)
    {
        if (!CoveringSlots.TryGetValue(part, out var slots))
            return false;

        foreach (var slot in slots)
        {
            if (_inventory.TryGetSlotEntity(uid, slot, out _))
                return true;
        }

        return false;
    }

    private void OnHealthExamined(Entity<WarlockInjuriesComponent> ent, ref HealthBeingExaminedEvent args)
    {
        if (ent.Comp.Injuries.Count == 0)
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString("warlock-injuries-none"));
            return;
        }

        // Одежда прячет следы: под шлемом не читается лицо, под комбинезоном — торс и конечности.
        // Клеймо на плече — это метка, которую можно скрыть, надев рукав, и это намеренно.
        var visible = ent.Comp.Injuries.Where(i => !IsCovered(ent.Owner, i.Part)).ToList();
        var hidden = ent.Comp.Injuries.Count - visible.Count;

        if (visible.Count == 0)
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString("warlock-injuries-all-covered"));
            return;
        }

        // Группируем по «вид + место», чтобы вместо шести строк про синяки
        // получилась одна с указанием, где именно их шесть.
        var groups = visible
            .Where(i => i.Type != WarlockInjuryType.Brand)
            .GroupBy(i => (i.Type, i.Part))
            .OrderBy(g => g.Key.Type)
            .ThenBy(g => g.Key.Part);

        foreach (var group in groups)
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString(
                GetInjuryLoc(group.Key.Type, group.Count()),
                ("part", Loc.GetString(GetPartLoc(group.Key.Part))),
                ("count", group.Count())));
        }

        // Клейма пишем каждое отдельно: у них своя надпись и своё место.
        foreach (var brand in visible.Where(i => i.Type == WarlockInjuryType.Brand))
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString(
                "warlock-injuries-brand",
                ("part", Loc.GetString(GetPartLoc(brand.Part))),
                ("brand", brand.Text is { } t ? Loc.GetString(t) : Loc.GetString("warlock-brand-unreadable"))));
        }

        // Про скрытое сообщаем фактом, без подробностей: видно, что одежда что-то закрывает.
        if (hidden > 0)
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString("warlock-injuries-partly-covered"));
        }
    }

    /// <summary>
    /// Подбирает строку по виду травмы и по тому, много её или мало.
    /// </summary>
    private static string GetInjuryLoc(WarlockInjuryType type, int count)
    {
        // У зубов и глаз тяжести нет: они либо есть, либо нет.
        if (type == WarlockInjuryType.MissingTooth)
            return "warlock-injuries-tooth";

        if (type == WarlockInjuryType.MissingEye)
            return "warlock-injuries-eye";

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

    public static string GetPartLoc(WarlockBodyPart part)
    {
        return part switch
        {
            WarlockBodyPart.Head => "warlock-body-part-head",
            WarlockBodyPart.Torso => "warlock-body-part-torso",
            WarlockBodyPart.LeftArm => "warlock-body-part-left-arm",
            WarlockBodyPart.RightArm => "warlock-body-part-right-arm",
            WarlockBodyPart.LeftLeg => "warlock-body-part-left-leg",
            _ => "warlock-body-part-right-leg",
        };
    }

    #endregion

    #region Механические последствия

    private void OnRefreshSpeed(Entity<WarlockInjuriesComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        // Замедляет только сломанная нога. Рука в переломе бегать не мешает.
        var legs = ent.Comp.Injuries.Count(i =>
            i.Type == WarlockInjuryType.Fracture &&
            i.Part is WarlockBodyPart.LeftLeg or WarlockBodyPart.RightLeg);

        if (legs <= 0)
            return;

        args.ModifySpeed(MathF.Pow(ent.Comp.LegFractureSlowdown, legs));
    }

    /// <summary>
    /// Сломана ли рука с этой стороны. Средние руки (у кого их больше двух)
    /// считаем целыми: сопоставить их с левой или правой всё равно нечем.
    /// </summary>
    private bool IsArmBroken(WarlockInjuriesComponent comp, HandLocation location)
    {
        var part = location switch
        {
            HandLocation.Left => WarlockBodyPart.LeftArm,
            HandLocation.Right => WarlockBodyPart.RightArm,
            _ => (WarlockBodyPart?) null,
        };

        return part is { } p && comp.Injuries.Any(i => i.Type == WarlockInjuryType.Fracture && i.Part == p);
    }

    /// <summary>
    /// Сколько ног сломано. Две — это уже не хромота, а лежачее положение.
    /// </summary>
    private static int BrokenLegs(WarlockInjuriesComponent comp) => comp.Injuries.Count(i =>
        i.Type == WarlockInjuryType.Fracture &&
        i.Part is WarlockBodyPart.LeftLeg or WarlockBodyPart.RightLeg);

    /// <summary>
    /// Сломанной рукой ничего не поднять. Если целых рук не осталось — не поднять вообще ничем.
    /// </summary>
    private void OnPickupAttempt(Entity<WarlockInjuriesComponent> ent, ref PickupAttemptEvent args)
    {
        if (args.Cancelled || !TryComp<HandsComponent>(ent, out var hands))
            return;

        // Проверяем именно активную руку: именно в неё ваниль и кладёт поднятое.
        if (hands.ActiveHandId is not { } active || !hands.Hands.TryGetValue(active, out var hand))
            return;

        if (!IsArmBroken(ent.Comp, hand.Location))
            return;

        args.Cancel();

        if (args.ShowPopup)
            _popup.PopupClient(Loc.GetString("warlock-injuries-arm-broken"), ent, ent);
    }

    /// <summary>
    /// На двух сломанных ногах не встают.
    /// </summary>
    private void OnStandAttempt(Entity<WarlockInjuriesComponent> ent, ref StandAttemptEvent args)
    {
        if (BrokenLegs(ent.Comp) >= 2)
            args.Cancel();
    }

    /// <summary>
    /// Проверяет, не изменился ли расклад по конечностям, и приводит тело в соответствие:
    /// сломанная рука роняет всё, что держала, на двух сломанных ногах персонаж падает.
    /// </summary>
    private void ApplyLimbConsequences(Entity<WarlockInjuriesComponent> ent)
    {
        // Ронять предметы и валить наземь имеет право только сервер: на клиенте
        // это разъедется с предсказанием и предмет будет прыгать из руки на пол и обратно.
        if (!_net.IsServer)
            return;

        if (TryComp<HandsComponent>(ent, out var hands))
        {
            foreach (var (id, hand) in hands.Hands)
            {
                if (!IsArmBroken(ent.Comp, hand.Location))
                    continue;

                if (_hands.TryGetHeldItem((ent.Owner, hands), id, out _))
                    _hands.TryDrop((ent.Owner, hands), id, checkActionBlocker: false);
            }
        }

        if (BrokenLegs(ent.Comp) >= 2 && !_standing.IsDown(ent.Owner))
            _standing.Down(ent.Owner);
    }

    #endregion

    #region Речь

    /// <summary>
    /// Без зубов не выговорить свистящие. Чем больше дыр, тем меньше внятного.
    /// </summary>
    private void OnAccent(Entity<WarlockInjuriesComponent> ent, ref AccentGetEvent args)
    {
        var teeth = Count(ent.Owner, WarlockInjuryType.MissingTooth);

        if (teeth < ent.Comp.LispThreshold)
            return;

        args.Message = Lisp(args.Message, teeth >= ent.Comp.HeavyLispThreshold);
    }

    /// <summary>
    /// Шепелявость. На лёгкой стадии уходят только свистящие, на тяжёлой ещё и взрывные
    /// губные — их без передних зубов тоже не собрать.
    /// </summary>
    public static string Lisp(string message, bool heavy)
    {
        var sb = new StringBuilder(message.Length);

        foreach (var c in message)
        {
            var replaced = c switch
            {
                'с' => 'ш',
                'С' => 'Ш',
                'з' => 'ж',
                'З' => 'Ж',
                'ц' => 'ч',
                'Ц' => 'Ч',
                _ => c,
            };

            if (heavy)
            {
                replaced = replaced switch
                {
                    'ч' => 'щ',
                    'Ч' => 'Щ',
                    'т' => 'ф',
                    'Т' => 'Ф',
                    'д' => 'в',
                    'Д' => 'В',
                    _ => replaced,
                };
            }

            sb.Append(replaced);
        }

        return sb.ToString();
    }

    #endregion

    #region Механические последствия, часть вторая

    private void OnRefreshStamina(Entity<WarlockInjuriesComponent> ent, ref RefreshStaminaCritThresholdEvent args)
    {
        var bruises = Count(ent.Owner, WarlockInjuryType.Bruise);

        // Переломы не в ногах бегать не мешают, но выматывают.
        var fractures = ent.Comp.Injuries.Count(i =>
            i.Type == WarlockInjuryType.Fracture &&
            i.Part is not (WarlockBodyPart.LeftLeg or WarlockBodyPart.RightLeg));

        if (bruises > 0)
            args.Modifier *= MathF.Pow(ent.Comp.BruiseStaminaPenalty, bruises);

        if (fractures > 0)
            args.Modifier *= MathF.Pow(ent.Comp.FractureStaminaPenalty, fractures);
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
    /// перелом срастается через несколько шагов и оставляет шрам на том же месте.
    /// </summary>
    private void Heal(Entity<WarlockInjuriesComponent> ent)
    {
        TryRemoveInjury(ent.Owner, WarlockInjuryType.Abrasion);

        // Синяк сходит через раз, чтобы держался заметно дольше ссадины.
        if (_random.Prob(0.5f))
            TryRemoveInjury(ent.Owner, WarlockInjuryType.Bruise);

        var index = ent.Comp.Injuries.FindIndex(i => i.Type == WarlockInjuryType.Fracture);
        if (index < 0)
        {
            ent.Comp.FractureProgress = 0;
            return;
        }

        ent.Comp.FractureProgress++;

        if (ent.Comp.FractureProgress < ent.Comp.FractureTicksToHeal)
            return;

        ent.Comp.FractureProgress = 0;

        // Сросшаяся кость всегда оставляет след, и след остаётся там же, где был перелом.
        var part = ent.Comp.Injuries[index].Part;
        ent.Comp.Injuries.RemoveAt(index);
        ent.Comp.Injuries.Add(new WarlockInjury(WarlockInjuryType.Scar, part));

        OnInjuriesChanged(ent);
    }
}
