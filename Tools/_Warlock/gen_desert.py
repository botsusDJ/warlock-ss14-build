#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
_Warlock — генератор основной карты «Пустыня».

Почему генератор, а не ручная правка yml: карта 400x400, и любая переделка
планировки вручную — это тысячи строк. Скрипт держит планировку в читаемом виде
(прямоугольники комнат), а стены, двери, кабели и свет расставляет сам.

Главное здесь — комплекс Варлока. Он собран как храм, а не как коробка:
привратная башня внизу, длинный неф, два поперечных коридора, крылья с комнатами
и восьмигранная апсида с алтарём наверху. Вход один — через привратную.

Гравитация задана самому гриду (inherent), а не генератору: генератор можно
обесточить или взорвать, и тогда вся станция уплывёт. Сам генератор всё равно
стоит — в технической пристройке сбоку от карты, вместе с газовыми шахтами.
"""

import base64
import struct
from collections import defaultdict

# ==================================================================================================
# Тайлы
# ==================================================================================================

TILES = [
    "Space",                    # 0
    "FloorDesert",              # 1
    "FloorLowDesert",           # 2
    "FloorAsteroidSand",        # 3
    "FloorPlanetDirt",          # 4
    "FloorSteel",               # 5
    "FloorDark",                # 6
    "FloorWood",                # 7
    "FloorReinforced",          # 8
    "FloorTechMaint",           # 9
    "FloorAsteroidSandDug",     # 10
    "FloorSteelDirty",          # 11
    "FloorGrayConcrete",        # 12
    "FloorGrayConcreteSmooth",  # 13
    "FloorGrayConcreteMono",    # 14
    "FloorOldConcrete",         # 15
    "FloorWhiteMarble",         # 16
    "FloorDarkMarble",          # 17
    "FloorWoodLarge",           # 18
    "FloorMono",                # 19
    "FloorBasalt",              # 20
    "FloorCave",                # 21
    "FloorGold",                # 22
]
T = {name: i for i, name in enumerate(TILES)}

MAP_W = MAP_H = 400
CHUNK = 16

tiles = {}       # (x, y) -> (tile_id, variant)
entities = []    # (proto, x, y)


def put(x, y, tile, variant=0):
    if 0 <= x < MAP_W and 0 <= y < MAP_H:
        tiles[(x, y)] = (T[tile], variant)


def rect(x0, y0, x1, y1, tile, variant=0):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(x, y, tile, variant)


def ent(proto, x, y):
    entities.append((proto, x, y))


# ==================================================================================================
# Пустыня
# ==================================================================================================

def hash2(x, y, salt=0):
    """Дешёвый детерминированный шум. random не берём: карта должна собираться одинаково."""
    h = (x * 73856093) ^ (y * 19349663) ^ (salt * 83492791)
    h = (h ^ (h >> 13)) * 1274126177
    return (h ^ (h >> 16)) & 0x7FFFFFFF


def build_desert():
    """Песок по всей карте с пятнами низкой пустыни и грунта — иначе поле выглядит плоским."""
    for y in range(MAP_H):
        for x in range(MAP_W):
            n = hash2(x // 7, y // 7, 1) % 100
            if n < 18:
                t = "FloorLowDesert"
            elif n < 26:
                t = "FloorPlanetDirt"
            else:
                t = "FloorDesert"
            tiles[(x, y)] = (T[t], hash2(x, y, 2) % 4)


# ==================================================================================================
# Каркас построек
#
# Стены не рисуются вручную. Собирается множество полов, потом стеной становится
# всё, что к полу примыкает, но само полом не является. Так не бывает дыр в контуре
# и перегородки между соседними комнатами появляются сами, если оставить зазор.
# ==================================================================================================

class Complex:
    def __init__(self, wall, ext_wall=None):
        self.floor = {}       # (x,y) -> tile name
        self.doors = {}       # (x,y) -> proto
        self.wall = wall
        self.ext_wall = ext_wall or wall
        self.rooms = []       # (name, x0,y0,x1,y1)

    def room(self, name, x0, y0, x1, y1, tile):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.floor[(x, y)] = tile
        self.rooms.append((name, x0, y0, x1, y1))

    def blob(self, cells, tile):
        for c in cells:
            self.floor[c] = tile

    def door(self, x, y, proto):
        self.doors[(x, y)] = proto

    def wall_cells(self):
        w = set()
        for (x, y) in self.floor:
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    c = (x + dx, y + dy)
                    if c not in self.floor:
                        w.add(c)
        return w

    def emit(self):
        walls = self.wall_cells()
        # Двери прорезают стену: тайл под ними становится полом, стена снимается.
        for (x, y), proto in self.doors.items():
            walls.discard((x, y))
            self.floor.setdefault((x, y), "FloorGrayConcrete")

        for (x, y), tile in self.floor.items():
            put(x, y, tile)
        for (x, y) in walls:
            put(x, y, "FloorGrayConcrete")
            ent(self.wall, x, y)
        for (x, y), proto in self.doors.items():
            ent(proto, x, y)
        return walls


# ==================================================================================================
# Комплекс Варлока
# ==================================================================================================

WX, WY = 40, 250   # левый нижний угол комплекса на карте

def L(x, y):
    return (WX + x, WY + y)


def build_warlock():
    c = Complex("WallSandstone")

    STONE = "FloorGrayConcrete"
    HALL = "FloorGrayConcreteSmooth"
    ROOM = "FloorOldConcrete"
    WOOD = "FloorWoodLarge"
    TEMPLE = "FloorDarkMarble"
    HOLY = "FloorWhiteMarble"
    TECH = "FloorTechMaint"

    def R(name, x0, y0, x1, y1, tile):
        c.room(name, WX + x0, WY + y0, WX + x1, WY + y1, tile)

    # --- привратная: единственный вход ---------------------------------------------------------
    # Зазоры между привратной, постами и нефом оставлены нарочно: в них встают
    # стены, а двери прорезают их обратно. Без зазора дверь висела бы посреди пола.
    R("gate", 30, 2, 43, 8, STONE)
    R("guard_w", 24, 3, 28, 8, ROOM)
    R("guard_e", 45, 3, 49, 8, ROOM)

    # --- неф: главный коридор снизу доверху ----------------------------------------------------
    R("nave", 34, 10, 39, 67, HALL)

    # --- три поперечных коридора ---------------------------------------------------------------
    R("cross_a", 10, 20, 63, 23, HALL)
    R("cross_b", 10, 40, 63, 43, HALL)
    R("cross_c", 10, 62, 63, 67, HALL)   # шире: это трансепт

    # --- ярус 1: хозяйство ---------------------------------------------------------------------
    R("mess", 10, 8, 21, 18, WOOD)          # трапезная
    R("wash", 23, 8, 32, 18, ROOM)          # умывальня
    R("supply", 41, 8, 50, 18, ROOM)        # снабжение
    R("power", 52, 8, 63, 18, TECH)         # генераторная

    # --- ярус 2: казармы и хранилища -----------------------------------------------------------
    R("barracks_adept", 10, 26, 21, 38, ROOM)
    R("barracks_mage", 23, 26, 32, 38, ROOM)
    R("storage_main", 41, 26, 50, 38, TECH)
    R("vestry", 52, 26, 63, 38, TECH)       # ризница: хранилище утвари

    # --- ярус 3: работа ------------------------------------------------------------------------
    R("library", 10, 46, 21, 60, WOOD)
    R("workshop", 23, 46, 32, 60, TECH)
    R("infirmary", 41, 46, 50, 60, ROOM)
    R("chapterhall", 52, 46, 63, 60, WOOD)

    # --- север: покои старших и храм -----------------------------------------------------------
    R("q_archmage", 10, 70, 23, 76, WOOD)
    R("q_priest", 10, 78, 23, 84, WOOD)
    R("q_highpriest", 50, 70, 63, 76, WOOD)
    R("q_ritualist", 50, 78, 63, 84, WOOD)

    R("sanctuary", 26, 70, 47, 82, TEMPLE)

    # --- апсида: восьмигранник с алтарём -------------------------------------------------------
    cx, cy, r = 36, 88, 9
    apse = []
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            dx, dy = abs(x - cx), abs(y - cy)
            if dx <= r and dy <= r and dx + dy <= r + 4:
                apse.append((WX + x, WY + y))
    c.blob(apse, HOLY)

    # --- срезанные углы: чтобы силуэт не был прямоугольником -----------------------------------
    # Внешние углы корпуса скашиваются по диагонали.
    for corner_x, corner_y, sx, sy in ((10, 8, 1, 1), (63, 8, -1, 1),
                                       (10, 84, 1, -1), (63, 84, -1, -1)):
        for i in range(5):
            for j in range(5 - i):
                cell = (WX + corner_x + sx * i, WY + corner_y + sy * j)
                c.floor.pop(cell, None)

    # ==============================================================================================
    # Двери
    # ==============================================================================================
    GATE = "WarlockAirlockGuildWarlock"
    IN = "WarlockAirlockGuildWarlock"

    def D(x, y, proto=IN):
        c.door(WX + x, WY + y, proto)

    # вход снаружи: двойной шлюз, и это единственный проём в контуре
    D(36, 1, GATE); D(37, 1, GATE)
    D(36, 0, GATE); D(37, 0, GATE)
    # привратная -> неф (зазор y=9)
    D(36, 9); D(37, 9)
    # посты охраны (зазоры x=29 и x=44)
    D(29, 6); D(44, 6)

    # ярус 1 -> коридор A (зазор y=19)
    D(15, 19); D(27, 19); D(45, 19); D(57, 19)
    # ярус 2 -> коридор A (зазор y=24..25)
    for yy in (24, 25):
        D(15, yy); D(27, yy); D(45, yy); D(57, yy)
    # ярус 2 -> коридор B (зазор y=39)
    D(15, 39); D(27, 39); D(45, 39); D(57, 39)
    # ярус 3 -> коридор B (зазор y=44..45)
    for yy in (44, 45):
        D(15, yy); D(27, yy); D(45, yy); D(57, yy)
    # ярус 3 -> коридор C (зазор y=61)
    D(15, 61); D(27, 61); D(45, 61); D(57, 61)
    # покои -> коридор C (зазоры y=68..69)
    for yy in (68, 69):
        D(16, yy); D(56, yy)
    # святилище -> коридор C
    for yy in (68, 69):
        D(36, yy); D(37, yy)
    # покои младших этажей -> верхние покои (зазор y=77)
    D(16, 77); D(56, 77)
    # коридор A и B выходят в неф сами (пересекаются), крылья соединены зазорами x=22, x=33, x=40, x=51
    for xx in (22, 33, 40, 51):
        D(xx, 13); D(xx, 32); D(xx, 53)

    walls = c.emit()

    # ==============================================================================================
    # Начинка
    # ==============================================================================================
    rooms = {name: (x0, y0, x1, y1) for name, x0, y0, x1, y1 in c.rooms}

    def fill(name, proto, positions):
        x0, y0, x1, y1 = rooms[name]
        for dx, dy in positions:
            ent(proto, x0 + dx, y0 + dy)

    # --- алтарь и храм ---
    ent("WarlockAltarAtrak", WX + 36, WY + 90)
    ent("WarlockAltarDjokt", WX + 32, WY + 88)
    ent("WarlockAltarRuzut", WX + 40, WY + 88)
    for i in range(6):
        ent("CandleRed", WX + 31, WY + 84 + i)
        ent("CandleRed", WX + 41, WY + 84 + i)
    # скамьи святилища двумя рядами вдоль прохода
    for row in range(71, 81, 2):
        for xx in list(range(27, 34)) + list(range(39, 46)):
            ent("ChairWood", WX + xx, WY + row)
    ent("WarlockBannerWarlock", WX + 27, WY + 82)
    ent("WarlockBannerWarlock", WX + 46, WY + 82)

    # --- казармы: койки и шкафчики ---
    for name, cols in (("barracks_adept", range(11, 21, 3)), ("barracks_mage", range(24, 32, 3))):
        x0, y0, x1, y1 = rooms[name]
        for xx in cols:
            for yy in range(y0 + 1, y1 - 1, 4):
                ent("Bed", xx, yy)
                ent("LockerSteel", xx + 1, yy)

    # --- покои старших ---
    for name, banner in (("q_archmage", True), ("q_priest", False),
                         ("q_highpriest", True), ("q_ritualist", False)):
        x0, y0, x1, y1 = rooms[name]
        ent("Bed", x0 + 2, y0 + 2)
        ent("TableWood", x0 + 5, y0 + 2)
        ent("ChairWood", x0 + 5, y0 + 3)
        ent("LockerSteel", x0 + 1, y1 - 1)
        ent("BookshelfFilled", x0 + 8, y0 + 1)
        ent("BookshelfFilled", x0 + 9, y0 + 1)
        if banner:
            ent("WarlockBannerWarlock", x0 + 11, y0 + 1)

    # --- библиотека ---
    x0, y0, x1, y1 = rooms["library"]
    for yy in range(y0 + 1, y1, 3):
        for xx in range(x0 + 1, x1):
            ent("BookshelfFilled", xx, yy)
    ent("TableWood", x0 + 4, y0 + 2); ent("ChairWood", x0 + 4, y0 + 3)

    # --- трапезная ---
    x0, y0, x1, y1 = rooms["mess"]
    for yy in range(y0 + 2, y1 - 1, 4):
        for xx in range(x0 + 2, x1 - 1, 3):
            ent("TableWood", xx, yy)
            ent("ChairWood", xx, yy + 1)
            ent("ChairWood", xx, yy - 1)

    # --- хранилища ---
    for name in ("storage_main", "vestry", "supply"):
        x0, y0, x1, y1 = rooms[name]
        for yy in range(y0 + 1, y1, 3):
            for xx in range(x0 + 1, x1, 2):
                ent("Table", xx, yy)
        ent("LockerSteel", x0 + 1, y1 - 1)
        ent("LockerSteel", x0 + 2, y1 - 1)
        ent("OreBox", x1 - 2, y0 + 1)

    # --- мастерская и лазарет ---
    x0, y0, x1, y1 = rooms["workshop"]
    ent("ToolboxMechanicalFilled", x0 + 2, y0 + 2)
    ent("MachineFrame", x0 + 4, y0 + 2)
    ent("SheetSteel", x0 + 6, y0 + 2)
    for yy in range(y0 + 4, y1, 3):
        ent("Table", x0 + 2, yy); ent("Table", x0 + 3, yy)

    x0, y0, x1, y1 = rooms["infirmary"]
    for yy in range(y0 + 2, y1 - 1, 3):
        ent("Bed", x0 + 2, yy)
        ent("Bed", x1 - 2, yy)

    # --- зал собраний ---
    x0, y0, x1, y1 = rooms["chapterhall"]
    for yy in range(y0 + 3, y1 - 2):
        ent("TableWood", x0 + 5, yy); ent("TableWood", x0 + 6, yy)
        ent("ChairWood", x0 + 4, yy); ent("ChairWood", x0 + 7, yy)
    ent("WarlockBannerWarlock", x0 + 1, y1 - 1)

    # --- привратная ---
    x0, y0, x1, y1 = rooms["gate"]
    ent("WarlockBannerWarlock", x0 + 1, y1)
    ent("WarlockBannerWarlock", x1 - 1, y1)
    for name in ("guard_w", "guard_e"):
        gx0, gy0, gx1, gy1 = rooms[name]
        ent("Table", gx0 + 1, gy0 + 1)
        ent("ChairWood", gx0 + 1, gy0 + 2)
        ent("LockerSteel", gx1 - 1, gy1 - 1)
    ent("ComputerId", x0 + 2, y0 + 1)

    # ==============================================================================================
    # Питание, свет, атмосфера
    # ==============================================================================================
    # RTG и подстанция в генераторной, APC-шина по всем полам: свет и шлюзы должны работать.
    px0, py0, px1, py1 = rooms["power"]
    ent("GeneratorRTG", px0 + 2, py0 + 2)
    ent("GeneratorRTG", px0 + 2, py0 + 5)
    ent("SubstationBasic", px0 + 5, py0 + 2)
    ent("ComputerPowerMonitoring", px0 + 8, py0 + 1)
    for xx in range(px0 + 2, px0 + 6):
        ent("CableHV", xx, py0 + 2)
    ent("CableMV", px0 + 5, py0 + 2)
    ent("APCBasic", px0 + 6, py0 + 2)

    for cell in c.floor:
        ent("CableApcExtension", cell[0], cell[1])

    # Свет: по одному светильнику через каждые пять тайлов вдоль стен.
    lit = 0
    for (x, y) in sorted(c.floor):
        if (x + y) % 7:
            continue
        if any((x + dx, y + dy) in walls for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            ent("Poweredlight", x, y)
            lit += 1

    # Воздушные вентили по крупным помещениям — на случай, если позже включат GridAtmosphere.
    for name in ("nave", "cross_a", "cross_b", "cross_c", "sanctuary",
                 "barracks_adept", "barracks_mage", "mess", "library"):
        x0, y0, x1, y1 = rooms[name]
        ent("GasVentPump", (x0 + x1) // 2, (y0 + y1) // 2)
    for name in ("gate", "cross_a", "cross_b", "cross_c"):
        x0, y0, x1, y1 = rooms[name]
        ent("AirAlarm", x0 + 1, y1)

    # ==============================================================================================
    # Точки спавна
    # ==============================================================================================
    x0, y0, x1, y1 = rooms["barracks_adept"]
    for i in range(6):
        ent("WarlockSpawnPointWarlockAdept", x0 + 2 + (i % 3) * 3, y0 + 2 + (i // 3) * 5)
    x0, y0, x1, y1 = rooms["barracks_mage"]
    for i in range(6):
        ent("WarlockSpawnPointWarlockMage", x0 + 2 + (i % 3) * 3, y0 + 2 + (i // 3) * 5)
    for name, proto in (("q_archmage", "WarlockSpawnPointWarlockArchmageBishop"),
                        ("q_priest", "WarlockSpawnPointWarlockPriest"),
                        ("q_highpriest", "WarlockSpawnPointWarlockHighPriest"),
                        ("q_ritualist", "WarlockSpawnPointWarlockRitualist")):
        x0, y0, x1, y1 = rooms[name]
        ent(proto, x0 + 3, y0 + 3)

    x0, y0, x1, y1 = rooms["gate"]
    for i in range(4):
        ent("SpawnPointLatejoin", x0 + 3 + i * 2, y0 + 4)
    ent("SpawnPointObserver", x0 + 6, y0 + 6)

    return c


# ==================================================================================================
# Техническая пристройка: гравитация и воздух
#
# Стоит в стороне от комплекса, за картой боевых действий. Взорвать её можно,
# но гравитация всё равно задана гриду, а не генератору: иначе один заряд
# отправляет в невесомость всю станцию.
# ==================================================================================================

def build_utility():
    ux, uy = 15, 200
    c = Complex("WallSolid")
    c.room("util", ux, uy, ux + 15, uy + 13, "FloorTechMaint")
    c.door(ux + 8, uy - 1, "AirlockGlass")
    c.emit()

    ent("GravityGenerator", ux + 3, uy + 9)
    ent("GravityGeneratorMini", ux + 3, uy + 5)
    ent("GasMinerOxygenStationLarge", ux + 8, uy + 10)
    ent("GasMinerNitrogenStationLarge", ux + 11, uy + 10)
    ent("AirCanister", ux + 13, uy + 2)
    ent("AirCanister", ux + 14, uy + 2)
    ent("GeneratorRTG", ux + 2, uy + 2)
    ent("SubstationBasic", ux + 5, uy + 2)
    ent("APCBasic", ux + 7, uy + 2)
    ent("CableHV", ux + 2, uy + 2)
    ent("CableHV", ux + 3, uy + 2)
    ent("CableHV", ux + 4, uy + 2)
    ent("CableHV", ux + 5, uy + 2)
    ent("CableMV", ux + 5, uy + 2)
    ent("CableMV", ux + 6, uy + 2)
    ent("CableMV", ux + 7, uy + 2)
    for cell in c.floor:
        ent("CableApcExtension", cell[0], cell[1])
    for (x, y) in sorted(c.floor):
        if (x + y) % 9 == 0:
            ent("Poweredlight", x, y)


# ==================================================================================================
# Лагеря Технос и Фактос
#
# Компактные и функциональные: их очередь на нормальную планировку следующая,
# сейчас важно, чтобы роли этих гильдий было где заспавнить.
# ==================================================================================================

def build_camp(ox, oy, wall, door, jobs, banner):
    c = Complex(wall)
    c.room("hall", ox, oy, ox + 29, oy + 9, "FloorSteel")
    c.room("west", ox, oy + 12, ox + 13, oy + 25, "FloorSteelDirty")
    c.room("east", ox + 16, oy + 12, ox + 29, oy + 25, "FloorSteelDirty")
    c.room("link", ox + 13, oy + 10, ox + 16, oy + 11, "FloorSteel")
    c.door(ox + 14, oy - 1, door)
    c.door(ox + 15, oy - 1, door)
    c.door(ox + 6, oy + 11, door)
    c.door(ox + 23, oy + 11, door)
    walls = c.emit()

    ent("GeneratorRTG", ox + 2, oy + 2)
    ent("SubstationBasic", ox + 4, oy + 2)
    ent("APCBasic", ox + 6, oy + 2)
    ent("CableHV", ox + 2, oy + 2); ent("CableHV", ox + 3, oy + 2); ent("CableHV", ox + 4, oy + 2)
    ent("CableMV", ox + 4, oy + 2); ent("CableMV", ox + 5, oy + 2); ent("CableMV", ox + 6, oy + 2)
    ent("GasPort", ox + 9, oy + 2)
    ent("AirCanister", ox + 9, oy + 3)
    for cell in c.floor:
        ent("CableApcExtension", cell[0], cell[1])
    for (x, y) in sorted(c.floor):
        if (x + y) % 8 == 0 and any((x + dx, y + dy) in walls for dx, dy in
                                    ((1, 0), (-1, 0), (0, 1), (0, -1))):
            ent("Poweredlight", x, y)
    ent(banner, ox + 1, oy + 9)
    ent(banner, ox + 28, oy + 9)

    for name in ("west", "east"):
        x0, y0, x1, y1 = [r for r in c.rooms if r[0] == name][0][1:]
        for xx in range(x0 + 1, x1, 3):
            for yy in range(y0 + 1, y1, 4):
                ent("Bed", xx, yy)
                ent("LockerSteel", xx + 1, yy)
        ent("GasVentPump", (x0 + x1) // 2, (y0 + y1) // 2)
    ent("GasVentPump", ox + 15, oy + 5)
    ent("AirAlarm", ox + 1, oy + 9)

    # спавны: старшие в зале, рядовые в жилых блоках
    slots = [(ox + 4 + i * 3, oy + 6) for i in range(6)]
    body = [(ox + 3 + i * 4, oy + 20) for i in range(6)] + \
           [(ox + 19 + i * 4, oy + 20) for i in range(6)]
    for i, proto in enumerate(jobs["lead"]):
        ent(proto, *slots[i % len(slots)])
    for i, proto in enumerate(jobs["rank"]):
        for k in range(6):
            ent(proto, *body[(i * 6 + k) % len(body)])


# ==================================================================================================
# Центр карты: скальный массив, реликвии и две пещеры
# ==================================================================================================

def build_center():
    cx, cy, r = 200, 200, 46
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            dx, dy = x - cx, y - cy
            d = (dx * dx + dy * dy) ** 0.5
            if d > r:
                continue
            edge = r - d
            n = hash2(x, y, 5) % 100
            if edge < 4 and n < 55:
                continue
            put(x, y, "FloorBasalt")
            if n < 3:
                ent("AsteroidRockGold" if n == 0 else
                    "AsteroidRockSilver" if n == 1 else "AsteroidRockUranium", x, y)
            elif n < 6:
                ent("AsteroidRockArtifactFragment", x, y)
            elif n < 55:
                ent("AsteroidRock", x, y)

    # тоннели: три коридора к центру, чтобы массив не был монолитом.
    # Камни из тоннелей вычищаются одним проходом: список сущностей большой,
    # и фильтровать его на каждый тайл — это минуты работы вместо секунд.
    import math
    carved = set()
    for ang in (0, 120, 240):
        a = math.radians(ang)
        for step in range(r + 6):
            x = int(cx + math.cos(a) * step)
            y = int(cy + math.sin(a) * step)
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    put(x + dx, y + dy, "FloorCave")
                    carved.add((x + dx, y + dy))
    entities[:] = [e for e in entities
                   if not (e[0].startswith("AsteroidRock") and (e[1], e[2]) in carved)]

    # реликвии в центре
    k = 0
    for y in range(cy - 8, cy + 9):
        for x in range(cx - 8, cx + 9):
            if hash2(x, y, 7) % 11 == 0:
                ent("WarlockRelic", x, y)
                k += 1
    return k


def build_caves():
    """Две пещеры древних рас. Артефакты остались здесь — из комплекса их убрали."""
    # Кассолюбы: северо-восток
    kx, ky = 300, 300
    for y in range(ky, ky + 22):
        for x in range(kx, kx + 26):
            if hash2(x, y, 11) % 100 < 78:
                put(x, y, "FloorCave")
    for i, proto in enumerate(("WarlockArtefactAmberEye", "WarlockArtefactKassThroat",
                               "WarlockArtefactBlindChoir", "WarlockArtefactHiveInHand",
                               "WarlockArtefactSecondHeart")):
        ent(proto, kx + 4 + i * 4, ky + 11)
    for i in range(6):
        ent("FloraStalagmite", kx + 2 + i * 4, ky + 3)
        ent("CrystalYellow", kx + 3 + i * 4, ky + 18)

    # К'хриты: юго-восток
    hx, hy = 300, 90
    for y in range(hy, hy + 22):
        for x in range(hx, hx + 26):
            if hash2(x, y, 13) % 100 < 78:
                put(x, y, "FloorCave")
    for i, proto in enumerate(("WarlockArtefactGoldenSwarm", "WarlockArtefactCarapaceGraft",
                               "WarlockArtefactSwapSeal", "WarlockArtefactSilenceBell",
                               "WarlockArtefactTarGland")):
        ent(proto, hx + 4 + i * 4, hy + 11)
    for i in range(5):
        ent("WarlockSpawnerKhritScorpion", hx + 5 + i * 4, hy + 5)
        ent("WarlockSpawnerKhritScarab", hx + 5 + i * 4, hy + 16)
        ent("CrystalOrange", hx + 6 + i * 4, hy + 19)


# ==================================================================================================
# Сериализация
# ==================================================================================================

def serialize():
    chunks = defaultdict(lambda: bytearray(7 * CHUNK * CHUNK))
    used = set()
    for (x, y), (tid, var) in tiles.items():
        ci, cj = x // CHUNK, y // CHUNK
        lx, ly = x % CHUNK, y % CHUNK
        buf = chunks[(ci, cj)]
        off = (ly * CHUNK + lx) * 7
        struct.pack_into("<H", buf, off, tid)
        buf[off + 5] = var
        used.add((ci, cj))

    out = []
    out.append("meta:")
    out.append("  format: 7")
    out.append("  category: Map")
    out.append("  engineVersion: 250.0.0")
    out.append("  forkId: \"\"")
    out.append("  forkVersion: \"\"")
    out.append("  time: '2026-08-20T00:00:00.0000000'")
    out.append("  entityCount: %d" % (2 + len(entities)))
    out.append("maps:")
    out.append("- 1")
    out.append("grids:")
    out.append("- 2")
    out.append("orphans: []")
    out.append("nullspace: []")
    out.append("tilemap:")
    for i, name in enumerate(TILES):
        out.append("  %d: %s" % (i, name))
    out.append("entities:")
    out.append("- proto: \"\"")
    out.append("  entities:")
    out.append("  - uid: 1")
    out.append("    components:")
    out.append("    - type: MetaData")
    out.append("      name: Warlock Desert")
    out.append("    - type: Transform")
    out.append("    - type: Map")
    out.append("      mapPaused: True")
    out.append("    - type: GridTree")
    out.append("    - type: Broadphase")
    out.append("    - type: OccluderTree")
    out.append("    - type: Parallax")
    out.append("      parallax: Desert")
    out.append("    - type: MapAtmosphere")
    out.append("      space: False")
    out.append("      mixture:")
    out.append("        volume: 2500")
    out.append("        immutable: True")
    out.append("        temperature: 293.15")
    out.append("        moles:")
    out.append("          Oxygen: 21.824879")
    out.append("          Nitrogen: 82.10312")
    out.append("  - uid: 2")
    out.append("    components:")
    out.append("    - type: MetaData")
    out.append("      name: Warlock Complex")
    out.append("    - type: Transform")
    out.append("      parent: 1")
    out.append("    - type: MapGrid")
    out.append("      chunks:")
    for (ci, cj) in sorted(used):
        out.append("        %d,%d:" % (ci, cj))
        out.append("          ind: %d,%d" % (ci, cj))
        out.append("          tiles: " + base64.b64encode(bytes(chunks[(ci, cj)])).decode())
        out.append("          version: 7")
    out.append("    - type: Broadphase")
    out.append("    - type: Physics")
    out.append("      bodyStatus: InAir")
    out.append("      angularDamping: 0.05")
    out.append("      linearDamping: 0.05")
    out.append("      fixedRotation: True")
    out.append("      bodyType: Static")
    out.append("    - type: Fixtures")
    out.append("      fixtures: {}")
    out.append("    - type: OccluderTree")
    out.append("    - type: SpreaderGrid")
    out.append("    - type: Shuttle")
    out.append("    - type: GridPathfinding")
    # Ключ обязан совпадать с ключом в stations: у прототипа gameMap WarlockDesert,
    # иначе станция не создастся и раунд не начнётся.
    out.append("    - type: BecomesStation")
    out.append("      id: WarlockDesert")
    # Гравитация задаётся гриду напрямую. inherent запрещает GravitySystem гасить её
    # по состоянию генераторов: сама планировка не должна зависеть от одной машины.
    out.append("    - type: Gravity")
    out.append("      enabled: True")
    out.append("      inherent: True")
    out.append("      gravityShakeSound: !type:SoundPathSpecifier")
    out.append("        path: /Audio/Effects/alert.ogg")
    # GasTileOverlay намеренно нет: у грида нет GridAtmosphere, воздух берётся
    # из MapAtmosphere карты, и рисовать оверлею нечего.
    out.append("    - type: RadiationGridResistance")

    by_proto = defaultdict(list)
    for proto, x, y in entities:
        by_proto[proto].append((x, y))

    uid = 3
    for proto in sorted(by_proto):
        out.append("- proto: %s" % proto)
        out.append("  entities:")
        for (x, y) in by_proto[proto]:
            out.append("  - uid: %d" % uid)
            out.append("    components:")
            out.append("    - type: Transform")
            out.append("      pos: %.1f,%.1f" % (x + 0.5, y + 0.5))
            out.append("      parent: 2")
            uid += 1
    return "\n".join(out) + "\n"


def main():
    build_desert()
    build_warlock()
    build_utility()

    build_camp(
        60, 60, "WallShuttle", "WarlockAirlockTechnos",
        {"lead": ["WarlockSpawnPointTechnosArchtechnomage",
                  "WarlockSpawnPointTechnosSupremeTechnocrat",
                  "WarlockSpawnPointTechnosTechnosorcerer"],
         "rank": ["WarlockSpawnPointTechnosTechnomancer",
                  "WarlockSpawnPointTechnosMage",
                  "WarlockSpawnPointTechnosAdept"]},
        "WarlockBannerTechnos")

    build_camp(
        300, 200, "WallSolid", "WarlockAirlockFactos",
        {"lead": ["WarlockSpawnPointFactosArtarchmage",
                  "WarlockSpawnPointFactosGreatSeeker",
                  "WarlockSpawnPointFactosSorcerer"],
         "rank": ["WarlockSpawnPointFactosEnchanter",
                  "WarlockSpawnPointFactosMage",
                  "WarlockSpawnPointFactosAdept"]},
        "WarlockBannerFactos")

    relics = build_center()
    build_caves()

    text = serialize()
    path = "Resources/Maps/_Warlock/desert.yml"
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)

    print("тайлов: %d" % len(tiles))
    print("сущностей: %d" % len(entities))
    print("реликвий в центре: %d" % relics)
    print("записано: %s" % path)


if __name__ == "__main__":
    main()
