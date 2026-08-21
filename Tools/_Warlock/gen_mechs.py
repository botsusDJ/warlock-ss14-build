#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
_Warlock — генератор прототипов мехов Братства Стали.

Четыре рамы, сорок узлов, сорок рецептов фабрикатора и четыре сборочные рамы.
Руками это писать нельзя: девять десятых текста у них одинаковые, и любая правка
баланса означала бы сорок одинаковых правок с гарантированной опечаткой в одной.
Здесь вся разница между машинами лежит в одной таблице MECHS сверху.

Спрайты — перекрашенная ваниль, как и вся броня Братства: рама берёт состояние
из mecha.rsi, узлы — из ripley_construction.rsi. Это плейсхолдеры, и помечены
как плейсхолдеры.

Запуск из корня билда:
    python3 Tools/_Warlock/gen_mechs.py
"""
import os

OUT_MECHS = 'Resources/Prototypes/_Warlock/Entities/Mechs/mechs.yml'
OUT_PARTS = 'Resources/Prototypes/_Warlock/Entities/Mechs/parts.yml'
OUT_LATHE = 'Resources/Prototypes/_Warlock/Recipes/mech_parts.yml'
OUT_TOOLS = 'Resources/Prototypes/_Warlock/Entities/Mechs/service.yml'
OUT_ACTS = 'Resources/Prototypes/_Warlock/Actions/mechs.yml'

# ==================================================================================================
# Узлы
#
# Нога намеренно разобрана на три части. Основание переживает раму, коленный узел
# ходит в ремонт раз в смену, а ступню в песке стирает быстрее всего — и это три
# разные проблемы снабжения вместо одной кнопки «починить».
# ==================================================================================================

# (гнездо, имя, спрайт-состояние, вес для хода, скорость грязи, скорость оплавления)
BIPED = [
    ('cockpit',  'кабина',              'ripley_harness', 0.4, 0.4, 1.2),
    ('reactor',  'реакторный блок',     'ripley_chassis', 0.6, 0.3, 1.6),
    ('hip_l',    'левое основание ноги','ripley_l_leg',   1.0, 0.7, 0.8),
    ('hip_r',    'правое основание ноги','ripley_r_leg',  1.0, 0.7, 0.8),
    ('knee_l',   'левый коленный узел', 'ripley_l_leg',   1.0, 1.1, 1.0),
    ('knee_r',   'правый коленный узел','ripley_r_leg',   1.0, 1.1, 1.0),
    ('foot_l',   'левая ступня',        'ripley_l_leg',   1.0, 1.8, 0.9),
    ('foot_r',   'правая ступня',       'ripley_r_leg',   1.0, 1.8, 0.9),
    ('mount_l',  'левый пилон',         'ripley_l_arm',   0.2, 0.5, 1.3),
    ('mount_r',  'правый пилон',        'ripley_r_arm',   0.2, 0.5, 1.3),
]

# Механизатор ходит на четырёх. Ноги у него проще и дешевле, зато их вчетверо больше,
# и потеря одной не обездвиживает — он просто заваливается на бок и ползёт.
SPIDER = [
    ('cockpit',  'кабина',                'ripley_harness', 0.4, 0.4, 1.2),
    ('reactor',  'реакторный блок',       'ripley_chassis', 0.6, 0.3, 1.6),
    ('leg_a',    'передняя левая лапа',   'ripley_l_leg',   0.8, 1.6, 0.9),
    ('leg_b',    'передняя правая лапа',  'ripley_r_leg',   0.8, 1.6, 0.9),
    ('leg_c',    'задняя левая лапа',     'ripley_l_leg',   0.8, 1.6, 0.9),
    ('leg_d',    'задняя правая лапа',    'ripley_r_leg',   0.8, 1.6, 0.9),
    ('claw_l',   'левый бур-захват',      'ripley_l_arm',   0.3, 1.2, 1.1),
    ('claw_r',   'правый бур-захват',     'ripley_r_arm',   0.3, 1.2, 1.1),
    ('mount_l',  'левый пилон',           'ripley_l_arm',   0.2, 0.5, 1.3),
    ('mount_r',  'правый пилон',          'ripley_r_arm',   0.2, 0.5, 1.3),
]

# ==================================================================================================
# Рамы
#
# Трибун и Мекантек — самые тяжёлые: Мекантек штурмовой, Трибун командный. Легионер
# составляет основную массу Братства, Механизатор в бою почти бесполезен, зато таскает.
# ==================================================================================================

MECHS = [
    dict(
        id='Mekantek', name='Мекантек', cls='Mechantek', state='marauder',
        color='#6b6f76', parts=BIPED, integrity=420, equipment=2,
        lube=140, turn=0.78, turn_angle=70, speed=2.1, sprint=2.6,
        steel=2400, glass=900, plastic=400,
        desc=('Штурмовая рама Братства, сваренная из трёх чужих. Ходит тяжело, '
              'разворачивается ещё тяжелее, но то, что оказалось перед ней, '
              'обычно уже не имеет значения.'),
    ),
    dict(
        id='Tribune', name='Трибун', cls='Tribune', state='durand',
        color='#7d7266', parts=BIPED, integrity=380, equipment=2,
        lube=130, turn=0.68, turn_angle=75, speed=2.3, sprint=2.9,
        steel=2200, glass=1100, plastic=350,
        desc=('Командная рама. Выше остальных на голову и видна с другого конца поля — '
              'что и есть её задача, и её же главная беда.'),
    ),
    dict(
        id='Legionary', name='Легионер', cls='Line', state='hamtr',
        color='#5f6b74', parts=BIPED, integrity=260, equipment=2,
        lube=100, turn=0.55, turn_angle=80, speed=2.6, sprint=3.2,
        steel=1400, glass=600, plastic=200,
        desc=('Линейная рама Братства. Проще всех и чинится в поле — потому их '
              'и больше всех.'),
    ),
    dict(
        id='Mekhanizator', name='Механизатор', cls='Worker', state='clarke',
        color='#7a6a3f', parts=SPIDER, integrity=200, equipment=2,
        lube=90, turn=0.32, turn_angle=100, speed=2.9, sprint=3.4,
        steel=1100, glass=500, plastic=250,
        desc=('Рабочая рама на четырёх лапах. Лезет туда, куда двуногая не встанет, '
              'и разворачивается почти как человек. В драке её жалко.'),
    ),
]


def required(parts):
    """Без чего рама не ходит. Пилон и кабина не в счёт: без них она просто бесполезна."""
    return [slot for slot, *_ in parts if slot.startswith(('hip', 'knee', 'foot', 'leg'))]


HEAD = """# _Warlock
# СОБРАНО СКРИПТОМ Tools/_Warlock/gen_mechs.py — ПРАВИТЬ ЗДЕСЬ БЕССМЫСЛЕННО.
# Вся разница между машинами лежит в таблице MECHS в начале скрипта.
#
{note}
"""


def mechs_yml():
    out = [HEAD.format(note=(
        "# Мехи Братства Стали. Ложатся поверх ванильного BaseMech: пилот, батарея,\n"
        "# слоты оружия и интерфейс ванильные. Своё — экипаж на двоих, посты, смазка\n"
        "# и разборные узлы, всё в компоненте WarlockMech.\n"
        "#\n"
        "# Спрайты — перекрашенная ваниль из mecha.rsi. Плейсхолдеры."))]

    for m in MECHS:
        req = required(m['parts'])
        out.append(f"""- type: entity
  parent: BaseMech
  id: WarlockMech{m['id']}
  name: {m['name']}
  description: >-
    {m['desc']}
  components:
  - type: Sprite
    sprite: Objects/Specific/Mech/mecha.rsi
    color: "{m['color']}"
    layers:
    - state: {m['state']}
      map: [ "enum.MechVisualLayers.Base" ]
  - type: Mech
    baseState: {m['state']}
    openState: {m['state']}-open
    brokenState: {m['state']}-broken
    maxIntegrity: {m['integrity']}
    maxEquipmentAmount: {m['equipment']}
  - type: MovementSpeedModifier
    baseWalkSpeed: {m['speed']}
    baseSprintSpeed: {m['sprint']}
  - type: WarlockMech
    class: {m['cls']}
    maxLubricant: {m['lube']}
    lubricant: {m['lube']}
    turnTime: {m['turn']}
    turnAngle: {m['turn_angle']}
    requiredSlots:
{chr(10).join(f'    - {s}' for s in req)}
  - type: Damageable
    damageContainer: Inorganic
