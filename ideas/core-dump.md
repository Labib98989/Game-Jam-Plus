# Core Dump

**Genre:** 3D roguelite movement shooter
**Engine:** Unity
**Themes:** One More Time (main), Something's Off (the impostor crew)
**Status:** The crafting rules, movement parts, weapons, enemies and rooms are settled. Mods (including the vision mods) and armor are still a draft.

## Pitch

Aliens have taken the ship. Before they fell, the crew stored vital data in the core of their maintenance robot, and now it has to escape. The problem: it's down to almost nothing, just a small block of a body, its head and a few spare parts. Between rooms, a workshop hands out free parts, and you fit them onto your robot like 3D Tetris pieces, wherever you can make them fit. Your parts are your weapons, your movement and your armor. Save the crew you find along the way, if they really are crew.

## Theme fit

- **One More Time:** a winning run takes about 10 minutes, every run offers different parts, and you're back in a new run within 2 seconds of dying.
- **Something's Off:** some of the crew you rescue are aliens in disguise. The first time one turns on you is the "Wait. What?" moment. After that, spotting them is a skill.

## Core loop

1. Clear a room with whatever parts you have.
2. In the workshop, take free parts and fit them onto your robot.
3. Optionally take on a rescue challenge for a crewmate you can't identify in advance.
4. Move to the next room. Reach the escape pod with the data intact.

## Run structure

- **Main menu:** start a run, how to play, quit, and your best time, over a view of the workshop.
- **Start:** the workshop, as the maintenance bay. You start with the head, two Legs, a Scatter Blaster, a Spring and a Jet already fitted, so you can walk, shoot, jump and boost from the first second.
- **Combat rooms:** 6 per run, picked from a pool of hand-made rooms (see Rooms). Small rooms come first and big ones later, and no room comes up twice in a run. Exits stay locked until the room is cleared. Each room takes about 60–90 seconds.
- **Rescue doors:** in 3 of the 6 rooms, picked from the rooms that have a spot for one.
- **Between rooms: the workshop.** Its stands hold 6 random parts, and everything is free: walk into a stand to take its part. Every visit also hands over a few connectors (2 Connectors, a Bar and an Elbow). The workshop repairs every part and 3 bars of hull. Crew perks trigger here.
- **Building only happens in the workshop.** Tab opens the build screen there and nowhere else, never mid-fight. Parts you take off stay in your inventory for later.
- **Finale: self-destruct.** After the last room and one more workshop, climb the escape shaft to the pod while the self-destruct rises below you, for about 60–90 seconds. Touching it hurts and bounces you back up. There's no boss.
- **End screen:** who escaped with you, rooms cleared, your time and your best time.
- **Death:** a short stats card, then Space goes straight into a new run in under 2 seconds.
- **Pause:** Esc frees the mouse and pauses, with resume, restart and main menu.
- **Target length:** about 10 minutes for a winning run. A new player's first death should come within 3–5 minutes.

## The robot

### The body

- **The core is a column of cubes, 1 wide, 1 deep and 4 tall.** Cubes are half a meter across, so the grid is fine enough for detailed builds: the body is 2 meters tall, and a 2-cube Leg is 1 meter long. Every part builds out from the body, directly or through connectors.
- **WASD steers, and parts push.** Every shot kicks the robot away from where the gun fires. A Jet pushes away from the side it's mounted on, so one on the back shoves you forward and one underneath lifts you. Momentum carries, so where you mount parts changes how you move.
- **Walking needs Legs or Treads, and they have to reach the ground.** A Leg has to point down with its foot on the robot's bottom row. Treads have to lie flat on the bottom row. Anywhere else they do nothing, and the build screen marks them with a yellow X. One Leg limp-hops, so WASD moves you in little hops. Two Legs run fast, and each extra Leg makes you faster. With no working Legs or Treads, the body can only shuffle very slowly, so you're never completely stuck.
- **Only Springs jump, and a Spring pushes off whatever its pad touches.** A Spring underneath jumps off the floor. A Spring on the side, pressed against a wall, kicks you off the wall. During a wall-run, every Spring kicks you off the wall.
- **Movement is fast, in the spirit of Titanfall and Ultrakill.** With Legs you run at about 16 m/s, slide (Ctrl) for a burst of speed that fades slowly, and wall-run by moving into a wall in mid-air, for about 1.6 seconds per wall. You can't grab the same wall again until you land. Jump just as you land and you keep your speed. The view widens and speed lines appear as you go faster.
- **The bare body has nothing:** no hop and no weapon. Every robot starts with a few parts (see Run structure).
- **Body slams hurt.** Crashing into an enemy damages it, and the faster you're going, the more it hurts.
- **It never tumbles.** It only turns left and right, and it always faces where the camera looks.
- **Later:** features that change the body's shape come after the jam essentials.

