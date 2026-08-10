# _Warlock — текстуры

Папка зарезервирована под собственные спрайты билда Warlock.

Сейчас все сущности билда используют **плейсхолдеры из ванильных наборов**:

| Сущность | Плейсхолдер |
|---|---|
| Иконки заклинаний | `Objects/Magic/magicactions.rsi` |
| Печать «Отзвук Мёртвого Бога» | `Structures/Magic/Cult/rune.rsi` (`cult5`) |
| Счётчик псионической энергии | `Interface/Alerts/essence_counter.rsi` |
| Кость Судьбы | `Objects/Fun/dice.rsi` (`d20_*`) |
| Оковы Логики | `Clothing/Neck/Misc/bling.rsi` |
| Свеча Мёртвого Бога | `Objects/Misc/candles.rsi` |
| Перчатка Тысячи Рук | `Clothing/Hands/Gloves/powerglove.rsi` |
| Урна Трёх Гильдий | `Objects/Specific/Chapel/chaplainurn.rsi` |
| Тело техномага | `Mobs/Species/Human/*` (техномаги — мутировавшие люди) |

Все плейсхолдеры подкрашены через `color` в прототипах, поэтому при замене на собственные
спрайты нужно не забыть убрать `color` из соответствующего компонента `Sprite`.
