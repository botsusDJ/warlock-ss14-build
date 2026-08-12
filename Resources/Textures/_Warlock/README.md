# _Warlock — текстуры

## Свои спрайты

```
Clothing/OuterClothing/Robes/
  factos_robe.rsi         мантия гильдии Фактос
  factos_lord_robe.rsi    мантия лорда Фактос
  technos_robe.rsi        мантия гильдии Технос
  technos_lord_robe.rsi   мантия лорда Технос
  warlock_robe.rsi        мантия гильдии Варлок
  warlock_lord_robe.rsi   мантия лорда Варлок
```

Каждая RSI: размер кадра 32×32, четыре состояния — `icon` (1 кадр),
`equipped-OUTERCLOTHING`, `inhand-left`, `inhand-right` (по 4 направления).

Листы на 4 направления лежат сеткой 2×2 (64×64). Это допустимо: загрузчик считает
столбцы как `ширина / size.x` и читает кадры построчно, так что 2×2 и 4×1 равнозначны.
Порядок кадров обязателен: **юг, север, восток, запад**.

## Как добавлять новые

1. Папка обязана заканчиваться на `.rsi` — иначе движок её не увидит.
2. Внутри `meta.json` с `version`, `license`, `copyright`, `size` и списком `states`.
3. Имя PNG = имя состояния. Лишних файлов в папке быть не должно, движок на них ругается.
4. В прототипе путь пишется от `Textures`, то есть с `_Warlock/`:
   `sprite: _Warlock/Clothing/OuterClothing/Robes/factos_robe.rsi`
5. Для одежды путь нужен и в `Sprite`, и в `Clothing` — иначе надетый вид не подхватится.
   Когда спрайт свой, `clothingVisuals` и `color` не нужны: они были костылём для плейсхолдеров.

## Ещё на плейсхолдерах

| Сущность | Плейсхолдер |
|---|---|
| Иконки заклинаний и действий | `Objects/Magic/magicactions.rsi` |
| Печати заклинаний | `Structures/Magic/Cult/rune.rsi` |
| Счётчик псионической энергии | `Interface/Alerts/essence_counter.rsi` |
| Иконки ролей | `Interface/Misc/job_icons.rsi` |
| Кость Судьбы | `Objects/Fun/dice.rsi` |
| Оковы Логики, символы богов | `Clothing/Neck/Misc/bling.rsi`, `Objects/Specific/Chapel/ratvartablet.rsi` |
| Свеча Мёртвого Бога | `Objects/Misc/candles.rsi` |
| Перчатка Тысячи Рук | `Clothing/Hands/Gloves/powerglove.rsi` |
| Урна Трёх Гильдий | `Objects/Specific/Chapel/chaplainurn.rsi` |
| Комбинезоны и головные уборы гильдий | ванильные жгуты и капюшоны |
| Снаряжение Братства и Унатхов | ванильные броня, шлемы, кость |
| Алтари и обелиски | `Structures/Furniture/Altars/Gods/` |
| Терминалы целей | `Structures/Machines/computers.rsi` |
| Клейма | `Objects/Tools/welder.rsi` |
| Тело техномага | `Mobs/Species/Human/*` (техномаги — мутировавшие люди) |

Плейсхолдеры подкрашены через `color` в прототипах. При замене на свои спрайты
`color` и `clothingVisuals` нужно убирать — как это уже сделано для мантий.
