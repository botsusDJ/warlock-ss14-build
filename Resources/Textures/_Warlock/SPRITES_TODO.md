# Warlock — что ещё нужно нарисовать

Аудит на 12 августа 2026. Проверено скриптом по всем прототипам `Resources/Prototypes/_Warlock`.

**Готово: 6 из 110.** Все шесть — мантии гильдий, лежат в
`_Warlock/Clothing/OuterClothing/Robes/`.

Всё остальное сейчас ходит на ванильных плейсхолдерах: игра не падает, но выглядит как ваниль.

## Как читать таблицы

- **Сейчас** — какой ванильный спрайт подставлен. Можно открыть и посмотреть, на что это похоже.
- **Куда класть** — путь внутри `Resources/Textures/_Warlock/`. Папка обязана кончаться на `.rsi`
  и содержать `meta.json`.
- **Состояния** — какие имена состояний обязаны быть в `meta.json`. Если хоть одного не хватает,
  предмет уйдёт в «пропавшую текстуру».

Всё, что ниже, — **32×32**, кроме иконок должностей (8×8).

Состояния с направлениями (`equipped-*`, `inhand-*`) рисуются в **4 направления**:
Юг, Север, Восток, Запад — именно в этом порядке. Лист может быть `4×1` или `2×2`,
загрузчик считает столбцы как `ширина / 32`.

---

# 1. Одежда — 18 предметов

## Братство Стали (5)

| Предмет | Сейчас | Куда класть | Состояния |
|---|---|---|---|
| форма Братства Стали | `Clothing/Uniforms/Jumpsuit/mercenary.rsi` | `Clothing/Uniforms/Jumpsuit/brotherhood.rsi` | `icon`, `equipped-INNERCLOTHING`, `inhand-left`, `inhand-right` |
| броня Братства Стали | `Clothing/OuterClothing/Armor/heavy.rsi` | `Clothing/OuterClothing/Armor/brotherhood.rsi` | `icon`, `equipped-OUTERCLOTHING`, `inhand-left`, `inhand-right` |
| броня командира Братства | `Clothing/OuterClothing/Armor/heavy.rsi` | `Clothing/OuterClothing/Armor/brotherhood_command.rsi` | то же |
| шлем Братства Стали | `Clothing/Head/Helmets/merc_helmet.rsi` | `Clothing/Head/Helmets/brotherhood.rsi` | `icon`, `equipped-HELMET`, `inhand-left`, `inhand-right` |
| шлем командира Братства | `Clothing/Head/Helmets/merc_helmet.rsi` | `Clothing/Head/Helmets/brotherhood_command.rsi` | то же |
| респиратор Братства Стали | `Clothing/Mask/gas.rsi` | `Clothing/Mask/brotherhood.rsi` | `icon`, `equipped-MASK`, `inhand-left`, `inhand-right` |

Шлем закрывает лицо, и это уже работает механически: под шлемом не читаются травмы и клейма.
Значит шлем должен выглядеть глухим, без открытого лица.

## Гильдии техномагов (6)

Мантии готовы. Нужны комбинезоны под них и капюшоны.

| Предмет | Сейчас | Куда класть | Состояния |
|---|---|---|---|
| комбинезон Фактос | `Clothing/Uniforms/Jumpsuit/scientist.rsi` | `Clothing/Uniforms/Jumpsuit/factos.rsi` | `icon`, `equipped-INNERCLOTHING`, `inhand-left`, `inhand-right` |
| комбинезон Технос | `Clothing/Uniforms/Jumpsuit/engineering.rsi` | `Clothing/Uniforms/Jumpsuit/technos.rsi` | то же |
| облачение Варлок | `Clothing/Uniforms/Jumpsuit/chaplain.rsi` | `Clothing/Uniforms/Jumpsuit/warlock.rsi` | то же |
| капюшон Фактос | `Clothing/Head/Hoods/chaplain.rsi` | `Clothing/Head/Hoods/factos.rsi` | `icon`, `equipped-HELMET`, `inhand-left`, `inhand-right` |
| капюшон Технос | `Clothing/Head/Hoods/rad.rsi` | `Clothing/Head/Hoods/technos.rsi` | то же |
| капюшон Варлок | `Clothing/Head/Hoods/cult.rsi` | `Clothing/Head/Hoods/warlock.rsi` | то же |