### Building: 3D Tetris

**Every part is a 3D shape made of 1–4 cubes,** on the same grid as the body.

- **Each part always has the same shape,** so players learn them.
- **A part sticks as long as one of its faces touches something that can hold it.** The body and connectors hold anything. Mods hold other mods, and the head holds vision mods (see Mods). Nothing else holds anything, so a gun can't sit on a Leg.
- **Connectors are the robot's skeleton.** They're plain blocks that attach to the body or to each other. Build arms, towers and frames out of them, then hang parts on them.
- **You can turn a part any way you like.**
- **Every part has to connect back to the body.** Nothing floats.
- **The build zone is big,** much bigger than the body, so there's room for long arms and towers. You still can't build past it, and its exact size gets tuned in testing. There's no energy or weight cost.
- **The body can move too.** In the build screen you can pick up the body itself and move or turn it, as long as every part still connects to it.
- **Parts have to make sense.** Most parts work wherever they are, as long as their working end is clear (below), so a gun on your backside is fine and goofy robots are part of the fun. Parts that move you by touching something need that thing: Legs and Treads need the ground (above), and a Spring needs a surface under its pad.
- **Nothing gets stuck in the floor.** If a new part pokes out below the robot's feet, the whole robot lifts up to stand on it.
- **Barrels must be clear.** Anything with a working end only works if the cell right in front of that end is empty: a gun's barrel, a Jet's nozzle, the Ram's tip, the Grapple's hook. You can still place a part with its barrel blocked, but the build screen marks it with a yellow X. If the part blocking it gets knocked off in a fight, the blocked part starts working.

### Buttons

- **You choose which button fires each part.** Everything on the same button fires together.
- **There are four buttons:** left mouse, right mouse, Space and Shift.
- **New parts get a default button,** so new players never have to set anything up: guns go on left mouse, Springs on Space, Jets and Rams on Shift, and everything else on right mouse. You can change it on the build screen.
- **Parts that work all the time,** like armor, mods and Legs, don't need a button.

### The head

- **The head is a part.** Every robot has exactly one from the start, and it's never offered as a pick. It's 1 cube and can go anywhere on the body or a connector.
- **The camera stays behind the robot** wherever the head is.
- **If the head gets knocked off, the HUD breaks loose.** The health bar, crosshair and arrows bounce all over the screen like loose parts until you snap the head back on, and then they fly back into place. You can still see the whole fight, and your guns still aim at the center of the screen as usual; it's just very distracting. The head beeps and blinks so you can find it. Losing it doesn't kill you.

### Mods

**Mods boost the parts they touch, and mods chained together boost more.** The idea comes from Mothergunship's caps, but here a mod only affects what it touches. Touching means sharing a full face, not just an edge or a corner.

- **Three families: movement, offense and vision.** Each boosts its own parts. Movement mods boost Legs, Treads, Springs, Jets, the Ram and the Grapple. Offense mods boost the weapons. Vision mods boost the head.
- **Hybrids take two families.** The Ram takes movement and offense mods: it lunges further and hits harder. The Rocket Launcher takes offense and vision mods: vision mods help its rockets find targets.
- **Each family has several types, and every type makes its own chains.** Offense, for example, has Damage, Fire Rate, Reload Speed and Recoil. Only mods of the same type link up, so a Damage mod next to a Fire Rate mod is two separate chains.
- **A chain boosts every part of its family that touches any mod in it.** The longer the chain, the bigger the boost, with each extra mod adding a bit less, up to a ceiling. A part touching two chains of the same type only gets the stronger one, so chaining is how a boost grows.
- **Mods hold other mods, of any type, but no other parts.** They attach to the body, a connector or another mod, and vision mods can also sit on the head. So a mod chain can snake out on its own, and the parts next to it get the boost without being attached to it.
- **Knock a mod out of the middle of a chain,** and the chain splits into two weaker ones. Anything held on only by that mod falls off with it.
- **The build screen** lights up a mod's chain and the parts it boosts, and labels each part with what it gets.

