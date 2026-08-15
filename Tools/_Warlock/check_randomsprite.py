#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
_Warlock — проверка RandomSprite.

Зачем нужна отдельная проверка именно под этот компонент.

В RandomSprite поле available имеет тип
    List<Dictionary<string, Dictionary<string, string?>>>
то есть для сериализатора это просто строки. Ни YAMLLinter, ни валидатор ссылок
на прототипы сюда не смотрят: с их точки зрения "#e8c46a" — совершенно законная
строка. А RandomSpriteSystem на MapInit берёт её и суёт в

    ProtoMan.Index<ColorPalettePrototype>(selectedState.Value)

и сервер падает с UnknownPrototypeException прямо при инициализации карты.

Падение отложенное: прототип грузится молча, ошибка выстреливает только когда
на карте появится сущность с этим компонентом. Поэтому проверять надо статически.

Правило: значение — либо пустая строка, либо "Inherit", либо id существующей
палитры (- type: palette).

Запуск из корня билда:
    python3 Tools/_Warlock/check_randomsprite.py
"""
import os
import re
import sys

ROOT = 'Resources/Prototypes'


def collect_palettes():
    """Все id прототипов - type: palette во всей сборке."""
    palettes = set()
    for dirpath, _, filenames in os.walk(ROOT):
        for name in filenames:
            if not name.endswith('.yml'):
                continue
            path = os.path.join(dirpath, name)
            try:
                text = open(path, encoding='utf-8').read()
            except OSError:
                continue
            if '- type: palette' not in text:
                continue
            for block in text.split('- type: palette')[1:]:
                match = re.search(r'^\s+id:\s*(\S+)\s*$', block, re.M)
                if match:
                    palettes.add(match.group(1))
    return palettes


def collect_usages():
    """Все значения внутри блоков available у RandomSprite.

    Разбор построчный, а не через YAML: файлы прототипов содержат теги !type:,
    на которых обычный safe_load спотыкается, а тащить сюда кастомный лоадер
    ради одной проверки не стоит.
    """
    usages = []
    for dirpath, _, filenames in os.walk(ROOT):
        for name in filenames:
            if not name.endswith('.yml'):
                continue
            path = os.path.join(dirpath, name)
            try:
                lines = open(path, encoding='utf-8').read().split('\n')
            except OSError:
                continue
            if 'RandomSprite' not in '\n'.join(lines):
                continue

            inside = False
            base_indent = 0
            for number, line in enumerate(lines, 1):
                if re.match(r'^\s*-?\s*type:\s*RandomSprite\s*$', line):
                    inside = True
                    base_indent = len(line) - len(line.lstrip())
                    continue
                if not inside:
                    continue
                if not line.strip():
                    continue
                indent = len(line) - len(line.lstrip())
                # следующий компонент — блок кончился
                if re.match(r'^\s*-\s*type:\s*\w+', line) and indent <= base_indent:
                    inside = False
                    continue
                if indent <= base_indent:
                    inside = False
                    continue
                match = re.match(r'^\s*([\w.\-]+):\s*(\S.*?)\s*$', line)
                if not match:
                    continue
                value = match.group(2).strip().strip('"\'')
                # служебные поля самого компонента, не пары «состояние: палитра»
                if match.group(1) in ('available', 'getAllGroups', 'selected'):
                    continue
                usages.append((path, number, match.group(1), value))
    return usages


def main():
    if not os.path.isdir(ROOT):
        print('Запускать из корня билда: не вижу', ROOT)
        return 2

    palettes = collect_palettes()
    usages = collect_usages()

    bad = []
    for path, number, state, value in usages:
        if value in ('', 'Inherit', '{}', 'null'):
            continue
        if value.startswith('#'):
            bad.append((path, number, state, value, 'цвет вместо id палитры'))
        elif value not in palettes:
            bad.append((path, number, state, value, 'палитры с таким id нет'))

    print('палитр найдено:', len(palettes))
    print('пар «состояние: палитра» проверено:', len(usages))

    if not bad:
        print('\nOK — все значения ссылаются на существующие палитры.')
        return 0

    print('\nОШИБКИ (сервер упадёт на инициализации карты):')
    for path, number, state, value, why in bad:
        print('  %s:%d  %s: %s  — %s' % (path, number, state, value, why))
    return 1


if __name__ == '__main__':
    sys.exit(main())