## Королевство Унатхи (5)

| Предмет | Сейчас | Куда класть | Состояния |
|---|---|---|---|
| чешуйчатый доспех | `Clothing/Uniforms/Jumpsuit/ancient.rsi` | `Clothing/Uniforms/Jumpsuit/unathi.rsi` | `icon`, `equipped-INNERCLOTHING`, `inhand-left`, `inhand-right` |
| одеяние надсмотрщика | `Clothing/Uniforms/Jumpsuit/ancient.rsi` | `Clothing/Uniforms/Jumpsuit/unathi_slaver.rsi` | то же |
| пластинчатый панцирь | `Clothing/OuterClothing/Armor/bone_armor.rsi` | `Clothing/OuterClothing/Armor/unathi_scales.rsi` | `icon`, `equipped-OUTERCLOTHING`, `inhand-left`, `inhand-right` |
| мантия Короля-Жреца | `Clothing/OuterClothing/Suits/shrine-maiden.rsi` | `Clothing/OuterClothing/Suits/unathi_royal.rsi` | то же |
| костяной шлем | `Clothing/Head/Helmets/bone_helmet.rsi` | `Clothing/Head/Helmets/unathi.rsi` | `icon`, `equipped-HELMET`, `inhand-left`, `inhand-right` |

Носят это ящеры. У унатхов своя форма головы, так что шлем и капюшон лучше сразу проверять
на `MobReptilian`, а не на человеке.

## Рабы (1)

| Предмет | Сейчас | Куда класть | Состояния |
|---|---|---|---|
| роба раба Братства | `Clothing/Uniforms/Jumpsuit/ancient.rsi` | `Clothing/Uniforms/Jumpsuit/slave.rsi` | `icon`, `equipped-INNERCLOTHING`, `inhand-left`, `inhand-right` |

Эту робу насильно надевает «Ошейник Покорности», так что она должна читаться как рабская
с первого взгляда и издалека.

---

# 2. Артефакты вымершей расы — 5

Общий тон: тёплый камень, чужая геометрия, ничего механического.

| Предмет | Сейчас | Куда класть | Состояния |
|---|---|---|---|
| Кость Судьбы | `Objects/Fun/dice.rsi::d20_20` | `Objects/Artefacts/fate_die.rsi` | `icon` + желательно `inhand-left`, `inhand-right` |
| Оковы Логики | `Clothing/Neck/Misc/bling.rsi::icon` | `Clothing/Neck/logic_shackles.rsi` | `icon`, `equipped-NECK`, `inhand-left`, `inhand-right` |
| Свеча Мёртвого Бога | `Objects/Misc/candles.rsi` | `Objects/Artefacts/deadgod_candle.rsi` | `candle-big`, `fire-big` (слой пламени отдельный), `inhand-left`, `inhand-right` |
| Перчатка Тысячи Рук | `Clothing/Hands/Gloves/powerglove.rsi::icon` | `Clothing/Hands/thousand_hands.rsi` | `icon`, `equipped-HAND`, `inhand-left`, `inhand-right` |
| Урна Трёх Гильдий | `Objects/Specific/Chapel/chaplainurn.rsi::icon` | `Objects/Artefacts/guild_urn.rsi` | `icon`, `inhand-left`, `inhand-right` |

---

# 3. Артефакты живых культов — 6

Тон другой: это сделали руками, недавно, и они злее. Механтехион — железо и заклёпки,
боги Королевства — кость, чешуя, кровь.

