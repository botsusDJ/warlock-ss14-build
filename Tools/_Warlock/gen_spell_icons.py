#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
_Warlock — иконки заклинаний второго набора и спрайты трёх реликвий глав гильдий.

Иконки действий 32x32. Ванильный magicactions.rsi даёт всего десять состояний, и они
уже разобраны первым набором: двадцать новых заклинаний с ним стали бы неразличимы
в панели действий.

Схема одна на все двадцать. Тёмный круг, поверх него глиф, вокруг — кольцо цвета
раздела каталога:

    рядовые      серо-синий
    боевые       красный
    капелланские золотой
    командирские фиолетовый

Так раздел заклинания видно прямо в панели, не открывая книгу, а глиф отличает
заклинания внутри раздела.

Реликвии 32x32, по одной на гильдию, в цветах гильдии.

Запуск из корня билда:
    python3 Tools/_Warlock/gen_spell_icons.py
"""
import json
import math
import os

from PIL import Image, ImageDraw

ACTIONS = 'Resources/Textures/_Warlock/Interface/Actions/warlock_spells.rsi'
RELICS = 'Resources/Textures/_Warlock/Objects/Relics'

# Цвет кольца по разделу каталога.
RING = {
    'common':   (0x7f, 0x94, 0xb0),
    'combat':   (0xc4, 0x46, 0x3c),
    'chaplain': (0xe0, 0xb0, 0x2a),
    'command':  (0x9a, 0x5c, 0xd6),
}

DISC = (0x1c, 0x1e, 0x24)      # тёмная подложка под глифом
DISC_HI = (0x2a, 0x2e, 0x36)   # блик сверху, чтобы круг не выглядел дырой
GLYPH = (0xe6, 0xea, 0xf0)     # основной цвет глифа
DIM = (0x8e, 0x96, 0xa4)       # второстепенные детали глифа

S = 32
C = S / 2


def disc(ring):
    """Подложка: тёмный круг с бликом и кольцо цвета раздела."""
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([1, 1, S - 2, S - 2], fill=DISC + (255,), outline=ring + (255,), width=2)
    # Блик — дуга по верхней кромке. Без него круг читается провалом.
    d.arc([4, 4, S - 5, S - 5], start=200, end=340, fill=DISC_HI + (255,), width=2)
    return im, d


def ray(d, angle, r0, r1, color, width=2):
    a = math.radians(angle)
    d.line([C + math.cos(a) * r0, C - math.sin(a) * r0,
            C + math.cos(a) * r1, C - math.sin(a) * r1], fill=color + (255,), width=width)


# ==================================================================================================
# Глифы
#
# Каждый рисуется поверх подложки в поле примерно 18x18 в центре. Все — простые
# геометрические фигуры: на тридцати двух пикселях детальнее не читается.
# ==================================================================================================

def g_mending(d):        # молоток над наковальней
    d.rectangle([11, 19, 21, 22], fill=GLYPH + (255,))
    d.rectangle([9, 10, 23, 13], fill=GLYPH + (255,))
    d.rectangle([15, 12, 17, 19], fill=DIM + (255,))


def g_grip(d):           # сжатая ладонь
    d.rounded_rectangle([10, 12, 22, 22], radius=3, fill=GLYPH + (255,))
    for x in (12, 15, 18):
        d.line([x, 12, x, 8], fill=GLYPH + (255,), width=2)
    d.line([22, 15, 25, 12], fill=DIM + (255,), width=2)


def g_step(d):           # след и штрихи движения
    d.ellipse([13, 8, 20, 17], fill=GLYPH + (255,))
    d.ellipse([13, 18, 20, 23], fill=GLYPH + (255,))
    for y in (11, 15, 19):
        d.line([6, y, 10, y], fill=DIM + (255,), width=2)


def g_sight(d):          # глаз со зрачком
    d.ellipse([7, 11, 25, 21], outline=GLYPH + (255,), width=2)
    d.ellipse([13, 13, 19, 19], fill=GLYPH + (255,))
    d.ellipse([15, 15, 17, 17], fill=DISC + (255,))


def g_hearth(d):         # пламя, перечёркнутое
    d.polygon([(16, 8), (21, 16), (19, 22), (13, 22), (11, 16)], fill=DIM + (255,))
    d.line([8, 24, 24, 8], fill=GLYPH + (255,), width=3)


def g_volley(d):         # осколки во все стороны
    for a in range(0, 360, 45):
        ray(d, a, 6, 13, GLYPH)
    d.ellipse([14, 14, 18, 18], fill=DIM + (255,))


def g_cramp(d):          # согнутая ломаная
    d.line([9, 8, 15, 14], fill=GLYPH + (255,), width=3)
    d.line([15, 14, 10, 18], fill=GLYPH + (255,), width=3)
    d.line([10, 18, 22, 24], fill=GLYPH + (255,), width=3)


def g_debt(d):           # две капли, стрелка между ними
    d.polygon([(10, 9), (13, 15), (7, 15)], fill=GLYPH + (255,))
    d.polygon([(22, 17), (25, 23), (19, 23)], fill=DIM + (255,))
    d.line([12, 17, 20, 13], fill=GLYPH + (255,), width=2)


def g_shroud(d):         # щит из линий
    d.polygon([(16, 7), (24, 11), (24, 18), (16, 25), (8, 18), (8, 11)],
              outline=GLYPH + (255,), width=2)
    for y in (13, 17, 21):
        d.line([11, y, 21, y], fill=DIM + (255,), width=1)


def g_bind(d):           # кольцо с цепью вниз
    d.ellipse([11, 7, 21, 15], outline=GLYPH + (255,), width=2)
    for y in (16, 20):
        d.ellipse([13, y, 19, y + 5], outline=DIM + (255,), width=2)


def g_rites(d):          # чаша с лучом
    d.arc([9, 12, 23, 24], start=0, end=180, fill=GLYPH + (255,), width=3)
    d.line([16, 18, 16, 24], fill=GLYPH + (255,), width=2)
    ray(d, 90, 9, 14, DIM, 2)


def g_vow(d):            # губы, перечёркнутые
    d.arc([8, 12, 24, 22], start=0, end=180, fill=GLYPH + (255,), width=3)
    d.line([9, 8, 23, 24], fill=GLYPH + (255,), width=3)


def g_burden(d):         # две фигуры под общей чертой
    d.line([7, 11, 25, 11], fill=GLYPH + (255,), width=2)
    d.ellipse([9, 14, 14, 19], fill=GLYPH + (255,))
    d.ellipse([18, 14, 23, 19], fill=DIM + (255,))
    d.line([11, 19, 11, 24], fill=GLYPH + (255,), width=2)
    d.line([20, 19, 20, 24], fill=DIM + (255,), width=2)


def g_consecrate(d):     # купол над кругом
    d.arc([6, 8, 26, 28], start=180, end=360, fill=GLYPH + (255,), width=3)
    d.ellipse([13, 16, 19, 22], fill=DIM + (255,))


def g_flock(d):          # колокол
    d.polygon([(16, 7), (23, 20), (9, 20)], fill=GLYPH + (255,))
    d.rectangle([8, 20, 24, 22], fill=GLYPH + (255,))
    d.ellipse([15, 23, 18, 26], fill=DIM + (255,))


def g_seizure(d):        # ладонь и падающий предмет
    d.rounded_rectangle([8, 9, 18, 15], radius=2, fill=GLYPH + (255,))
    d.line([20, 12, 20, 20], fill=DIM + (255,), width=2)
    d.polygon([(17, 19), (23, 19), (20, 24)], fill=DIM + (255,))


def g_chain(d):          # три звена в ряд
    for x in (7, 13, 19):
        d.ellipse([x, 13, x + 7, 20], outline=GLYPH + (255,), width=2)


def g_excommunicate(d):  # разорванное кольцо
    d.arc([8, 8, 24, 24], start=30, end=330, fill=GLYPH + (255,), width=3)
    d.line([22, 8, 26, 12], fill=DIM + (255,), width=2)
    d.line([26, 8, 22, 12], fill=DIM + (255,), width=2)


def g_census(d):         # столбцы разной высоты
    for i, h in enumerate((8, 14, 11, 17)):
        x = 8 + i * 5
        d.rectangle([x, 24 - h, x + 3, 24], fill=(GLYPH if i % 2 == 0 else DIM) + (255,))


def g_search(d):         # лупа
    d.ellipse([8, 8, 21, 21], outline=GLYPH + (255,), width=3)
    d.line([20, 20, 25, 25], fill=GLYPH + (255,), width=3)


def g_seers_eye(d):      # реликвия Фактоса: глаз в огранке
    d.polygon([(16, 5), (25, 16), (16, 27), (7, 16)], outline=GLYPH + (255,), width=2)
    d.ellipse([11, 12, 21, 20], fill=(0x8b, 0x3f, 0xd6, 255))
    d.ellipse([14, 14, 18, 18], fill=(0x14, 0x0c, 0x1e, 255))


def g_machine_heart(d):  # реликвия Техноса: шестерня с сердцевиной
    for a in range(0, 360, 45):
        ray(d, a, 8, 13, GLYPH, 3)
    d.ellipse([9, 9, 23, 23], outline=GLYPH + (255,), width=2)
    d.ellipse([13, 13, 19, 19], fill=(0xdf, 0xe8, 0xff, 255))


ICONS = [
    # (состояние, раздел, глиф)
    ('MendingHands',   'common',   g_mending),
    ('SteadyGrip',     'common',   g_grip),
    ('LightStep',      'common',   g_step),
    ('SecondSight',    'common',   g_sight),
    ('ColdHearth',     'common',   g_hearth),

    ('SplinterVolley', 'combat',   g_volley),
    ('IronCramp',      'combat',   g_cramp),
    ('BloodDebt',      'combat',   g_debt),
    ('StaticShroud',   'combat',   g_shroud),
    ('Gravebind',      'combat',   g_bind),

    ('LastRites',      'chaplain', g_rites),
    ('VowOfSilence',   'chaplain', g_vow),
    ('BurdenShare',    'chaplain', g_burden),
    ('Consecrate',     'chaplain', g_consecrate),
    ('CallTheFlock',   'chaplain', g_flock),

    ('WritOfSeizure',  'command',  g_seizure),
    ('ChainOfCommand', 'command',  g_chain),
    ('Excommunicate',  'command',  g_excommunicate),
    ('Census',         'command',  g_census),
    ('RightOfSearch',  'command',  g_search),

    # Действия реликвий. Раздела у них нет, но кольцо всё равно нужно —
    # берём цвет гильдии-владельца.
    ('SeersEye',       'command',  g_seers_eye),
    ('MachineHeart',   'common',   g_machine_heart),
]


# ==================================================================================================
# Реликвии как предметы
#
# Отдельно от иконок действий: у предмета на полу другая задача — его надо узнать
# среди хлама, поэтому силуэт заметный, а фона-круга нет.
# ==================================================================================================

def relic_censer():
    """Кадило Касса: чаша на цепи, внутри тлеет."""
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    gold = (0xe0, 0xb0, 0x2a, 255)
    dark = (0x6b, 0x50, 0x12, 255)
    ember = (0xff, 0xe6, 0x9a, 255)

    # цепь
    for y in range(3, 12, 3):
        d.ellipse([15, y, 19, y + 3], outline=dark, width=1)
    # чаша
    d.polygon([(9, 14), (23, 14), (20, 26), (12, 26)], fill=gold, outline=dark)
    d.rectangle([8, 12, 24, 15], fill=gold, outline=dark)
    # тление внутри
    d.ellipse([13, 16, 19, 21], fill=ember)
    d.ellipse([15, 17, 17, 19], fill=(0xff, 0xff, 0xe0, 255))
    return im


def relic_eye():
    """Око Фактоса: гранёный камень с фиолетовой радужкой."""
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    frame = (0x6a, 0x6e, 0x7a, 255)
    iris = (0x8b, 0x3f, 0xd6, 255)
    d.polygon([(16, 3), (27, 16), (16, 29), (5, 16)], fill=(0x2c, 0x2e, 0x33, 255), outline=frame)
    d.polygon([(16, 8), (23, 16), (16, 24), (9, 16)], fill=iris)
    d.ellipse([13, 13, 19, 19], fill=(0x14, 0x0c, 0x1e, 255))
    d.ellipse([14, 12, 16, 14], fill=(0xdc, 0xdc, 0xe4, 255))
    return im


def relic_heart():
    """Сердце Механтехиона: шестерня, внутри которой бьётся свет."""
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    steel = (0xd6, 0xdb, 0xe2, 255)
    dark = (0x6e, 0x74, 0x7e, 255)
    core = (0xdf, 0xe8, 0xff, 255)

    for a in range(0, 360, 45):
        r = math.radians(a)
        x, y = C + math.cos(r) * 12, C - math.sin(r) * 12
        d.rectangle([x - 3, y - 3, x + 3, y + 3], fill=steel, outline=dark)
    d.ellipse([5, 5, 27, 27], fill=steel, outline=dark, width=2)
    d.ellipse([11, 11, 21, 21], fill=(0x3a, 0x3f, 0x48, 255))
    d.ellipse([13, 13, 19, 19], fill=core)
    return im


RELICS_ART = [
    ('censer_of_kass', relic_censer),
    ('seers_eye', relic_eye),
    ('machine_heart', relic_heart),
]


def meta(states, size=32):
    return {
        'version': 1,
        'license': 'CC-BY-SA-3.0',
        'copyright': 'Нарисовано для билда Warlock',
        'size': {'x': size, 'y': size},
        'states': [{'name': s} for s in states],
    }


def main():
    if not os.path.isdir('Resources/Prototypes/_Warlock'):
        print('Запускать из корня билда.')
        return 2

    os.makedirs(ACTIONS, exist_ok=True)
    for name, section, glyph in ICONS:
        im, d = disc(RING[section])
        glyph(d)
        im.save(os.path.join(ACTIONS, name + '.png'))
    json.dump(meta([n for n, _, _ in ICONS]),
              open(os.path.join(ACTIONS, 'meta.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=2)
    print('иконок заклинаний:', len(ICONS))

    for name, draw in RELICS_ART:
        out = os.path.join(RELICS, name + '.rsi')
        os.makedirs(out, exist_ok=True)
        draw().save(os.path.join(out, 'icon.png'))
        json.dump(meta(['icon']),
                  open(os.path.join(out, 'meta.json'), 'w', encoding='utf-8'),
                  ensure_ascii=False, indent=2)
    print('реликвий:', len(RELICS_ART))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
