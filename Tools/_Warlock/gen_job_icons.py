#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
_Warlock — генератор иконок должностей трёх гильдий. 8x8.

Схема взята у ванили и разобрана по пикселям на Captain, Chaplain и Scientist:
четыре угловых пикселя прозрачны, по периметру рамка тёмным оттенком цвета
гильдии, внутри заливка, поверх глиф.

Ранг показан ДВУМЯ способами сразу, и это осознанно. На восьми пикселях одного
признака мало: глиф отличается парой точек, а пара точек на таком размере
теряется. Поэтому вместе с глифом меняется цвет рамки — от тусклой у младших
до почти белой у главы гильдии. Рамку видно боковым зрением в списке из тридцати
человек, детали глифа — уже при наведении.

Гильдии:
    Варлок — чёрная молния на золоте
    Фактос — глаз с фиолетовой радужкой на тёмно-сером
    Технос — белая шестерня на сером

Запуск из корня билда:
    python3 Tools/_Warlock/gen_job_icons.py
"""
import json
import os

from PIL import Image

BASE = 'Resources/Textures/_Warlock/Interface/Misc'


def with_marks(art, marks):
    """Дорисовать метки ранга поверх глифа, не затирая сам глиф."""
    out = [list(r) for r in art]
    for (x, y, ch) in marks:
        if out[y][x] == '.':
            out[y][x] = ch
    return [''.join(r) for r in out]


# ==================================================================================================
# Варлок: чёрная молния на золоте
#
# Глиф чёрный, а не белый как у ванили: гильдия — это золото с чёрным,
# и белая молния на золоте не читалась бы.
# ==================================================================================================

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

BAR = [(x, 5, 'o') for x in range(3, 6)]        # черта сана в правом нижнем углу

WARLOCK = {
    'dir': 'warlock_job_icons.rsi',
    'fill': (0xe0, 0xb0, 0x2a),      # золото гильдии
    'fill_hi': (0xf4, 0xcc, 0x5e),   # подсветка верхней строки заливки
    'glyph': (0x14, 0x11, 0x0a),     # чёрная молния
    # Метки ранга светлые, а не тёмные. Тёмные сливались с молнией в одно пятно:
    # на шести пикселях два тёмных знака рядом читаются как один кривой.
    'mark': (0xfd, 0xf4, 0xd2),
    'accent': None,
    'pupil': None,
    'order': ['Adept', 'Mage', 'Ritualist', 'Priest', 'HighPriest', 'ArchmageBishop'],
    'borders': {
        'Adept':          (0x7a, 0x5c, 0x10),
        'Mage':           (0x8f, 0x6c, 0x12),
        'Ritualist':      (0xa8, 0x80, 0x16),
        'Priest':         (0xc6, 0x99, 0x1e),
        'HighPriest':     (0xe8, 0xc2, 0x54),
        'ArchmageBishop': (0xfa, 0xf0, 0xc8),
    },
    'roles': {
        'Adept':          BOLT_THIN,                                      # тонкая молния
        'Mage':           BOLT,                                           # полная молния
        'Ritualist':      with_marks(BOLT, [(0, 2, 'o'), (5, 2, 'o')]),   # знаки обрядового круга
        'Priest':         with_marks(BOLT, BAR),                          # черта сана
        'HighPriest':     with_marks(BOLT, BAR + [(0, 0, 'o'), (5, 0, 'o')]),
        # Зубцы митры внутри поля, а не на рамке: вылезая на рамку, они рвали её,
        # и иконка выглядела повреждённой, а не украшенной.
        'ArchmageBishop': with_marks(BOLT, BAR + [(0, 0, 'o'), (1, 0, 'o'),
                                                  (4, 0, 'o'), (5, 0, 'o'), (5, 1, 'o')]),
    },
}

# ==================================================================================================
# Фактос: глаз с фиолетовой радужкой на тёмно-сером
#
# Глаз миндалевидный, шире чем выше: круглый на шести пикселях читается шаром,
# а не глазом. Радужка отдельным цветом от белка — иначе весь глиф сливается
# в светлое пятно и от шестерни Техноса его в списке не отличить.
#
# 'B' — белок, 'A' — радужка (акцент), 'o' — метка ранга.
# ==================================================================================================

EYE_THIN = [
    "......",
    "..BB..",
    ".BAAB.",
    ".BAAB.",
    "..BB..",
    "......",
]
EYE = [
    "..BB..",
    ".BAAB.",
    "BAPPAB",
    "BAPPAB",
    ".BAAB.",
    "..BB..",
]
# Щелевидный зрачок: у старших Фактос глаз перестаёт быть человеческим.
EYE_SLIT = [
    "..BB..",
    ".BPPB.",
    "BAPPAB",
    "BAPPAB",
    ".BPPB.",
    "..BB..",
]
# Блик в зрачке — только у главы гильдии. Один пиксель, но глаз оживает.
EYE_GLINT = [
    "..BB..",
    ".BAAB.",
    "BAPBAB",
    "BAPPAB",
    ".BAAB.",
    "..BB..",
]

RAYS_TOP = [(0, 0, 'o'), (5, 0, 'o')]                          # лучи по верхним углам
RAYS = RAYS_TOP + [(0, 5, 'o'), (5, 5, 'o')]                   # и по нижним
HALO = RAYS + [(0, 1, 'o'), (5, 1, 'o'), (0, 4, 'o'), (5, 4, 'o')]   # полный венец

FACTOS = {
    'dir': 'factos_job_icons.rsi',
    'fill': (0x2c, 0x2e, 0x33),      # тёмно-серый фон гильдии
    'fill_hi': (0x3a, 0x3d, 0x44),
    'glyph': (0xdc, 0xdc, 0xe4),     # белок
    'accent': (0x8b, 0x3f, 0xd6),    # фиолетовая радужка
    'pupil': (0x14, 0x0c, 0x1e),     # зрачок
    'mark': (0xc9, 0xa8, 0xf0),
    'order': ['Adept', 'Mage', 'Enchanter', 'Sorcerer', 'GreatSeeker', 'Artarchmage'],
    'borders': {
        'Adept':       (0x4a, 0x3e, 0x5c),
        'Mage':        (0x5c, 0x46, 0x7a),
        'Enchanter':   (0x72, 0x52, 0x9c),
        'Sorcerer':    (0x8e, 0x62, 0xc2),
        'GreatSeeker': (0xb0, 0x86, 0xe0),
        'Artarchmage': (0xdc, 0xcc, 0xf6),
    },
    'roles': {
        'Adept':       EYE_THIN,
        'Mage':        EYE,
        'Enchanter':   with_marks(EYE, RAYS_TOP),
        'Sorcerer':    with_marks(EYE, RAYS),
        'GreatSeeker': with_marks(EYE_SLIT, RAYS),
        'Artarchmage': with_marks(EYE_GLINT, HALO),
    },
}

# ==================================================================================================
# Технос: белая шестерня на сером
#
# Шестерня — сплошное кольцо с квадратным отверстием и зубцами сверху и снизу.
# Зубцы по всем четырём сторонам пробовались первыми: на шести пикселях кольцо
# от них становилось толщиной в пиксель и разваливалось.
# ==================================================================================================

GEAR_THIN = [
    "..BB..",
    ".BBBB.",
    ".B..B.",
    ".B..B.",
    ".BBBB.",
    "..BB..",
]
GEAR = [
    "B.BB.B",
    ".BBBB.",
    "BB..BB",
    "BB..BB",
    ".BBBB.",
    "B.BB.B",
]

AXLE_HALF = [(2, 2, 'o'), (3, 3, 'o')]          # ось намечена по диагонали
AXLE = [(2, 2, 'o'), (3, 2, 'o'), (2, 3, 'o'), (3, 3, 'o')]   # полная ось
PINS_TOP = [(1, 0, 'o'), (4, 0, 'o')]           # штифты в проёмах между зубцами
PINS_BOT = [(1, 5, 'o'), (4, 5, 'o')]

TECHNOS = {
    'dir': 'technos_job_icons.rsi',
    'fill': (0x6e, 0x72, 0x76),      # серый фон гильдии
    'fill_hi': (0x82, 0x86, 0x8b),
    'glyph': (0xf2, 0xf4, 0xf6),     # белая шестерня
    'accent': None,
    'pupil': None,
    # Метки тёмные: на белой шестерне светлые не видно вовсе.
    'mark': (0x2a, 0x2d, 0x30),
    'order': ['Adept', 'Mage', 'Technomancer', 'Technosorcerer',
              'SupremeTechnocrat', 'Archtechnomage'],
    'borders': {
        'Adept':             (0x35, 0x38, 0x3b),
        'Mage':              (0x46, 0x4a, 0x4e),
        'Technomancer':      (0x5c, 0x61, 0x66),
        'Technosorcerer':    (0x7d, 0x84, 0x8a),
        'SupremeTechnocrat': (0xa8, 0xb0, 0xb6),
        'Archtechnomage':    (0xe4, 0xe9, 0xec),
    },
    'roles': {
        'Adept':             GEAR_THIN,
        'Mage':              GEAR,
        'Technomancer':      with_marks(GEAR, AXLE_HALF),
        'Technosorcerer':    with_marks(GEAR, AXLE),
        'SupremeTechnocrat': with_marks(GEAR, AXLE + PINS_TOP),
        'Archtechnomage':    with_marks(GEAR, AXLE + PINS_TOP + PINS_BOT),
    },
}

GUILDS = [WARLOCK, FACTOS, TECHNOS]


def icon(guild, role):
    art = guild['roles'][role]
    border = guild['borders'][role]
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
            px[x, y] = (guild['fill_hi'] if y == 1 else guild['fill']) + (255,)

    colors = {
        'B': guild['glyph'],
        'o': guild['mark'],
        'A': guild.get('accent') or guild['glyph'],
        'P': guild.get('pupil') or guild.get('accent') or guild['glyph'],
    }
    for j, row in enumerate(art):
        for i, ch in enumerate(row):
            if ch in colors:
                px[1 + i, 1 + j] = colors[ch] + (255,)
    return im


def main():
    if not os.path.isdir('Resources/Prototypes/_Warlock'):
        print('Запускать из корня билда.')
        return 2

    total = 0
    for guild in GUILDS:
        out = os.path.join(BASE, guild['dir'])
        os.makedirs(out, exist_ok=True)
        for role in guild['order']:
            icon(guild, role).save(os.path.join(out, role + '.png'))
        json.dump({
            'version': 1,
            'license': 'CC-BY-SA-3.0',
            'copyright': 'Нарисовано для билда Warlock',
            'size': {'x': 8, 'y': 8},
            'states': [{'name': r} for r in guild['order']],
        }, open(os.path.join(out, 'meta.json'), 'w', encoding='utf-8'),
            ensure_ascii=False, indent=2)
        print('%-24s иконок: %d' % (guild['dir'], len(guild['order'])))
        total += len(guild['order'])

    print('всего:', total)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