### Damage

- **The robot has one health bar,** and it's the only health shown on screen. A hit on the body lowers it, and when it's empty the run ends. It's big enough to take a lot of hits, because a lot is going on at once.
- **A hit on a part knocks it off.** Every part breaks off at the first hit; only armor will take more. Outer parts shield the ones inside and the body, so fitting parts together is also how you armor up.
- **Parts that take more than one hit show their damage on themselves, not with numbers:**
  - every hit makes the part flash white and jolt
  - worn down, it turns orange and rattles as you move
  - close to breaking, it glows red, shakes hard, sparks and smokes
  - when it breaks off: a clang, sparks and a short screen shake
- **A part that snaps back on** comes back at full health, and every part is repaired between rooms.
- **Anything no longer connected to the body falls off too.** Shoot away the part holding up an arm and the whole arm drops off with it, in one piece, so it all snaps back on together. Vision mods held on only by the head fly off with it.
- **Knocked-off parts** land nearby as physics objects. A chunk that falls off stays in one piece. Touch it to snap it back into its old spot. Parts stay until you leave the room, and the grapple can catch them in mid-air.
- **After a hit on the body** you get 0.3 seconds where the body can't be hit again. Parts take every hit.
- **No self-damage** from your own explosions.

### Forgiving hitboxes

A lot is going on at once, so the game always leans in the player's favor.

- **Your hits:** shots, slams and Ram tips count if they pass close to an enemy. Enemies' hitboxes are bigger than they look.
- **Guns that don't face forward** aim themselves at the nearest enemy in a wide cone in the direction they point, since you can't aim them yourself.
- **Grapple:** it hooks the nearest surface in a wide cone around the crosshair, and a marker shows where before you fire.
- **Getting parts back:** knocked-off parts snap back on as soon as you get within 5 m. You don't have to touch them. You can take a part back 1 second after it falls off, and you have 3 seconds before Skitters can grab it.
- **Keeping track:** arrows at the edge of the screen point to your knocked-off parts.

## Parts list (draft)

Shapes are counted in cubes. Every part has an upside and a quirk.

**Connectors** (no button). Parts attach only to the body and to connectors. You get a few connectors each room, on top of your part pick:

| Part | Shape | What it does |
|---|---|---|
| **Connector** | 1 | A plain block that holds parts and other connectors |
| **Connector Bar** | Line of 3 | The same, for long arms and towers |
| **Connector Elbow** | L of 3 | The same, for turning corners |

**Weapons** (settled). Every weapon except the Buzz Saw starts on left mouse, and you can change its button on the build screen. Shots per reload are starting numbers to tune.

| Part | Shape | Shots per reload | What it does | Quirk |
|---|---|---|---|---|
| **Scatter Blaster** | L of 3 | 2 | Shotgun blast with a big kick | Short range |
| **Rocket Launcher** | Line of 3 | 3 | Rockets with splash damage. They always fly forward, wherever the launcher is mounted | Long reload |
| **Rocket Fist** | L of 4 | 1 | A huge punch at range that knocks enemies back | Fires your actual fist. Reloading calls it back, and it smacks anything on the way |
| **Rivet Gun** | Line of 2 | 30 | Rapid fire while you hold it, long range, small kick. Good against Spitters up on the walls | Low damage per shot |
| **Welder** | Line of 2 | 3 seconds of flame | Hold for a short cone of flame that burns through packs of Skitters | Very short range |
| **Buzz Saw** | Square of 4 | Never reloads | Always spinning, and hurts anything it touches. No button. Pairs with body slams and the Ram | Only hurts what it actually touches |
| **Zapper** | 1 | 5 | Lightning that jumps to the nearest enemy, then on to 2 more. Aims itself | Weak, short range |
| **Bomb Bay** | 1 | 3 | Drops bombs the way it points. Pointing down while you hover with a Jet, it's a bombing run | Bombs take a second to go off |

Rules for all weapons:

- **A weapon fires the way it points.** Facing forward, it aims at the crosshair. Pointing anywhere else, it aims itself at enemies in that direction. Rockets are the exception: they always fly forward.
- **Every shot kicks the robot the other way,** and bigger guns kick harder, so guns double as movement. A Scatter Blaster on your back fires whenever you shoot and shoves you forward. Move it to Shift and it becomes a boost button.
- **Parts on the same button fire together.** More guns means more damage and more kick.
- **No ammo pickups, but guns reload.** A gun fires its shots, then can't fire again until it reloads. Empty guns reload by themselves, and the reload key (R) reloads every gun at once. Reloading everything early is a choice: all your guns go quiet at the same time. Reloading shows on the gun itself, not on the HUD.

