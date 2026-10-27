# Writes Core Dump's starter rooms as room-editor JSON. Run: python tools/make_rooms.py CoreDump/Assets/Resources/Rooms
# Coordinates are meters on the 1 m grid; the floor's top is y = 0, x runs east and z runs north.
# Rooms are built for a fast robot: big, closed in by a ceiling, with three layers (the floor,
# rooftops and catwalks around 6-15 m, and tower tops and beams near the ceiling), loops instead of
# dead ends, tall walls to run along, and pads and ramps to get up fast.
import json, os, sys, itertools, random

OUT = sys.argv[1]
SMALL, MEDIUM, LARGE = 0, 1, 2
COMBAT, WORKSHOP, SHAFT = 0, 1, 2
UP = (0, 1, 0)
EAST, WEST, NORTH, SOUTH = (1, 0, 0), (-1, 0, 0), (0, 0, 1), (0, 0, -1)
SOLID = {"floor", "wall", "pillar", "ceiling", "crate", "cover"}
STANDS_ON_FLOOR = {"entry", "exit", "vent", "ground", "rescue", "shop", "pod", "jumppad", "launchpad"}
NEEDS_HEADROOM = {"entry", "exit", "jumppad", "launchpad", "vent", "ground"}


def v(x, y, z):
    return {"x": x, "y": y, "z": z}


def box_of(p):
    m, s = p["min"], p["size"]
    return (m["x"], m["y"], m["z"], m["x"] + s["x"], m["y"] + s["y"], m["z"] + s["z"])


def overlap(a, b):
    return a[0] < b[3] and b[0] < a[3] and a[1] < b[4] and b[1] < a[4] and a[2] < b[5] and b[2] < a[5]