| Предмет | Сейчас | Куда класть | Состояния |
|---|---|---|---|
| Гвоздь Механтехиона | `Objects/Tools/crowbar.rsi::icon` | `Objects/Artefacts/mechantechion_nail.rsi` | `icon`, `inhand-left`, `inhand-right` |
| Неотступный Шестерён | `Objects/Misc/stock_parts.rsi::nano_mani` | `Objects/Artefacts/relentless_cog.rsi` | `icon`, `inhand-left`, `inhand-right` |
| Ремонтный Червь | `Objects/Misc/stock_parts.rsi::micro_laser` | `Objects/Artefacts/repair_worm.rsi` | `icon`, `inhand-left`, `inhand-right` |
| Ошейник Покорности | `Clothing/Neck/Misc/bling.rsi::icon` | `Clothing/Neck/subjugation_collar.rsi` | `icon`, `equipped-NECK`, `inhand-left`, `inhand-right` |
| Клык Атрака | `Objects/Weapons/Melee/kitchen_knife.rsi::icon` | `Objects/Weapons/Melee/atrak_fang.rsi` | `icon`, `inhand-left`, `inhand-right` |
| Семя Рузута | `Objects/Specific/Hydroponics/apple.rsi::seed` | `Objects/Artefacts/ruzut_seed.rsi` | `icon`, `inhand-left`, `inhand-right` |

Шестерён летает за хозяином по полу — он будет часто виден лежащим, а не в руках.
Гвоздь и Клык — оружие ближнего боя, руки для них обязательны.

---

# 4. Клейма — 3

Все три сейчас выглядят как сварочный аппарат.

| Предмет | Куда класть | Состояния |
|---|---|---|
| наборное клеймо | `Objects/Tools/branding_blank.rsi` | `icon`, `inhand-left`, `inhand-right` |
| клеймо Королевства | `Objects/Tools/branding_unathi.rsi` | то же |
| клеймо Братства | `Objects/Tools/branding_brotherhood.rsi` | то же |

У наборного надпись меняется игроком через меню, так что на иконке текста быть не должно —
пустая колодка с литерами. У двух других оттиск отлит намертво, его можно показать на иконке.

---

# 5. Культовая атрибутика — 7

| Предмет | Сейчас | Куда класть | Состояния |
|---|---|---|---|
| Свод Механтехиона | `Objects/Specific/Chapel/codexnanotrasimus.rsi` | `Objects/Religion/codex_mechantechion.rsi` | `icon`, `inhand-left`, `inhand-right` |
| знак Механтехиона | `Clothing/Neck/Misc/bling.rsi` | `Clothing/Neck/symbol_mechantechion.rsi` | `icon`, `equipped-NECK`, `inhand-left`, `inhand-right` |
| Скрижаль Касса | `Objects/Specific/Chapel/ratvartablet.rsi` | `Objects/Religion/tablet_kass.rsi` | `icon`, `inhand-left`, `inhand-right` |
| печать Касса | `Clothing/Neck/Misc/bling.rsi` | `Clothing/Neck/symbol_kass.rsi` | `icon`, `equipped-NECK`, `inhand-left`, `inhand-right` |
| фетиш Дьёкта | `Objects/Specific/Chapel/ratvartablet.rsi` | `Objects/Religion/fetish_djokt.rsi` | `icon`, `inhand-left`, `inhand-right` |
| фетиш Атрака | `Objects/Specific/Chapel/ratvartablet.rsi` | `Objects/Religion/fetish_atrak.rsi` | то же |
| фетиш Рузута | `Objects/Specific/Chapel/ratvartablet.rsi` | `Objects/Religion/fetish_ruzut.rsi` | то же |

Три фетиша сейчас неразличимы — одна и та же скрижаль. Их надо развести по силуэту, а не
по цвету: Дьёкт (хитрость, гордость), Атрак (кровь, честный бой), Рузут (жизнь, обман).