**Movement** (settled; all need a button except Legs and Treads):

| Part | Shape | What it does | Quirk |
|---|---|---|---|
| **Leg** | Line of 2 | Running. One Leg limp-hops, two run fast, and each extra Leg makes you faster. Legs let you slide and wall-run | Has to point down with its foot on the bottom row, or it does nothing |
| **Spring** | 1 | Jump. The only part that jumps. It pushes off whatever its pad touches: underneath it jumps, on the side against a wall it wall-jumps. More Springs on one button jump higher | Does nothing with no surface to push off |
| **Jet** | 1 | Tap it to dash the way it pushes. Hold it to keep pushing away from the side it's on: on the back it shoves you forward, underneath it lifts you. More Jets push harder | Fuel runs out in the air and drops you. It refills when you touch the ground |
| **Ram** | Line of 3 | A burst lunge toward wherever it points, and its tip damages whatever it hits. Pointing down in mid-air, it's a butt slam whose landing knocks enemies away. Pointing up, it's a rocket jump | You can't steer during the lunge |
| **Grapple** | Line of 2 | Hooks onto any surface near the crosshair and pulls you there. Catches falling parts | Does no damage |
| **Treads** | Line of 2 | Drives on its own, faster than Legs, and you can't be knocked back, even by your own guns. Legs added on top make you faster still | Has to lie flat on the bottom row. Slow to get going, and you skid when you stop |

Rules for all movement parts:

- **More of the same part on one button means a stronger version,** with each extra one adding a bit less. Ceilings stop silly stacks from launching you into space.
- **Everything resets when you land.** Jet fuel refills, and the Ram gets one lunge per jump.
- **Parts on the same button combine.** Springs and a Jet underneath on one button: you jump, then hover while you hold the button.

**Later:** Balloon (you fall slowly, and 4 or more float you upward) and Magnet (cling to walls and jump off them).

**Mods** (1 cube each, no button). A longer chain makes every effect stronger.

| Mod | Family | What a chain does |
|---|---|---|
| **Power** | Movement | Legs and Treads run faster, Springs jump higher, Jets push harder, the Ram lunges further, the Grapple pulls harder |
| **Recharge** | Movement | Jets hold more fuel and dash again sooner, and the Ram lunges again sooner |
| **Damage** | Offense | Weapons and the Ram hit harder |
| **Fire Rate** | Offense | Weapons fire faster, and the Buzz Saw spins faster |
| **Reload Speed** | Offense | Weapons reload faster |
| **Recoil** | Offense | Weapons kick harder, for more recoil movement |

**Vision mods** (1 cube each, no button). They boost the head, and the Rocket Launcher:

| Mod | On the head | On a Rocket Launcher |
|---|---|---|
| **Wide Lens** | A wider view | Nothing yet |
| **Thermal Eye** | Marks enemies through walls; a longer chain sees further | Its rockets home in on enemies |
| **Scope** *(not built yet)* | Zooms in | Longer range |
| **Radar** *(not built yet)* | A minimap of nearby enemies; a longer chain covers more | Nothing yet |
| **Rear-view Mirror** *(not built yet)* | A small picture of what's behind you | Nothing yet |
| **Scanner** *(not built yet)* | Shows whether a rescued crewmate is an impostor | Nothing yet |
| **Flashlight** *(later)* | Lights up dark rooms, where you'd otherwise see only what's near you | Nothing yet |

**Armor** (no button):

| Part | Shape | What it does | Quirk |
|---|---|---|---|
| **Armor Plate** | Square of 4 | Much tougher than any other part | Takes a lot of the build zone |

Combos to make sure work:

- **A Fire Rate mod between two Scatter Blasters:** both fire faster.
- **A head ringed with vision mods:** you see everything, and they shield the head. Lose one and you lose that view.
- **A Ram between a Power chain and a Damage chain:** it lunges further and hits harder.
- **A long Recoil chain snaking past every gun:** every shot shoves you hard, so your guns become your engine.
- **Several Springs on one button:** one enormous jump.
- **Springs and a Jet underneath with a Recharge mod, all on one button:** a high jump, then a long hover.
- **Treads with forward guns:** the Treads cancel the guns' kick, so you become a steady turret.
- **A Ram pointing down with Springs:** jump high, then butt slam a crowd.