class Room:
    def __init__(self, name, size, kind=COMBAT):
        self.name, self.size, self.kind, self.pieces = name, size, kind, []

    def add(self, kind, x, y, z, sx=1, sy=1, sz=1, turn=0, face=UP, chance=100):
        self.pieces.append({"kind": kind, "min": v(x, y, z), "size": v(sx, sy, sz), "turn": turn, "face": v(*face), "chance": chance})
        return True

    # Whether a box is free of every piece so far, with `margin` meters of space around its sides.
    def fits(self, x, y, z, sx, sy, sz, margin=0):
        b = (x - margin, y, z - margin, x + sx + margin, y + sy, z + sz + margin)
        return not any(overlap(b, box_of(p)) for p in self.pieces)

    def try_add(self, kind, x, y, z, sx=1, sy=1, sz=1, margin=0, **kw):
        if not self.fits(x, y, z, sx, sy, sz, margin): return False
        return self.add(kind, x, y, z, sx, sy, sz, **kw)

    # A slab whose top is at `top`.
    def floor(self, x, z, sx, sz, top=0, thick=1, chance=100):
        return self.add("floor", x, top - thick, z, sx, thick, sz, chance=chance)

    def try_floor(self, x, z, sx, sz, top, thick=1):
        return self.try_add("floor", x, top - thick, z, sx, thick, sz)

    def block(self, x, z, sx, sz, h, base=0, kind="wall", chance=100):
        return self.add(kind, x, base, z, sx, h, sz, chance=chance)

    # Rises `h` over its length toward `turn` (0 north, 1 east, 2 south, 3 west).
    def ramp(self, x, z, sx, sz, h, turn, base=0):
        return self.try_add("ramp", x, base, z, sx, h, sz, turn=turn)

    def pad(self, x, z, base=0):
        return self.try_add("jumppad", x, base, z, 3, 1, 3, margin=0) and self.headroom_ok(x + 1, base, z + 1)

    def launcher(self, x, z, turn, base=0):
        return self.try_add("launchpad", x, base, z, 3, 1, 3, turn=turn)

    def crate(self, x, z, base=0, chance=100):
        return self.try_add("crate", x, base, z, 2, 2, 2, chance=chance)

    def cover(self, x, z, along_x=True, base=0, chance=100):
        sx, sz = (4, 1) if along_x else (1, 4)
        return self.try_add("cover", x, base, z, sx, 2, sz, turn=0 if along_x else 1, chance=chance)

    def lamp(self, x, y, z):
        return self.try_add("lamp", x, y, z)

    def mark(self, kind, x, y, z, face=UP, turn=0):
        return self.try_add(kind, x, y, z, face=face, turn=turn)

    def headroom_ok(self, x, y, z):
        return True

    # The floor, four walls and a ceiling of a w x d room centered on the origin.
    def shell(self, w, d, h):
        self.w, self.d, self.h = w, d, h
        hw, hd = w // 2, d // 2
        self.floor(-hw, -hd, w, d)
        self.block(-hw - 1, -hd - 1, w + 2, 1, h)
        self.block(-hw - 1, hd, w + 2, 1, h)
        self.block(-hw - 1, -hd, 1, d, h)
        self.block(hw, -hd, 1, d, h)
        self.add("ceiling", -hw - 1, h, -hd - 1, w + 2, 1, d + 2)

    # A perch on the inside face of one of the outer walls.
    def wall_perch(self, side, along, y):
        hw, hd = self.w // 2, self.d // 2
        if side == "west": return self.mark("perch", -hw, y, along, EAST)
        if side == "east": return self.mark("perch", hw - 1, y, along, WEST)
        if side == "north": return self.mark("perch", along, y, hd - 1, SOUTH)
        return self.mark("perch", along, y, -hd, NORTH)

    # Beams under the ceiling to grapple onto, running east-west every `every` meters.
    def beams(self, every, rng):
        hw, hd = self.w // 2, self.d // 2
        for z in range(-hd + every, hd - 4, every):
            self.try_add("wall", -hw, self.h - 5, z, self.w, 2, 2)

    # A random empty floor cell, at least `margin` from anything, inside x/z ranges.
    def free_floor(self, rng, xr, zr, y=0, margin=1, tries=60):
        for _ in range(tries):
            x, z = rng.randint(*xr), rng.randint(*zr)
            if self.fits(x, y, z, 1, 3, 1, margin) and self.supported(x, y, z):
                return x, z
        return None

    def supported(self, x, y, z):
        for p in self.pieces:
            if p["kind"] not in SOLID: continue
            b = box_of(p)
            if b[4] == y and b[0] <= x < b[3] and b[2] <= z < b[5]: return True
        return False

    def solid_at(self, x, y, z):
        c = (x, y, z, x + 1, y + 1, z + 1)
        return any(p["kind"] in SOLID and overlap(c, box_of(p)) for p in self.pieces)

    # Scatters markers of a kind on free floor cells.
    def scatter(self, kind, count, rng, xr, zr, y=0, margin=2):
        placed = 0
        for _ in range(count * 8):
            spot = self.free_floor(rng, xr, zr, y, margin)
            if spot and self.mark(kind, spot[0], y, spot[1]): placed += 1
            if placed == count: break
        return placed

    def check(self):
        problems = []
        boxes = [(p, box_of(p)) for p in self.pieces]
        for (a, ba), (b, bb) in itertools.combinations(boxes, 2):
            if overlap(ba, bb):
                problems.append(f"{a['kind']}@{tuple(a['min'].values())} overlaps {b['kind']}@{tuple(b['min'].values())}")
        for p in self.pieces:
            k, m, s, f = p["kind"], p["min"], p["size"], p["face"]
            cx, cz = m["x"] + s["x"] // 2, m["z"] + s["z"] // 2
            if k in STANDS_ON_FLOOR or (k == "perch" and f["y"] == 1):
                if not self.supported(cx, m["y"], cz): problems.append(f"{k}@{tuple(m.values())} has nothing under it")
            if k == "perch" and f["y"] == 0 and not self.solid_at(m["x"] - f["x"], m["y"], m["z"] - f["z"]):
                problems.append(f"perch@{tuple(m.values())} has no wall behind it")
            if k in NEEDS_HEADROOM and any(self.solid_at(cx, m["y"] + dy, cz) for dy in (1, 2)):
                problems.append(f"{k}@{tuple(m.values())} has no headroom")
        kinds = [p["kind"] for p in self.pieces]
        spawns = sum(k in ("vent", "ground", "perch", "air") for k in kinds)
        if kinds.count("entry") != 1: problems.append(f"{kinds.count('entry')} entries")
        if self.kind == COMBAT and spawns < 12: problems.append(f"only {spawns} spawn spots")
        if self.kind != SHAFT and "exit" not in kinds: problems.append("no exit")
        return problems

    def save(self):
        data = {"version": 1, "name": self.name, "size": self.size, "type": self.kind, "pieces": self.pieces}
        with open(os.path.join(OUT, self.name + ".json"), "w", encoding="utf-8") as f:
            json.dump(data, f, indent=4)

    def summary(self):
        kinds = [p["kind"] for p in self.pieces]
        count = lambda *ks: sum(kinds.count(k) for k in ks)
        return (f"{self.w}x{self.d}x{self.h}  pieces {len(self.pieces)}  pads {count('jumppad')}/{count('launchpad')}  "
                f"vents {count('vent')} ground {count('ground')} perches {count('perch')} air {count('air')} lamps {count('lamp')}")