---

# 6. Постройки — 6

| Объект | Сейчас | Куда класть | Состояния |
|---|---|---|---|
| капище Механтехиона | `Structures/Furniture/Altars/Gods/nanotrasen.rsi::technology` | `Structures/Altars/mechantechion.rsi` | `full` (одно состояние) |
| обелиск Касса | `Structures/Furniture/Altars/Gods/convertaltar.rsi::white` | `Structures/Altars/kass.rsi` | `full` |
| алтарь Дьёкта | `…convertaltar.rsi::yellow` | `Structures/Altars/djokt.rsi` | `full` |
| алтарь Атрака | `…convertaltar.rsi::red` | `Structures/Altars/atrak.rsi` | `full` |
| алтарь Рузута | `…convertaltar.rsi::festival` | `Structures/Altars/ruzut.rsi` | `full` |
| отзвук мёртвого бога | `Structures/Magic/Cult/rune.rsi::cult5` | `Effects/deadgod_anchor.rsi` | `full`, желательно анимированный |

Отзвук — временный эффект от заклинания, живёт секунды. Анимация тут окупается.

---

# 7. Документы и допуски — 6

| Предмет | Сейчас | Куда класть | Состояния |
|---|---|---|---|
| пропуск Фактос | ванильный `id_cards.rsi::default` + иконка должности | `Objects/Misc/id_factos.rsi` | `icon`, `inhand-left`, `inhand-right` |
| пропуск Технос | то же | `Objects/Misc/id_technos.rsi` | то же |
| пропуск Варлок | то же | `Objects/Misc/id_warlock.rsi` | то же |
| костяная бирка унатхов | ванильный `id_cards.rsi::default` | `Objects/Misc/tag_unathi.rsi` | то же |
| ключ-карта Братства | `Objects/Misc/id_cards.rsi::default` | `Objects/Misc/key_brotherhood.rsi` | то же |
| командирская ключ-карта | `Objects/Misc/id_cards.rsi::default` | `Objects/Misc/key_brotherhood_command.rsi` | то же |

Ключ-карты одноразовые и их носят пачками — на иконке должно читаться, что это дешёвый
расходник, а не именной пропуск. Бирка унатхов вообще не электроника, а кость.

---

# 8. Иконки действий — 19

Все сидят на ванильном `Objects/Magic/magicactions.rsi`, поэтому половина заклинаний
выглядит одинаково. Формат 32×32, одно состояние `icon` на файл.

Класть в `Interface/Actions/`.

**Псионика (5):** Литания Расщепления · Хор Стали · Отзвук Мёртвого Бога ·
Заёмное Дыхание · Разделённая Участь

**Ритуалы (10):** Эхо-Копия · Проклятая Хватка · Печать Отбрасывания · Печать Гнили ·
Литания Укрепления · Чутьё Реликвий · Иссушающее Касание · Погребальный Костёр ·
Личина Брата · Жатва Дара

**Артефакты (1):** Рука Издалека

**Унатхи (2):** Боевой Клич · Священная Ярость

**Бой (1):** Сила удара — переключатель на три положения. Здесь одной иконки мало,
нужно три состояния (`weak`, `normal`, `strong`) или три файла.

Сейчас одинаковыми иконками сидят: Проклятая Хватка и Жатва Дара (`gib`),
Печать Гнили и Погребальный Костёр и Священная Ярость (`fireball`),
Хор Стали и Печать Отбрасывания и Боевой Клич (`repulse`),
Литания Укрепления и Заёмное Дыхание (`shield`),
Иссушающее Касание и Разделённая Участь (`magicmissile`),
Эхо-Копия и Рука Издалека (`item_recall`).

---

# 9. Интерфейс — 34

## Алерты (2)