## Enemies

- **Every attack hits whatever part of the robot is closest.** That part takes the damage, so the parts on the outside of your build shield the rest.
- **Skitters steal parts.** A knocked-off part left lying for 3 seconds gets grabbed by the nearest Skitter, which runs off with it at about half speed (11 m/s), so you can catch it. Kill the Skitter and the part drops where it is, and you get another 3 seconds to take it back. They never take the head.
- **Tougher enemies show up later in a run.** "From room" is the first room each one can appear in.
- **Enemies are big,** bigger than the robot, and their shots are fast and frequent, aimed where you're heading. Standing still gets you stripped in seconds.

| Enemy | Behavior | How to beat it | From room |
|---|---|---|---|
| **Skitter** | Fast (20 m/s), in packs of 4–8. Leaps up to 12 m at you, onto ledges too, and steals knocked-off parts | Splash damage, body slams and constant movement | 1 |
| **Spitter** | Clings to ledges, walls and ceilings and lobs a glob every 1.2 seconds, aimed where you're heading, from up to 70 m. Its mouth glows first. Hops to a new perch every so often | Get up high: jumps, grapple or hover. The Rivet Gun reaches it | 1 |
| **Brute** | About 4.5 m tall, armored at the front and slow to turn. Fires heavy shots the way it faces, its cannon glowing first. Its front plate stops 80% of the damage; its back takes 50% extra | Circle round it and hit its back | 3 |
| **Diver** | Flies. Circles high, then makes strafing runs past you, hosing you with fast shots. Every third run is a dive-bomb instead, with a short hover first. A dive that misses leaves it stunned | Dodge the dive, then hit it while it's down. Guns on top of the robot aim themselves up at it | 3 |
| **Stalker** | A fast loner that pounces up to 20 m, between levels too. It crouches first, and a miss leaves it stuck for a moment | Dodge the pounce and hit it while it's stuck | 4 |
| **Hornet** | The hardest. Flies 20–34 m away and never stops moving, darting around you, and fires fast bursts of three aimed where you're heading | Rockets with a Thermal Eye, guns that aim themselves, or the Grapple to get close | 5 |

## Rooms

**Every room is hand-made in the room editor** (below) and saved as a file. A run picks its rooms from that pool. Procedural level generation is cut, so the variety comes from what changes on each visit:

- **Order:** small rooms first and big ones later, in random order within each size.
- **Mirroring:** each time a room comes up, there's a 50% chance it's flipped left to right.
- **"Sometimes" pieces:** a crate, ledge or bit of cover can be marked with a chance to appear (25%, 50% or 75%), rolled each time the room loads. Spelunky gets much of its variety this way.
- **Waves:** the enemies are never part of the room. The run picks them each time (below), so the same room is a different fight.
- **Rooms don't join up.** Leaving through the exit fades to the workshop, and the workshop's exit fades to the next room.

### Waves

- **Each room has 3 waves, and 4 from room 3 on.** Each wave is bought from a budget that grows with every room (10 points, plus 5 per room and 3 per wave). Prices: a Skitter 1 (they come in packs), a Spitter 3, a Diver 4, a Stalker 5, a Hornet 7 and a Brute 8. A room that's already crowded gets a smaller wave.
- **Waves run on a timer, 20 seconds each,** so they overlap. When it runs out, the next wave comes whether or not you've finished the last one, so slow play piles enemies up. Clear a wave early and the next one comes early. The HUD shows the time left until the next wave.
- **The exit unlocks** when the last wave is dead.
- **Spawning:** enemies come out of spawn spots placed in the editor, picked at random from the spots within 60 m of you (or the three nearest, if none are that close), including right in front of you. Skitters pour out of vents, Brutes and Stalkers come through ground spawn spots, Spitters and Stalkers land on perch spots, and Divers and Hornets fly in from air spawn spots. A short spawn effect plays first, so nothing pops out of nowhere.

### Room shape

The robot is fast: Legs run at 16–26 m/s, and dashes, grapple pulls and Ram lunges reach 40 m/s. Rooms are built for that, the way Titanfall 2's maps are: lanes are highways to move down, not sightlines to hold, and the open floor is the dangerous place to be. Starting numbers, to tune in playtests:

