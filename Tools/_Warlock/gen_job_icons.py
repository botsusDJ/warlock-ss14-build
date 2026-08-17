#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
_Warlock — генератор иконок должностей гильдии Варлок. 8x8.

Схема взята у ванили и разобрана по пикселям на Captain, Chaplain и Scientist:
четыре угловых пикселя прозрачны, по периметру рамка тёмным оттенком цвета
должности, внутри заливка средним, поверх глиф. У ванили глиф белый, у нас
чёрный: гильдия Варлок — это золото с чёрным, и белая молния на золоте
не читалась бы.

Ранг показан ДВУМЯ способами сразу, и это осознанно. На восьми пикселях одного
признака мало: глиф отличается парой точек, а пара точек на таком размере
теряется. Поэтому вместе с глифом меняется цвет рамки — от тусклой бронзы у
адепта до белой у Архимага-Епископа. Рамку видно боковым зрением в списке из
тридцати человек, детали глифа — уже при наведении.

Запуск из корня билда:
    python3 Tools/_Warlock/gen_job_icons.py
"""
import json
import os

from PIL import Image

OUT = 'Resources/Textures/_Warlock/Interface/Misc/warlock_job_icons.rsi'

FILL       = (0xe0, 0xb0, 0x2a)   # золото гильдии
FILL_HI    = (0xf4, 0xcc, 0x5e)   # подсветка верхней строки заливки
GLYPH      = (0x14, 0x11, 0x0a)   # чёрная молния
# Метки ранга светлые, а не тёмные. Тёмные сливались с молнией в одно пятно:
# на шести пикселях два тёмных знака рядом читаются как один кривой.
MARK       = (0xfd, 0xf4, 0xd2)

BORDERS = {
    'Adept':          (0x7a, 0x5c, 0x10),
    'Mage':           (0x8f, 0x6c, 0x12),
    'Ritualist':      (0xa8, 0x80, 0x16),
    'Priest':         (0xc6, 0x99, 0x1e),
    'HighPriest':     (0xe8, 0xc2, 0x54),
    'ArchmageBishop': (0xfa, 0xf0, 0xc8),
}

# Глиф рисуется в поле 6x6 внутри рамки.
# 'B' — молния, '.' — заливка, 'o' — метка ранга.
#
# Молния: диагональ сверху-справа вниз-влево с поперечной вспышкой посередине.
# Первая версия была шире и с двумя почти полными строками — на шести пикселях
# это читалось кляксой, а не молнией.
BOLT_THIN = [
    "...B..",
    "..B...",
    ".BBB..",
    "..B...",
    ".B....",
    "B.....",
]
BOLT = [
    "...BB.",
    "..BB..",
    ".BBBB.",
    "..BB..",
    ".BB...",
    "BB....",
]


def with_marks(art, marks):
    out = [list(r) for r in art]
    for (x, y, ch) in marks:
        if out[y][x] == '.':
            out[y][x] = ch
    return [''.join(r) for r in out]


BAR = [(x, 5, 'o') for x in range(3, 6)]        # черта сана в правом нижнем углу

ROLES = {
    'Adept':          BOLT_THIN,                                 # тонкая молния
    'Mage':           BOLT,                                      # полная молния
    'Ritualist':      with_marks(BOLT, [(0, 2, 'o'), (5, 2, 'o')]),   # знаки обрядового круга
    'Priest':         with_marks(BOLT, BAR),                     # черта сана
    'HighPriest':     with_marks(BOLT, BAR + [(0, 0, 'o'), (5, 0, 'o')]),
    # Зубцы митры внутри поля, а не на рамке: вылезая на рамку, они рвали её,
    # и иконка выглядела повреждённой, а не украшенной.
    'ArchmageBishop': with_marks(BOLT, BAR + [(0, 0, 'o'), (1, 0, 'o'),
                                              (4, 0, 'o'), (5, 0, 'o'), (5, 1, 'o')]),
}

ORDER = ['Adept', 'Mage', 'Ritualist', 'Priest', 'HighPriest', 'ArchmageBishop']


def icon(role):
    art = ROLES[role]
    border = BORDERS[role]
    im = Image.new('RGBA', (8, 8), (0, 0, 0, 0))
    px = im.load()

    # Рамка по периметру, четыре угла прозрачны — как у ванильных иконок.
    for i in range(8):
        for (x, y) in ((i, 0), (i, 7), (0, i), (7, i)):
            if (x, y) in ((0, 0), (7, 0), (0, 7), (7, 7)):
                continue
            px[x, y] = border + (255,)

    # Заливка с подсветкой верхней строки: без неё квадрат выглядит плоским.
    for y in range(1, 7):
        for x in range(1, 7):
            px[x, y] = (FILL_HI if y == 1 else FILL) + (255,)

    for j, row in enumerate(art):
        for i, ch in enumerate(row):
            if ch == 'B':
                px[1 + i, 1 + j] = GLYPH + (255,)
            elif ch == 'o':
                px[1 + i, 1 + j] = MARK + (255,)
    return im


def main():
    if not os.path.isdir('Resources/Prototypes/_Warlock'):
        print('Запускать из корня билда.')
        return 2
    os.makedirs(OUT, exist_ok=True)
    for role in ORDER:
        icon(role).save(os.path.join(OUT, role + '.png'))
    json.dump({
        'version': 1,
        'license': 'CC-BY-SA-3.0',
        'copyright': 'Нарисовано для билда Warlock',
        'size': {'x': 8, 'y': 8},
        'states': [{'name': r} for r in ORDER],
    }, open(os.path.join(OUT, 'meta.json'), 'w', encoding='utf-8'),
        ensure_ascii=False, indent=2)
    print('иконок записано:', len(ORDER), '->', OUT)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