""")

        # Сборочная рама: во что вставляются узлы, пока мех не готов.
        slots = [slot for slot, *_ in m['parts']]
        out.append(f"""- type: entity
  parent: BaseStructureDynamic
  id: WarlockMechFrame{m['id']}
  name: рама «{m['name']}»
  description: >-
    Голый несущий каркас. Пока все гнёзда не заняты, это просто железо на ножках.
  components:
  - type: Sprite
    sprite: Objects/Specific/Mech/ripley_construction.rsi
    color: "{m['color']}"
    state: ripley_chassis
  - type: WarlockMechFrame
    result: WarlockMech{m['id']}
    chassis: {m['id']}
    slots:
{chr(10).join(f'    - {s}' for s in slots)}
  - type: InteractionOutline
  - type: Physics
    bodyType: Dynamic
""")

    return '\n'.join(out)


def parts_yml():
    out = [HEAD.format(note=(
        "# Узлы мехов. Сорок штук: по десять на раму.\n"
        "#\n"
        "# Числа dirtRate и meltRate — не украшение. Ступня стоит в песке и забивается\n"
        "# вчетверо быстрее кабины, а реакторный блок плавится быстрее всех, потому что\n"
        "# и без огня работает горячим. Из-за этого склад запчастей у Братства перекошен:\n"
        "# ступней надо много, оснований — почти никогда."))]

    out.append("""- type: entity
  abstract: true
  parent: BaseItem
  id: WarlockBaseMechPart
  description: Узел рамы Братства. Снимается, теряется, плавится и забивается песком.
  components:
  - type: Item
    size: Huge
  - type: Damageable
    damageContainer: Inorganic
  - type: Tag
    tags:
    - WarlockMechPart