| Алерт | Сейчас | Куда класть | Состояния |
|---|---|---|---|
| Пси-энергия | `Interface/Alerts/essence_counter.rsi` | `Interface/Alerts/psi_energy.rsi` | `essence0` + цифры `0`–`9` (счётчик рисует число поверх) |
| Сила удара | `Interface/Alerts/stamina.rsi` | `Interface/Alerts/attack_strength.rsi` | три состояния под слабый / средний / сильный |

Пси-энергия — счётчик, он обновляется каждый кадр и складывает число из отдельных
спрайтов цифр. Комплект цифр обязателен, иначе счётчик не соберётся.

## Иконки должностей — 32, размер 8×8

Все 32 сидят на ванильном `Interface/Misc/job_icons.rsi`, то есть техномаги ходят
с иконками учёных, а Братство — с иконками синдиката.

Проще сделать одним листом `Interface/Misc/job_icons_warlock.rsi` на 32 состояния.

- **Фактос (6):** Артархимаг · Великий Искатель · Чародей · Зачарователь · Маг · Адепт
- **Технос (6):** Архитехномаг · Верховный Технократ · Техночародей · Техномант · Маг · Адепт
- **Варлок (6):** Архимаг-Епископ · Верховный Жрец · Жрец · Ритуалист · Маг · Адепт
- **Унатхи (7):** Король-Жрец · Коготь Короля · Жрец Троих · Старший Клык · Клык ·
  Новая Кровь · Надсмотрщик рабов
- **Братство (7):** Лорд-коммандер · Капеллан · Лейтенант · Сержант · Боевой брат ·
  Мусорщик · Раб

Внутри гильдии два нижних звания (Маг и Адепт) сейчас делят одну иконку с соседним —
это можно оставить, если разводить 32 штуки на 8×8 окажется бессмысленно.

---

# Что спрайта не требует

- **Печати Отбрасывания и Гнили** — невидимы намеренно. Спрайта нет вообще, только
  коллайдер-триггер. В этом весь смысл: наступивший не должен видеть, куда идёт.
- **Терминалы целей (5 штук)** — ванильная консоль, перекрашенная в цвет фракции.
  Так и задумано: «спрайт обычного терминала». Пять слоёв ванильного `BaseComputer`
  завязаны на визуализатор, трогать их без нужды не стоит.
- **КПК гильдий (3 штуки)** — ванильный корпус, свои только доступ и содержимое.
- **Связки ключ-карт (2 штуки)** — ванильная картонная коробка. Колхозный метод и должен
  выглядеть колхозно.
- **Техномаги** — раса пользуется человеческими спрайтами тела. Тёмная кожа и белые глаза
  задаются цветом в профиле, отдельная графика не нужна.

---

# Порядок, в котором это имеет смысл делать

1. **Одежда фракций (18).** Её видно постоянно и на всех. Сейчас три гильдии техномагов
   различимы только мантиями, а комбинезоны под ними — ванильные учёный с инженером.
2. **Иконки действий (19).** Шесть пар заклинаний неразличимы на панели, это прямо мешает играть.
3. **Артефакты (11).** Их мало, они редкие, но каждый — событие раунда.
4. **Клейма и культовая атрибутика (10).** Три фетиша сейчас один и тот же спрайт.
5. **Постройки (6).**
6. **Документы (6).**
7. **Иконки должностей (32).** 8×8, различить 32 штуки почти невозможно — это последнее,
   что окупается.

# Как проверить, что RSI собрана правильно

- Папка кончается на `.rsi`, внутри `meta.json` и только `.png`. Лишних файлов быть не должно —
  загрузчик на них ругается.
- В `meta.json` обязательны `version` (1), `license`, `copyright`, `size`, `states`.
- Состояние с направлениями объявляется как `"directions": 4`.
- Имя состояния в `meta.json` должно точно совпадать с тем, что стоит в прототипе.
  Опечатка даёт розово-чёрный квадрат, а не ошибку сборки.
