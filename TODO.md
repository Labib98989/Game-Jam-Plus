# Core Dump: left to do

## Gameplay (the robot and its parts)
- [x] **Part health.** Parts break off at the first hit (your call). The wear display (flash, orange, red, sparks) is built for parts that take more hits, like armor.
- [ ] **Armor Plate:** a square of 4, the toughest part, taking several hits.
- [ ] **Scope (vision mod):** zooms in. On a Rocket Launcher it gives longer range.
  - Still to decide: what makes it zoom, since mods have no button. Suggestion: hold middle mouse while you have a Scope.
- [ ] **Radar (vision mod):** a minimap of nearby enemies. A longer chain covers more ground.
- [ ] **Rear-view Mirror (vision mod):** a small picture of what's behind you.
- [ ] **Scanner (vision mod):** shows whether a rescued crewmate is an impostor. It needs the crew first.
- [ ] **Flashlight (vision mod, later):** lights up dark rooms.

## The run
- [x] **Room editor**, with pads, ceilings and room types.
- [x] **Rooms:** 8 combat rooms, the workshop and the escape shaft. Tweak them in the editor; more rooms are welcome.
- [x] **Enemies:** Skitter, Spitter, Brute, Diver, Stalker and Hornet.
- [x] **Waves:** budgets, the 20-second timer with early waves, spawns near you, the spawn flash.
- [x] **The run:** main menu, the workshop between rooms (free parts, building only there), rooms small first, mirrored, sometimes pieces, the escape shaft, pause, death and win screens, best time.
- [ ] **Crew:** E to interact, rescue doors, the Mechanic, VIP and Impostor.
- [ ] **Art and audio:**
  - the hit feedback: a clang and a screen shake when a part breaks off
  - the head's beep while it's lost
  - enemy, room and robot models
- [ ] **Gamepad support.**

## To tune in playtests
- [ ] Wave budgets and timer (Waves.cs), enemy speeds, fire rates and health (each enemy's Spawn)
- [ ] Room sizes, ceiling heights and how much is in them (the room generator, or the editor)
- [ ] Jump pad and launch pad strength
- [ ] Getting parts back: snap range (5 m) and Skitters' wait (3 s) in PartChunk.cs, a carrying Skitter's speed (0.55) in Skitter.cs
- [ ] The self-destruct's speed in the escape shaft
- [ ] Treads skid
- [ ] Ram lunge length
- [ ] Rocket-jump force
- [ ] How much each mod gives, the chain falloff and the ceiling (on each mod's asset)
- [ ] Camera framing now that the robot is smaller
- [ ] Build zone reach (12 cells)

## Housekeeping (yours)
- [ ] First git commit. There's still none.
- [ ] Delete the `%TEMP%\unityhub-*` folders (7.9 GB) and the empty `D:\Unity` folder.
- [ ] Add the WebGL module to Unity 6.6 through the Hub, for the web build.