""")

    for m in MECHS:
        out.append(f"# --- {m['name']} " + "-" * (86 - len(m['name'])))
        for slot, name, state, weight, dirt, melt in m['parts']:
            out.append(f"""- type: entity
  parent: WarlockBaseMechPart
  id: WarlockMechPart{m['id']}{slot.title().replace('_', '')}
  name: {name} «{m['name']}»
  components:
  - type: Sprite
    sprite: Objects/Specific/Mech/ripley_construction.rsi
    color: "{m['color']}"
    state: {state}
  - type: WarlockMechPart
    slot: {slot}
    chassis: {m['id']}
    weight: {weight}
    dirtRate: {dirt}
    meltRate: {melt}
""")

    return '\n'.join(out)


def lathe_yml():
    out = [HEAD.format(note=(
        "# Рецепты фабрикатора. Узлы печатаются поштучно: это и есть ремонт мехов\n"
        "# у Братства — не кнопка «починить», а очередь на фабрикаторе за конкретной\n"
        "# ступнёй, которой не хватает."))]

    out.append("""- type: latheRecipe
  abstract: true
  id: WarlockBaseMechPartRecipe
  categories:
  - Mech
  completetime: 8
""")

    for m in MECHS:
        # Мелочь печатается дешевле корпуса: делить стоимость поровну между кабиной
        # и ступнёй значило бы, что ступни никто не печатает.
        share = {
            'cockpit': 1.6, 'reactor': 1.4,
            'hip_l': 1.0, 'hip_r': 1.0, 'leg_a': 0.7, 'leg_b': 0.7, 'leg_c': 0.7, 'leg_d': 0.7,
            'knee_l': 0.7, 'knee_r': 0.7, 'foot_l': 0.5, 'foot_r': 0.5,
            'claw_l': 0.8, 'claw_r': 0.8, 'mount_l': 0.6, 'mount_r': 0.6,
        }
        out.append(f"# --- {m['name']} " + "-" * (86 - len(m['name'])))
        for slot, name, *_ in m['parts']:
            k = share[slot]
            out.append(f"""- type: latheRecipe
  parent: WarlockBaseMechPartRecipe
  id: WarlockMechPart{m['id']}{slot.title().replace('_', '')}
  result: WarlockMechPart{m['id']}{slot.title().replace('_', '')}
  materials:
    Steel: {int(m['steel'] * k / 10) * 10}
    Glass: {int(m['glass'] * k / 10) * 10}
    Plastic: {int(m['plastic'] * k / 10) * 10}
""")

    # Обслуживание печатается на том же фабрикаторе: смазка и ветошь — расходники,
    # без которых остальное не имеет смысла.
    out.append("""- type: latheRecipe
  parent: WarlockBaseMechPartRecipe
  id: WarlockMechGrease
  result: WarlockMechGrease
  completetime: 4
  materials:
    Steel: 200
    Plastic: 300

- type: latheRecipe
  parent: WarlockBaseMechPartRecipe
  id: WarlockMechRag
  result: WarlockMechRag
  completetime: 2
  materials:
    Cloth: 200
""")

    names = []
    for m in MECHS:
        for slot, *_ in m['parts']:
            names.append(f"WarlockMechPart{m['id']}{slot.title().replace('_', '')}")
    names += ['WarlockMechGrease', 'WarlockMechRag']

    out.append("""# Пак целиком уходит в фабрикатор Братства. Отдельным паком, а не добавкой
