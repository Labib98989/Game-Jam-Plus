# Writes the tutorial's rooms as room-editor JSON, one small room per lesson (see Run/Tutorial.cs).
# Run: python tools/make_tutorial.py CoreDump/Assets/Resources/Rooms
# It only writes the "Tutorial N ..." rooms. It borrows the Room class from make_rooms.py without
# running that script's room list, which would rewrite every other room (and any editor changes).
# The lessons find their rooms by name order, so keep the numbers at the front of the names.
import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
source = open(os.path.join(HERE, "make_rooms.py"), encoding="utf-8").read().split("\nrooms = [")[0]
helpers = {"__name__": "make_rooms"}
exec(compile(source, "make_rooms.py", "exec"), helpers)   # sets its OUT from sys.argv[1], same as here
Room, SMALL, entry_exit = helpers["Room"], helpers["SMALL"], helpers["entry_exit"]
TUTORIAL = 3


def lamps(r, y, spots):
    for x, z in spots: r.lamp(x, y, z)


rooms = []

# 1. Moving: open floor to run and slide on, and a block 2 m tall to jump onto.
r = Room("Tutorial 1 Moving", SMALL, TUTORIAL); rooms.append(r)
r.shell(30, 50, 14)
entry_exit(r)
r.block(-5, 2, 10, 8, 2)
r.crate(-13, -20); r.crate(11, -16); r.crate(-12, 14)
lamps(r, 9, [(-8, -12), (8, -12), (-8, 12), (8, 12)])

# 2. Jet and wall-run: a long, tall wall down the middle to run along on either side.
r = Room("Tutorial 2 Jet and Wall-run", SMALL, TUTORIAL); rooms.append(r)
r.shell(30, 70, 16)
entry_exit(r)
r.block(-1, -18, 2, 36, 9)
lamps(r, 11, [(-8, -24), (8, -24), (-8, 0), (8, 0), (-8, 24), (8, 24)])

# 3. Pads: a launch pad in front of the entry, and a jump pad beside a 12 m tower with the exit on top.
r = Room("Tutorial 3 Pads", SMALL, TUTORIAL); rooms.append(r)
r.shell(30, 64, 30)
r.mark("entry", 0, 0, -r.d // 2 + 3)
r.launcher(-1, -24, turn=0)
r.block(-6, 14, 12, 12, 12)
r.pad(-10, 9)
r.mark("exit", 0, 12, 23, turn=2)
lamps(r, 20, [(-8, -16), (8, -16), (-10, 4), (10, 4)])

# 4. Shooting: four targets (on ground spawn spots) behind a little cover.
r = Room("Tutorial 4 Shooting", SMALL, TUTORIAL); rooms.append(r)
r.shell(40, 50, 16)
entry_exit(r)
for x, z in [(-12, 4), (-5, 10), (5, 10), (12, 4)]: r.mark("ground", x, 0, z)
r.cover(-8, -4); r.cover(4, -4); r.cover(-2, 16)
lamps(r, 11, [(-10, -12), (10, -12), (-10, 10), (10, 10)])

# 5. Losing parts: open floor with some cover, and vents for the Skitters to come out of.
r = Room("Tutorial 5 Losing Parts", SMALL, TUTORIAL); rooms.append(r)
r.shell(50, 50, 16)
entry_exit(r)
for x, z in [(-16, 14), (16, 14), (0, 18)]: r.mark("vent", x, 0, z)
r.cover(-12, -6); r.cover(8, -6); r.cover(-3, 4, along_x=False); r.crate(-18, -2); r.crate(16, 0)
lamps(r, 11, [(-12, -12), (12, -12), (-12, 12), (12, 12)])

os.makedirs(helpers["OUT"], exist_ok=True)
bad = False
for room in rooms:
    problems = room.check()
    print(f"{room.name}: {len(room.pieces)} pieces" + ("" if not problems else "\n   PROBLEMS: " + "\n   ".join(problems)))
    bad |= bool(problems)
    room.save()
print("ok" if not bad else "fix the problems above")