| Size | Floor | Ceiling |
|---|---|---|
| Small | 80 × 80 m | 32 m |
| Medium | 110 × 110 m | 40 m |
| Large | 140 × 140 m, or a long 160 × 90 m hall | 48 m (40 m for the hall) |

- **Closed in.** Every room has a solid ceiling, so nothing flies out, with beams under it to grapple. The ceiling casts no shadow, so the sun still lights the room.
- **Three layers:** the floor, which is exposed, with sparse, small cover; rooftops, catwalks and bridges around 6–15 m; and tower tops and beams near the ceiling.
- **Several arenas sharing one space.** Each room has a few pockets, each with something in the middle to fight around (a tower, a reactor, a cluster of pillars), joined by wide lanes with tall walls to run along.
- **Loops, not dead ends.** There's always another way round.
- **Getting up is easy everywhere:** ramps onto low roofs, jump pads by tall ones, and launch pads down the lanes. Getting down is just a drop.
- **No bottomless pits.** Falling off a ledge just drops you to the floor below.

**The starter rooms** come in five layouts:

- **City block:** buildings on a grid of streets, with bridges between rooftops.
- **Boneyard:** open ground full of towers of every height.
- **Terraces:** the room climbs in tiers toward the exit.
- **Hall:** a long hangar with galleries down both sides and bridges across.
- **Reactor ring:** a loop around a core that reaches the ceiling.

### The room editor

A tool for the team, not for players. It runs in Unity's Play mode and saves rooms straight into the project.

- **It works like the build screen:** the pieces sit in folders, you drag them in, click to select, and use the same keys to nudge and turn. Pieces that overlap turn red, and there's a bin and undo.
- **A 1 m grid,** coarser than the robot's half-meter cells.
- **A fly camera like Unity's Scene view:** hold the right mouse button, then WASD to fly and Q/E to go down and up.
- **The folders:**
  - **Blocks:** floors, walls, pillars and ceilings. Click one, then drag across any surface to draw a rectangle of it. A selected block grows with Shift and a move key and shrinks with Alt and a move key.
  - **Ramps.**
  - **Pads:** the jump pad throws the robot straight up, about 18 m. The launch pad throws it up and away the way it faces, about 30 m across.
  - **Props:** crates, cover and lamps.
  - **Markers:** the entry, the exit, Skitter vents, ground spawn spots, perch spots, air spawn spots, the rescue door spot, workshop shop stands and the escape pod.
- **Selecting:** click a piece, and Shift-click to add more. Dragging a selection slides it along the floor, and R/F move it up and down.
- **Copy and paste** a selection with Ctrl+C and Ctrl+V. Paste lands on the surface under the mouse.
- **"Sometimes":** the details panel sets a piece's chance to appear. Pieces below 100% show see-through.
- **Room settings:** a name, a size (small, medium or large) and a type (combat, workshop or escape shaft).
- **Test:** drops the robot at the entry and starts the waves. Tab goes back to editing.
- **Saving warns, but never blocks:** a room with no entry, no exit or too few spawn spots still saves, with a warning.

### Rooms (technical)

- **Where things live:** the editor is its own scene, Assets/Scenes/RoomEditor.unity, rebuilt from the menu Core Dump > Build Room Editor. Rooms save to Assets/Resources/Rooms, one file each.
- **A room file** is a list of pieces: what it is, its position and size on the 1 m grid, which way it faces, and its chance to appear. Room files ship inside the game, which works in web builds.
- **Loading a room** builds every piece, gives each block one box collider, and merges the visuals for fast drawing. Blocks use materials that tile by world position, so a long wall never stretches its texture.
- **Mirroring** flips the room's data as it loads, never the objects' scale: negative scale breaks box colliders and the merging. Props should look the same mirrored.
- **The game's scene** is Assets/Scenes/Game.unity (Core Dump > Build Game), the first scene in a build.
- **Enemy pathfinding:** Unity's built-in navigation bakes a walking map when the room loads, behind the fade. Skitters, Stalkers and Brutes walk on it, and Skitters and Stalkers leap onto ledges off it. Spitters hop between perch spots, and flyers fly, staying inside the walls and under the ceiling.
- **Pads** sit on the Ignore Raycast layer, so they never block a shot, the grapple or the walking map.
- **Lighting:** rooms are built at load time, so they can't use baked lighting. Each room gets a few real-time lamps.

