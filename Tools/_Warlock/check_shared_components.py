#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
_Warlock — проверка добавления сетевых компонентов из общего кода.

Зачем нужна.

Сетевой компонент на сущность вешает только сервер: клиент получает его
состоянием. Если общая система вызывает EnsureComp или AddComp у сетевого
компонента, то на клиенте этот вызов случится посреди отката предсказанных
сущностей — движок в этот момент перебирает список компонентов сущности.
Коллекция изменится во время перебора, и клиент упадёт:

    System.InvalidOperationException: Collection was modified;
    enumeration operation may not execute.
        at Robust.Client.GameStates.ClientGameStateManager.ResetPredictedEntities()

Падение не воспроизводится ни компиляцией, ни запуском сервера — только живым
клиентом в момент, когда сработает обработчик. Поэтому проверка статическая.

Правило: в Content.Shared/_Warlock вызовы EnsureComp/AddComp/RemComp сетевых
компонентов допустимы только под явной проверкой стороны (_net.IsServer).
Компонент считается сетевым, если помечен NetworkedComponent.

Запуск из корня билда:
    python3 Tools/_Warlock/check_shared_components.py
"""
import os
import re
import sys

SHARED = 'Content.Shared/_Warlock'
ROOTS = ('Content.Shared', 'Content.Server', 'Content.Client')


def networked_components():
    """Имена компонентов, помеченных NetworkedComponent, по всей сборке."""
    found = set()
    for root in ROOTS:
        for dirpath, _, filenames in os.walk(root):
            for name in filenames:
                if not name.endswith('.cs'):
                    continue
                try:
                    text = open(os.path.join(dirpath, name), encoding='utf-8').read()
                except OSError:
                    continue
                for match in re.finditer(
                        r'\[[^\]]*NetworkedComponent[^\]]*\]\s*(?:\[[^\]]*\]\s*)*'
                        r'public\s+(?:sealed\s+|abstract\s+|partial\s+)*class\s+(\w+)',
                        text):
                    found.add(match.group(1))
    return found


def split_methods(text):
    """Грубо разрезать файл на методы: имя -> тело.

    Полный разбор C# здесь не нужен — достаточно понимать, в каком методе
    находится вызов, чтобы поискать рядом проверку стороны.
    """
    out = []
    for match in re.finditer(r'\n\s*(?:public|private|protected|internal)[^\n;=]*?'
                             r'\b(\w+)\s*\([^)]*\)\s*\n?\s*\{', text):
        start = match.end()
        depth = 1
        i = start
        while i < len(text) and depth > 0:
            if text[i] == '{':
                depth += 1
            elif text[i] == '}':
                depth -= 1
            i += 1
        out.append((match.group(1), text[start:i]))
    return out


def main():
    if not os.path.isdir(SHARED):
        print('Запускать из корня билда: не вижу', SHARED)
        return 2

    net = networked_components()
    bad = []

    for dirpath, _, filenames in os.walk(SHARED):
        for name in filenames:
            if not name.endswith('.cs'):
                continue
            path = os.path.join(dirpath, name)
            text = open(path, encoding='utf-8').read()
            text = re.sub(r'//.*', '', text)
            text = re.sub(r'/\*.*?\*/', '', text, flags=re.S)

            for method, body in split_methods(text):
                # Проверка стороны где-то в этом же методе считается достаточной.
                guarded = 'IsServer' in body or 'IsClient' in body
                if guarded:
                    continue
                for call in re.finditer(r'\b(EnsureComp|AddComp|RemComp)<\s*(\w+)\s*>', body):
                    comp = call.group(2)
                    if comp in net:
                        bad.append((os.path.basename(path), method,
                                    f'{call.group(1)}<{comp}>'))

    print('сетевых компонентов в сборке:', len(net))
    if not bad:
        print('OK — в общем коде нет незащищённых правок сетевых компонентов.')
        return 0

    print('\nПРАВКА СЕТЕВОГО КОМПОНЕНТА БЕЗ ПРОВЕРКИ СТОРОНЫ:')
    for f, method, call in bad:
        print(f'  {f}: {method}() -> {call}')
    print('\nОберните вызов в проверку _net.IsServer либо перенесите в серверную систему.')
    return 1


if __name__ == '__main__':
    sys.exit(main())