# Spawn spots and lamps every combat room gets, spread over the room's free floor and air.
def populate(r, rng, vents=8, grounds=5, airs=6, wall_perches=6, lamp_every=22):
    hw, hd, h = r.w // 2, r.d // 2, r.h
    inner_x, inner_z = (-hw + 2, hw - 3), (-hd + 6, hd - 3)
    r.scatter("vent", vents, rng, inner_x, inner_z, margin=2)
    r.scatter("ground", grounds, rng, inner_x, inner_z, margin=3)
    placed = 0
    for _ in range(airs * 10):
        x, z, y = rng.randint(-hw + 6, hw - 7), rng.randint(-hd + 6, hd - 7), rng.randint(h - 14, h - 8)
        if r.fits(x, y, z, 1, 1, 1, 3) and r.mark("air", x, y, z): placed += 1
        if placed == airs: break
    sides = ["west", "east", "north", "south"]
    for i in range(wall_perches * 4):
        side = sides[i % 4]
        along = rng.randint(-(hd if side in ("west", "east") else hw) + 4, (hd if side in ("west", "east") else hw) - 5)
        if r.wall_perch(side, along, rng.randint(5, max(6, h // 2))): wall_perches -= 1
        if wall_perches == 0: break
    for _ in range(40):
        spot = r.free_floor(rng, (-hw + 1, -hw + 1), (-hd + 4, hd - 5)) or r.free_floor(rng, (hw - 2, hw - 2), (-hd + 4, hd - 5))
        if spot and r.mark("rescue", spot[0], 0, spot[1]): break
    for x in range(-hw + lamp_every // 2, hw, lamp_every):
        for z in range(-hd + lamp_every // 2, hd, lamp_every):
            for dy in (0, 2, -2, 4):
                if r.lamp(x, int(h * 0.55) + dy, z): break


def entry_exit(r, exit_top=0):
    hd = r.d // 2
    r.mark("entry", 0, 0, -hd + 3)
    r.mark("exit", 0, exit_top, hd - 2, turn=2)


# ---- City: buildings on a grid of streets, Titanfall style ----

def city(name, size, w, d, h, cell, foot, heights, seed, tower_heights):
    rng = random.Random(seed)
    r = Room(name, size)
    r.shell(w, d, h)
    hw, hd = w // 2, d // 2
    nx, nz = w // cell, d // cell
    ox, oz = -hw + (w - nx * cell) // 2, -hd + (d - nz * cell) // 2
    ci = nx // 2
    plazas = {(ci, 0), (ci, nz - 1)}
    inner = [(i, j) for i in range(nx) for j in range(1, nz - 1) if (i, j) not in plazas]
    plazas |= set(rng.sample(inner, max(1, len(inner) // 8)))
    entry_exit(r)
    buildings = {}
    for i in range(nx):
        for j in range(nz):
            x0, z0 = ox + i * cell, oz + j * cell
            if (i, j) in plazas:
                # A plaza's middle gets something to fight around: a pillar cluster or a low block.
                cx, cz = x0 + cell // 2, z0 + cell // 2
                if (i, j) not in ((ci, 0), (ci, nz - 1)):
                    if rng.random() < 0.5:
                        for dx, dz in ((-4, -4), (2, -4), (-4, 2), (2, 2)): r.add("pillar", cx + dx, 0, cz + dz, 2, rng.choice((10, 14, 18)), 2)
                    else:
                        r.block(cx - 3, cz - 3, 6, 6, 3)
                        r.mark("perch", cx, 3, cz)
                continue
            if rng.random() < 0.16:
                th = rng.choice(tower_heights)
                tx, tz = x0 + cell // 2 - 2, z0 + cell // 2 - 2
                r.add("pillar", tx, 0, tz, 4, th, 4)
                r.floor(tx - 2, tz - 2, 8, 8, top=th + 1)
                buildings[(i, j)] = ("tower", tx - 2, tz - 2, 8, 8, th + 1)
                continue
            bw, bd, bh = rng.randint(*foot), rng.randint(*foot), rng.choice(heights)
            bx, bz = x0 + (cell - bw) // 2, z0 + (cell - bd) // 2
            r.block(bx, bz, bw, bd, bh)
            buildings[(i, j)] = ("building", bx, bz, bw, bd, bh)

    # Bridges between neighboring rooftops of about the same height.
    for (i, j), b in list(buildings.items()):
        for di, dj in ((1, 0), (0, 1)):
            n = buildings.get((i + di, j + dj))
            if not n or b[0] != "building" or n[0] != "building" or abs(b[5] - n[5]) > 3 or rng.random() > 0.55: continue
            top = min(b[5], n[5])
            if di:
                lo, hi = max(b[2], n[2]), min(b[2] + b[4], n[2] + n[4])
                if hi - lo >= 4: r.try_floor(b[1] + b[3], (lo + hi) // 2 - 2, n[1] - b[1] - b[3], 4, top)
            else:
                lo, hi = max(b[1], n[1]), min(b[1] + b[3], n[1] + n[3])
                if hi - lo >= 4: r.try_floor((lo + hi) // 2 - 2, b[2] + b[4], 4, n[2] - b[2] - b[4], top)

    # Ways up: ramps onto low roofs, jump pads by the rest, a pad at every tower's foot.
    for (i, j), (kind, bx, bz, bw, bd, bh) in buildings.items():
        if kind == "tower":
            for px, pz in ((bx + 8, bz + 2), (bx - 3, bz + 2), (bx + 2, bz + 8), (bx + 2, bz - 3)):
                if r.pad(px, pz): break
            r.mark("perch", bx + 1, bh, bz + 1)
            continue
        side = rng.choice("nsew")
        if bh <= 6:
            length = bh * 2
            if side == "s": r.ramp(bx + rng.randint(0, bw - 4), bz - length, 4, length, bh, 0)
            elif side == "n": r.ramp(bx + rng.randint(0, bw - 4), bz + bd, 4, length, bh, 2)
            elif side == "w": r.ramp(bx - length, bz + rng.randint(0, bd - 4), length, 4, bh, 1)
            else: r.ramp(bx + bw, bz + rng.randint(0, bd - 4), length, 4, bh, 3)
        elif rng.random() < 0.75:
            for s in [side] + list("nsew"):
                spot = {"s": (bx + rng.randint(0, bw - 3), bz - 4), "n": (bx + rng.randint(0, bw - 3), bz + bd + 1),
                        "w": (bx - 4, bz + rng.randint(0, bd - 3)), "e": (bx + bw + 1, bz + rng.randint(0, bd - 3))}[s]
                if r.pad(*spot): break
        if rng.random() < 0.65: r.mark("perch", bx + rng.randint(1, bw - 2), bh, bz + rng.randint(1, bd - 2))
        if rng.random() < 0.4: r.mark("perch", bx + bw, max(4, bh // 2), bz + bd // 2, EAST)
        if rng.random() < 0.4: r.mark("perch", bx - 1, max(4, bh // 2), bz + bd // 2, WEST)

    # Launch pads down the middle of some streets, and a little cover.
    for i in range(1, nx):
        for j in range(1, nz):
            if rng.random() < 0.3: r.launcher(ox + i * cell - 1, oz + j * cell - 1, rng.randrange(4))
    for _ in range(nx * nz):
        spot = r.free_floor(rng, (-hw + 3, hw - 6), (-hd + 8, hd - 6), margin=2)
        if spot: r.cover(spot[0], spot[1], along_x=rng.random() < 0.5, chance=rng.choice((100, 100, 75, 50)))
        spot = r.free_floor(rng, (-hw + 3, hw - 6), (-hd + 8, hd - 6), margin=2)
        if spot: r.crate(spot[0], spot[1], chance=rng.choice((100, 75, 50)))
    r.beams(cell * 2, rng)
    populate(r, rng)
    return r


# ---- Boneyard: open ground full of towers of every height ----

def boneyard(name, size, w, d, h, towers, seed):
    rng = random.Random(seed)
    r = Room(name, size)
    r.shell(w, d, h)
    hw, hd = w // 2, d // 2
    entry_exit(r)
    placed = []
    for _ in range(towers * 20):
        if len(placed) == towers: break
        tw, td, th = rng.randint(4, 10), rng.randint(4, 10), rng.randint(6, h - 12)
        x, z = rng.randint(-hw + 4, hw - 4 - tw), rng.randint(-hd + 10, hd - 8 - td)
        if abs(x + tw / 2) < 8 and (z < -hd + 14 or z + td > hd - 12): continue
        if not r.fits(x, 0, z, tw, th, td, margin=7): continue
        r.block(x, z, tw, td, th, kind="pillar" if tw <= 4 or td <= 4 else "wall")
        placed.append((x, z, tw, td, th))
    # Platforms strung between towers close enough to jump between.
    for a, b in itertools.combinations(placed, 2):
        top = min(a[4], b[4])
        if top < 6 or rng.random() > 0.5: continue
        if a[0] + a[2] <= b[0] and b[0] - (a[0] + a[2]) <= 16:
            lo, hi = max(a[1], b[1]), min(a[1] + a[3], b[1] + b[3])
            if hi - lo >= 3: r.try_floor(a[0] + a[2], (lo + hi) // 2 - 1, b[0] - a[0] - a[2], 3, top)
        elif a[1] + a[3] <= b[1] and b[1] - (a[1] + a[3]) <= 16:
            lo, hi = max(a[0], b[0]), min(a[0] + a[2], b[0] + b[2])
            if hi - lo >= 3: r.try_floor((lo + hi) // 2 - 1, a[1] + a[3], 3, b[1] - a[1] - a[3], top)
    for (x, z, tw, td, th) in placed:
        if th >= 6:
            for px, pz in ((x + tw + 1, z), (x - 4, z), (x, z + td + 1), (x, z - 4)):
                if r.pad(px, pz): break
        if rng.random() < 0.7: r.mark("perch", x + tw // 2, th, z + td // 2)
        if rng.random() < 0.3: r.mark("perch", x + tw, max(4, th // 2), z + td // 2, EAST)
    # Pairs of tall walls to run between, out in the open.
    for _ in range(40):
        x, z = rng.randint(-hw + 6, hw - 20), rng.randint(-hd + 12, hd - 30)
        along_x = rng.random() < 0.5
        a = (x, z, 24, 2) if along_x else (x, z, 2, 24)
        b = (x, z + 10, 24, 2) if along_x else (x + 10, z, 2, 24)
        if r.fits(a[0], 0, a[1], a[2], 12, a[3], 3) and r.fits(b[0], 0, b[1], b[2], 12, b[3], 3):
            r.block(a[0], a[1], a[2], a[3], 12)
            r.block(b[0], b[1], b[2], b[3], 12)
            r.mark("perch", a[0] + 1, 12, a[1])
            if sum(1 for p in r.pieces if p["kind"] == "wall" and p["size"]["y"] == 12) >= 6: break
    for _ in range(6):
        spot = r.free_floor(rng, (-hw + 4, hw - 7), (-hd + 10, hd - 8), margin=3)
        if spot: r.launcher(spot[0] - 1, spot[1] - 1, rng.randrange(4))
    for _ in range(w * d // 300):
        spot = r.free_floor(rng, (-hw + 3, hw - 6), (-hd + 8, hd - 6), margin=2)
        if spot: (r.cover if rng.random() < 0.5 else r.crate)(spot[0], spot[1], chance=rng.choice((100, 75, 50)))
    r.beams(24, rng)
    populate(r, rng)
    return r


# ---- Terraces: the room climbs in tiers toward the exit ----

def terraces(name, size, w, d, h, seed):
    rng = random.Random(seed)
    r = Room(name, size)
    r.shell(w, d, h)
    hw, hd = w // 2, d // 2
    b1 = -hd + int(d * 0.4)
    b2 = b1 + int(d * 0.3)
    r.floor(-hw, b1, w, b2 - b1, top=6, thick=6)
    r.floor(-hw, b2, w, hd - b2, top=12, thick=12)
    entry_exit(r, exit_top=12)
    for x in (-hw // 2 - 4, hw // 2 - 4):
        r.ramp(x, b1 - 12, 8, 12, 6, 0)
        r.pad(x + 14, b1 - 4)
    for x in (-hw // 3 - 4, hw // 3 - 4):
        r.ramp(x, b2 - 12, 8, 12, 6, 0, base=6)
        r.pad(x + 14, b2 - 4, base=6)
    # The low tier: two long walls to run between, with a gap in the middle for the way in, and pillars.
    for z in (-hd + 8, -hd + 18):
        for x0, x1 in ((-hw + 6, -8), (8, hw - 6)):
            if r.fits(x0, 0, z, x1 - x0, 12, 2):
                r.block(x0, z, x1 - x0, 2, 12)
                r.mark("perch", (x0 + x1) // 2, 12, z)
    for _ in range(4):
        spot = r.free_floor(rng, (-hw + 4, hw - 6), (-hd + 6, b1 - 16), margin=4)
        if spot: r.try_add("pillar", spot[0], 0, spot[1], 2, rng.choice((16, 20, 24)), 2, margin=2)
    # Every tier gets buildings to climb, as many as its size allows, with pads by the tall ones.
    def buildings(y, z0, z1):
        count = w * (z1 - z0) // 420
        for _ in range(count * 8):
            if count <= 0: break
            bw, bd, bh = rng.randint(5, 10), rng.randint(5, 10), rng.choice((3, 6, 9, 12, 15))
            x, z = rng.randint(-hw + 4, hw - 4 - bw), rng.randint(z0, max(z0, z1 - bd))
            if not (r.fits(x, y, z, bw, bh, bd, margin=4) and r.supported(x + bw // 2, y, z + bd // 2)): continue
            r.block(x, z, bw, bd, bh, base=y)
            r.mark("perch", x + bw // 2, y + bh, z + bd // 2)
            if bh >= 6:
                for px, pz in ((x + bw + 1, z), (x - 4, z), (x, z + bd + 1), (x, z - 4)):
                    if r.pad(px, pz, base=y): break
            count -= 1
    buildings(0, -hd + 22, b1 - 14)
    buildings(6, b1 + 2, b2 - 14)
    buildings(12, b2 + 2, hd - 6)
    # The top tier: a tall tower with a pad, and a catwalk across above the middle tier.
    tx = rng.choice((-hw // 2, hw // 2 - 8))
    if r.try_add("wall", tx, 12, b2 + 8, 8, 16, 8, margin=4):
        r.pad(tx + 10, b2 + 10, base=12)
        r.mark("perch", tx + 4, 28, b2 + 12)
    r.try_floor(-hw, b2 - 6, w, 4, 22)
    for x in (-hw // 2, 0, hw // 2):
        r.pad(x - 1, b2 - 12, base=6)
    for z_range, y in (((-hd + 8, b1 - 3), 0), ((b1 + 2, b2 - 3), 6), ((b2 + 2, hd - 4), 12)):
        for _ in range(w // 8):
            spot = r.free_floor(rng, (-hw + 3, hw - 6), z_range, y=y, margin=2)
            if spot: (r.cover if rng.random() < 0.6 else r.crate)(spot[0], spot[1], base=y, chance=rng.choice((100, 75, 50)))
    for y, z_range in ((6, (b1 + 2, b2 - 3)), (12, (b2 + 2, hd - 4))):
        r.scatter("vent", 2, rng, (-hw + 3, hw - 4), z_range, y=y)
        r.scatter("ground", 2, rng, (-hw + 3, hw - 4), z_range, y=y)
    r.beams(20, rng)
    populate(r, rng, vents=5, grounds=3)
    return r


# ---- Hall: a long hangar with galleries down both sides and bridges across ----

def hall(name, size, w, d, h, seed):
    rng = random.Random(seed)
    r = Room(name, size)
    r.shell(w, d, h)
    hw, hd = w // 2, d // 2
    entry_exit(r)
    g = 10
    for x in (-hw, hw - g):
        r.floor(x, -hd + 12, g, d - 24, top=10)                       # the galleries
        for z in range(-hd + 16, hd - 12, 20): r.add("pillar", x + (g - 2 if x < 0 else 0), 0, z, 2, 9, 2)
    for z in (-hd + 40, 0, hd - 40): r.try_floor(-hw + g, z - 2, w - 2 * g, 4, 18)   # bridges across
    for z in (-hd + 40, 0, hd - 40):                                  # pads up from the galleries to them
        r.pad(-hw + 3, z - 6, base=10); r.pad(hw - 6, z + 3, base=10)
    for x in (-hw + g, hw - g - 4):                                    # ramps up to the galleries
        for z in (-hd + 20, hd - 44):
            r.ramp(x, z, 4, 20, 10, 0 if z < 0 else 2)
    # Gates across the hall, splitting it into three halls you can see between.
    for z in (-hd // 3, hd // 3):
        x = -hw + g
        while x < hw - g:
            seg = min(12, hw - g - x)
            r.block(x, z, seg, 2, 14)
            x += seg + 12
        r.mark("perch", -hw + g + 6, 14, z)
    # The spine: stacks of containers down the middle.
    z = -hd + 22
    while z < hd - 22:
        cw, cd, ch = rng.randint(6, 10), rng.randint(6, 12), rng.choice((4, 6, 8))
        if r.fits(-cw // 2, 0, z, cw, ch, cd, 2):
            r.block(-cw // 2, z, cw, cd, ch)
            r.mark("perch", 0, ch, z + cd // 2)
            if ch <= 4: r.ramp(-cw // 2 - 8, z, 8, 4, ch, 1)
            else: r.pad(cw // 2 + 2, z, 0)
        z += cd + rng.randint(10, 16)
    # Walls to run along in the aisles, and tall pillars to grapple.
    for x in (-hw // 2 + 2, hw // 2 - 4):
        z = -hd + 18
        while z < hd - 30:
            if r.fits(x, 0, z, 2, 14, 24, 2): r.block(x, z, 2, 24, 14)
            z += 24 + rng.randint(14, 22)
    for _ in range(8):
        spot = r.free_floor(rng, (-hw + g + 4, hw - g - 6), (-hd + 10, hd - 10), margin=5)
        if spot: r.try_add("pillar", spot[0], 0, spot[1], 3, rng.choice((20, 26, 30)), 3, margin=2)
    for _ in range(6):
        spot = r.free_floor(rng, (-hw + g + 4, hw - g - 6), (-hd + 10, hd - 10), margin=3)
        if spot: r.launcher(spot[0] - 1, spot[1] - 1, rng.choice((0, 2)))
    for x in (-hw + 2, hw - g + 2):
        for z in range(-hd + 20, hd - 20, 28): r.mark("perch", x + 3, 10, z)
    for _ in range(w * d // 250):
        spot = r.free_floor(rng, (-hw + g + 2, hw - g - 4), (-hd + 8, hd - 6), margin=2)
        if spot: (r.cover if rng.random() < 0.5 else r.crate)(spot[0], spot[1], chance=rng.choice((100, 75, 50)))
    r.beams(16, rng)
    populate(r, rng, vents=10, grounds=6, airs=8)
    return r


# ---- Reactor: a loop around a core that reaches the ceiling ----

def reactor(name, size, w, d, h, seed):
    rng = random.Random(seed)
    r = Room(name, size)
    r.shell(w, d, h)
    hw, hd = w // 2, d // 2
    entry_exit(r)
    r.block(-10, -10, 20, 20, h)                                      # the core
    for top in (12, 24):                                              # rings around it
        r.floor(-14, -14, 28, 4, top); r.floor(-14, 10, 28, 4, top)
        r.floor(-14, -10, 4, 20, top); r.floor(10, -10, 4, 20, top)
        for x, z in ((-12, -12), (11, 11)): r.mark("perch", x, top, z)
    # An inner wall loop with a gap in the middle of each side; its top is a walkway.
    q, t, gap = 32, 3, 7
    for sx in (-q - t, q):
        r.block(sx, -q - t, t, q + t - gap, 12); r.block(sx, gap, t, q + t - gap, 12)
    for sz in (-q - t, q):
        r.block(-q, sz, q - gap, t, 12); r.block(gap, sz, q - gap, t, 12)
    for x, z in ((-q - 2, -20), (q + 1, 20), (-20, q + 1), (20, -q - 2)): r.mark("perch", x, 12, z)
    # Bridges from the walkway to the core's lower ring.
    r.try_floor(14, 9, q - 14, 4, 12); r.try_floor(-q, -13, q - 14, 4, 12)
    r.try_floor(-13, 14, 4, q - 14, 12); r.try_floor(9, -q, 4, q - 14, 12)
    # Pads up to the rings and the walkway.
    for x, z in ((-17, -3), (14, -3), (-3, -17), (-3, 14)): r.pad(x, z)
    for x, z in ((-q - 7, -10), (q + 4, 10), (10, -q - 7), (-10, q + 4)): r.pad(x, z)
    # Launch pads that throw you around the outer loop.
    for x, z, turn in ((-hw + 8, -20, 0), (hw - 11, 20, 2), (20, -hd + 8, 3), (-20, hd - 11, 1)): r.launcher(x, z, turn)
    # Towers in the outer corners.
    for cx, cz in ((-hw + 6, -hd + 12), (hw - 14, -hd + 12), (-hw + 6, hd - 14), (hw - 14, hd - 14)):
        th = rng.choice((14, 18, 22))
        r.block(cx, cz, 8, 8, th, kind="pillar")
        r.mark("perch", cx + 4, th, cz + 4)
        for px, pz in ((cx + 9, cz + 2), (cx + 2, cz + 9), (cx - 4, cz + 2)):
            if r.pad(px, pz): break
    for _ in range(w * d // 300):
        spot = r.free_floor(rng, (-hw + 3, hw - 6), (-hd + 8, hd - 6), margin=2)
        if spot: (r.cover if rng.random() < 0.5 else r.crate)(spot[0], spot[1], chance=rng.choice((100, 75, 50)))
    r.beams(18, rng)
    populate(r, rng)
    return r


rooms = [
    terraces("Loading Dock", SMALL, 80, 80, 32, seed=11),
    city("Crossroads", SMALL, 80, 80, 32, cell=20, foot=(10, 12), heights=(6, 9, 12, 15), seed=21, tower_heights=(16, 18)),
    boneyard("Pump Room", SMALL, 80, 80, 32, towers=12, seed=31),
    city("Cargo Bay", MEDIUM, 110, 110, 40, cell=22, foot=(10, 14), heights=(6, 9, 12, 15, 18), seed=41, tower_heights=(20, 24)),
    reactor("Reactor", MEDIUM, 110, 110, 40, seed=51),
    city("Server Farm", MEDIUM, 108, 108, 40, cell=18, foot=(6, 9), heights=(9, 12, 15, 18), seed=61, tower_heights=(22, 26)),
    hall("Hangar", LARGE, 90, 160, 40, seed=71),
    terraces("Atrium", LARGE, 140, 140, 48, seed=81),
]

# ---- The workshop, between rooms ----

r = Room("Workshop", SMALL, WORKSHOP); rooms.append(r)
r.shell(24, 24, 10)
r.mark("entry", 0, 0, -10); r.mark("exit", 0, 0, 11, turn=2)
for x, z in [(-8, -2), (-8, 3), (-4, 6), (3, 6), (7, 3), (7, -2)]: r.mark("shop", x, 0, z)   # six stands in a horseshoe
r.crate(-12, -12); r.crate(10, -12); r.crate(-12, 8, chance=50); r.crate(10, 9)
for x, y, z in [(-11, 6, -11), (10, 6, -11), (-11, 6, 10), (10, 6, 10), (0, 8, 0)]: r.lamp(x, y, z)

# ---- The escape shaft, at the end ----

r = Room("Escape Shaft", SMALL, SHAFT); rooms.append(r)
r.shell(16, 16, 66)
r.mark("entry", 0, 0, 0, turn=2)
slots = [(-8, -8), (-2, -8), (4, -8), (4, -2), (4, 4), (-2, 4), (-8, 4), (-8, -2)]   # around the walls, clockwise
for i in range(28):
    x, z = slots[i % len(slots)]
    r.floor(x, z, 4, 4, top=2 * (i + 1))              # a platform every 2 m, one Spring jump apart
r.floor(-8, 2, 16, 6, top=58)                         # the top deck
r.mark("pod", 0, 58, 5)
for y in range(6, 64, 8): r.lamp(0, y, 0)

os.makedirs(OUT, exist_ok=True)
bad = False
for room in rooms:
    problems = room.check()
    print(f"{room.name}: {room.summary()}" + ("" if not problems else "\n   PROBLEMS: " + "\n   ".join(problems[:12])))
    bad |= bool(problems)
    room.save()
print("ok" if not bad else "fix the problems above")