## Crew and rescues

- **Rescue doors** show how hard the challenge is (1–3 skulls) but not who's inside. A one-line voice bark through the door gives a hint ("Do you have ANY idea who I am?").
- **The challenge** is an extra, harder wave in that room. Beat it and the door opens.
- **Rescued crew come with you** from room to room. In each new room they wait at the entry until it's cleared, then follow you out. They're never in combat and never in the way, so they need no pathfinding.

| Crewmate | Chance | Effect |
|---|---|---|
| **Mechanic** | 1 in 5 | Restores one lost part between rooms |
| **Quartermaster** | 1 in 5 | One more stand in the workshop |
| **Scout** | 1 in 5 | Shows who's behind the next rescue door, impostors included |
| **VIP** | 1 in 5 | No upside. Makes the other crew nervous, so their perks only work every other room. Can't be abandoned |
| **Impostor** | 1 in 5 | An alien in disguise (see below) |

Three in five rescues are helpful, and a sharp player can turn an impostor into a reward, so rescuing pays off on average.

### The impostor

- **It looks like a normal Mechanic, Quartermaster or Scout.**
- **Its tells:** each impostor shows at least two of these:
  - a misspelled name tag ("Mechanik")
  - a voice bark that glitches
  - eyes that flicker green for a moment
  - a perk that never quite works
- **The reveal:** 1–2 rooms after you rescue it, it bursts into a combat room, rips off one of your parts and runs for a vent. Kill it within about 8 seconds to get the part back, or it's gone for good.
- **Spot it first:** you can shoot a crewmate you suspect. If it was an impostor, it drops a free part. If you were wrong, you just killed a real crewmate.
- **Your first two rescues are scripted.** The first is always a real crewmate, so players see why rescues are worth it. The second is always an impostor, which delivers the "Wait. What?" moment while a judge is still playing. After that, it's random.

## Controls

Mouse and keyboard first. Gamepad if there's time.

| Action | Input |
|---|---|
| Move | WASD |
| Slide | Ctrl |
| Aim and camera | Mouse |
| Fire parts | Left mouse, right mouse, Space and Shift. You choose which parts each one fires |
| Reload every gun | R |
| Interact, open rescue doors | E |
| Build (in the workshop only) | Tab |
| Pause | Esc |

**Build screen:**

- **Inventory:** the parts you own but haven't fitted, sorted into folders (Structure, Movement, Weapons, and movement, offense and vision mods), each with a count. Drag a part from it onto the robot. It snaps to the surface under the mouse, pointing out of it.
- **The only rules:** a part can't overlap anything, and it has to attach to something that can hold it (see Building). A part that breaks a rule doesn't vanish. It stays where you dropped it as a red hologram that you can keep moving and turning, and nothing else can be selected until it fits or goes away. A part counts as placed as soon as it fits. A part that fits but can't work there (a blocked barrel, a Leg off the ground) still goes on, with a yellow warning.
- **Selecting and adjusting:** click a part, or the body, to select it. Anything that would fall off without the selected part comes along, like everything hanging off a connector. W A S D and R/F move it one cell at a time, Q/E spin it, T/G tip it and Z/C roll it, all relative to the camera. Drag it to move it further.
- **Taking parts off:** drag a part onto the inventory to keep it, or onto the bin to throw it away (or press I or Delete). The head and the body never leave the robot.
- **Undo and letting go:** Esc lets go of the selection, and puts a red hologram back where it last fit. Ctrl+Z undoes.
- **Camera:** right-drag orbits around the robot and the scroll wheel zooms.
- **Seeing what's going on:** a panel shows the selected part, what it does, whether it works, its button (click to change it) and how to take it off. Every part shows a tag with its button, parts that won't work show a yellow X, and hovering over a part tells you what it is and what's wrong with it.

## Camera and 3D

- **Camera:** third person over the shoulder, built with Cinemachine. The robot sits to one side of the screen so it doesn't cover the crosshair.
- **It zooms out as the robot grows,** pulling back and up to keep the whole build in view.
- **Landing:** a simple shadow directly under the robot so players can judge where they'll land.
- **Build screen in 3D.** You place parts directly on the 3D robot, so it keeps working when the body changes shape later.
- **Rooms:** built in the room editor from blocks of any size, ramps and props on a 1 m grid (see Rooms). Every room needs height: ledges, catwalks and high walls to grapple onto.
- **Fallback:** if moving around still isn't fun by the 20% mark, switch to 2.5D (3D art, movement locked to a side-on plane).