# в ванильный MechParts: ванильный висит на общестанционном фабрикаторе, а рамы
# Братства печатать всей станции незачем.
- type: latheRecipePack
  id: WarlockMechParts
  recipes:
""" + '\n'.join(f'  - {n}' for n in names) + '\n')

    return '\n'.join(out)


def tools_yml():
    return HEAD.format(note=(
        "# Обслуживание мехов: фабрикатор Братства, смазка и ветошь.\n"
        "#\n"
        "# Смазка и ветошь нарочно дешёвые и расходные. Дорогой расходник копят\n"
        "# и не тратят, а копить смазку — значит ездить на сухой раме и стирать ноги.")) + """
- type: entity
  parent: ExosuitFabricator
  id: WarlockMechFabricator
  name: рамный фабрикатор Братства
  description: >-
    Переваренный под свои нужды экзокостюмный фабрикатор. Печатает узлы рам,
    смазку и ветошь — больше ничего Братству от него не нужно.
  components:
  - type: Sprite
    sprite: Structures/Machines/exosuit_fabricator.rsi
    color: "#8a7f6a"
    layers:
    - state: fab-idle
      map: [ "enum.LatheVisualLayers.IsRunning" ]
    - state: fab-load
      map: [ "enum.MaterialStorageVisualLayers.Inserting" ]
    - state: fab-o
      map: [ "enum.WiresVisualLayers.MaintenancePanel" ]
  - type: Lathe
    idleState: fab-idle
    runningState: fab-active
    staticPacks:
    - WarlockMechParts

- type: entity
  parent: BaseItem
  id: WarlockMechGrease
  name: канистра рамной смазки
  description: >-
    Густая серая смазка Братства. Пахнет горелым и держится в суставе смену.
    Заливается прямо в раму, лить куда-то ещё бессмысленно.
  components:
  - type: Sprite
    sprite: Objects/Tools/bucket.rsi
    color: "#6f6a5a"
    state: icon
  - type: Item
    size: Normal
  - type: WarlockMechGrease
    amount: 100
    pour: 50

- type: entity
  parent: BaseItem
  id: WarlockMechRag
  name: ветошь
  description: >-
    Промасленный лоскут. Единственное, что снимает с узла набившийся песок,
    и единственное, чего на складе Братства всегда не хватает.
  components:
  - type: Sprite
    sprite: Objects/Specific/Janitorial/rag.rsi
    color: "#8d8577"
    state: rag
  - type: Item
    size: Small
  - type: WarlockMechRag
    clean: 45
    uses: 6
"""


def actions_yml():
    return HEAD.format(note=(
        "# Действия экипажа мехов.\n"
        "#\n"
        "# Отдельного действия «выстрелить» нет: стрелок стреляет обычным кликом,\n"
        "# потому что его взаимодействия ретранслируются на мех так же, как у пилота.")) + """
- type: entity
  parent: BaseAction
  id: WarlockActionMechStation
  name: Сменить пост
  description: >-
    Пересесть с рычагов за прицелы и обратно. В одиночку рама или ходит, или стреляет:
    до того и другого разом руки не достают. Со стрелком на борту переключаться не нужно.
  components:
  - type: Action
    itemIconStyle: BigAction
    useDelay: 2
  - type: Sprite
    sprite: Objects/Specific/Mech/mecha_equipment.rsi
    state: mecha_clamp
  - type: InstantAction
    event: !type:WarlockMechStationEvent

- type: entity
  parent: BaseAction
  id: WarlockActionMechGunnerCycle
  name: Сменить орудие
  description: Переключить пилон, с которого стреляет стрелок.
  components:
  - type: Action
    itemIconStyle: BigAction
    useDelay: 1
  - type: Sprite
    sprite: Objects/Specific/Mech/mecha_equipment.rsi
    state: mecha_carbine
  - type: InstantAction
    event: !type:WarlockMechGunnerCycleEvent
"""


def main():
    if not os.path.isdir('Resources/Prototypes/_Warlock'):
        print('Запускать из корня билда.')
        return 2

    for path, text in ((OUT_MECHS, mechs_yml()), (OUT_PARTS, parts_yml()),
                       (OUT_LATHE, lathe_yml()), (OUT_TOOLS, tools_yml()),
                       (OUT_ACTS, actions_yml())):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(text)
        print('%-58s %5d строк' % (path, text.count('\n')))

    print('рам: %d, узлов: %d' % (len(MECHS), sum(len(m['parts']) for m in MECHS)))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