### Attaching parts (technical)

- **Everything is whole-number grid coordinates in the robot's own space.** The body is a set of cube positions. A part is a list of cube offsets plus one of 24 possible orientations.
- **A placement is valid** when every cube of the part is inside the build zone and empty, and the part is held on (see Holding below).
- **The build screen** casts a ray from the mouse to the robot. The surface it hits gives the empty cell next to it, and the part's first cube goes there. A see-through preview shows green, yellow or red. A selection that doesn't fit is kept off the robot as a hologram, and goes on the moment it fits.
- **After a part breaks off,** work out again which parts are held on, starting from the body. Anything no longer held falls off, as one chunk.
- **Cell size:** the grid works in whole cells, and one cell is half a meter in the game. Each part's object is scaled down by the cell size, so inside a part, 1 unit is 1 cell.
- **Part damage display:** each part's material has one glow value that runs from none through orange to red, and its model (not its grid cell) shakes harder as its health drops.
- **Mod chains** are rebuilt after every change: same-type mods that touch are linked, then each chain finds the parts of its family touching it. Each part keeps only its strongest chain of each type.
- **Holding:** each part type says what it can hold (the body and connectors hold anything, mods hold mods, the head holds vision mods). A part is held on if it touches the body, or a held part that can hold it.
- **Barrels:** each part marks its working-end cube and the direction it faces. After every change, including a part breaking off, any part with a filled cell in front of its working end is switched off.
- **Buttons:** each part stores its button, and one Input System action per button fires every part assigned to it.
- **Movement** uses a physics body with its rotation locked, so it never tumbles. WASD steers it, parts add pushes, and momentum carries. Knocked-off parts are physics objects too. Every mounted cube has a frictionless collider, so the robot stands on its Legs and slides along walls instead of snagging on them.
- **Rule for the artist:** build each part from 1-unit cubes on the grid (the game shrinks them to half a meter), and put the pivot at the center of the part's first cube. Make the working end (barrel, nozzle, hook, tip or lens) obvious at a glance.

## Story

TBD

## Jam scope

- **Must have** (a complete, submittable game):
  - the block body, building with 3D pieces, the head, buttons and the build screen
  - 10 parts: Scatter Blaster, Rocket Launcher, Rivet Gun, Leg, Spring, Jet, Ram, Grapple, Armor Plate, and the Power, Damage and Fire Rate mods
  - 2 vision mods: Scope and Radar
  - knocking parts off and snapping them back on
  - Skitter and Spitter, with waves on a timer (built, along with the Brute, Diver, Stalker and Hornet)
  - the room editor, and 6 combat rooms made with it, plus the workshop and the escape shaft (8 are built)
  - the main menu
  - rescues with the Mechanic, VIP and Impostor
  - the end screen
- **Should have:** Rocket Fist, Buzz Saw, Welder, Treads, the Recharge, Reload Speed and Recoil mods, Rear-view Mirror, Scanner, the Quartermaster and Scout, a pool of 10 rooms, best time.
- **Stretch goals:** Zapper, Bomb Bay, Balloon, Magnet, Thermal Eye, Wide Lens, dark rooms with the Flashlight, changing the body's shape, more rooms, more parts, a boss, gamepad support, an online leaderboard.
- **Cut:** procedural levels, player-made levels, cutscenes, unlocks that carry between runs, voice acting, energy costs.

## Build order (share of jam time)

| When | Build | Done when |
|---|---|---|
| 0–20% | Plain test room: movement, camera, snapping cube parts onto the robot, the head, and one gun and one movement part on buttons | Building a goofy robot and running around with it is fun on its own |
| 20–40% | Build screen, knocking parts off and snapping them back, the other must-have parts, mods (vision mods too), the room editor, Skitter and Spitter, waves, one real room | A full fight in one room works |
| 40–60% | Run flow: picking rooms (small first, mirrored, sometimes pieces), rescues, crew following you, Mechanic, VIP and Impostor | A whole run from start bay to escape shaft is playable with placeholder art |
| 60–80% | Escape shaft, end screen, art pass, audio, should-have items | Someone outside the team can finish a run |
| 80–100% | Balancing, playtests, builds, submission page, GIFs | Submitted with time to spare |

**Feature freeze at 80%.** After that, only fixes, balance and polish.
