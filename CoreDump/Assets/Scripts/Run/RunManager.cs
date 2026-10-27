using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoreDump
{
    // A whole run, from the main menu to the escape pod.
    // - The run starts in the workshop (the maintenance bay), then goes through the combat rooms
    //   with the workshop between each, and ends with the climb up the escape shaft.
    // - Rooms come from Resources/Rooms. A run picks its combat rooms from the pool, small ones
    //   first, flips some of them, and rolls their "sometimes" pieces.
    // - The workshop's stands hold free parts: walk into one to take it. It's the only place Tab
    //   opens building, and it repairs every part and some of the hull.
    // - A combat room's exit opens once its waves are dead. In the shaft, the self-destruct rises
    //   from below; reach the pod at the top to win.
    // - Esc pauses. Dying shows a stats card; Space goes straight into a new run.
    // - The main menu also starts the tutorial (Tutorial.cs).
    [DefaultExecutionOrder(100)]
    public partial class RunManager : MonoBehaviour
    {
        public RoomKit kit;
        public EnemyKit enemies;
        public RobotBody robot;
        public AimRig aim;
        [Tooltip("Circles the workshop behind the main menu. Positioned by this script.")]
        public CinemachineCamera menuCamera;
        [Tooltip("What the workshop's stands can offer. Each visit puts a different handful on them.")]
        public PartDefinition[] shopParts;
        [Tooltip("Handed over on every visit to the workshop, on top of what's on the stands.")]
        public PartDefinition[] workshopGift;
        public Material standMaterial;
        [Tooltip("Unlit; tinted per use: the exit's door, the pod's window.")]
        public Material glowMaterial;
        public Material hazardMaterial;
        public Material podMaterial;
        public int combatRooms = 6;
        [Tooltip("Seconds each wave gets before the next one comes in anyway.")]
        public float waveTime = 20f;
        [Tooltip("Hull the workshop repairs on each visit. Parts are always repaired in full.")]
        public int workshopHeal = 3;
        [Tooltip("How fast the self-destruct rises up the shaft, in meters per second, after a short delay.")]
        public float hazardSpeed = 0.9f;
        public float hazardDelay = 5f;

        // Trained: the tutorial is finished.
        enum State { Menu, Workshop, Room, Shaft, Won, Dead, Tutorial, Trained }

        const string BestTimeKey = "CoreDump.BestTime";
        const float FadeTime = 0.3f;
        static readonly Color Orange = new(1f, 0.6f, 0.15f);

        State state;
        bool paused, howToPlay, transitioning, newBest;
        // Where the robot got to before it was destroyed, for the stats card.
        string deathPlace;
        float fade;
        List<RoomData> combatPool = new();
        RoomData workshopRoom, shaftRoom;
        // This run's combat rooms, in order, and which one is next.
        readonly List<RoomData> plan = new();
        int roomIndex;
        float runTime, bestTime;

        // What's built right now: the room, its data (flipped, if it was), and what's in it.
        Transform area;
        RoomData current;
        Vector3 areaCenter;
        float areaFloor;
        ExitDoor exit;
        readonly List<ShopStand> stands = new();
        Waves waves;
        Transform hazard;
        float hazardTop, hazardStart, nextBurn;
        Vector3 podPoint;

        readonly System.Random random = new();
        BuildMode build;
        PartInventory inventory;
        RobotHealth health;
        StartingLoadout loadout;
        float menuAngle;
        string message;
        float messageUntil;
        GUIStyle titleStyle, headingStyle, textStyle, hudStyle, labelStyle;

        bool InPlay => state is State.Workshop or State.Room or State.Shaft or State.Tutorial;
        Vector3 RobotFeet => robot.transform.position + Vector3.up * robot.LocalBounds.min.y;

        void Start()
        {
            build = robot.GetComponent<BuildMode>();
            inventory = robot.GetComponent<PartInventory>();
            health = robot.GetComponent<RobotHealth>();
            loadout = robot.GetComponent<StartingLoadout>();
            health.Died += OnDied;
            JumpPad.Launched += OnPadLaunch;
            bestTime = PlayerPrefs.GetFloat(BestTimeKey, 0f);
            LoadRooms();
            BuildLessons();
            ShowMenu();
        }

        void OnDestroy() => JumpPad.Launched -= OnPadLaunch;

        void LoadRooms()
        {
            var rooms = Resources.LoadAll<TextAsset>("Rooms").Select(t => RoomData.FromJson(t.text)).Where(r => r != null).ToList();
            combatPool = rooms.Where(r => r.type == RoomType.Combat && r.All(RoomPieces.Entry).Any()).ToList();
            workshopRoom = rooms.FirstOrDefault(r => r.type == RoomType.Workshop) ?? PlainRoom("Workshop", RoomType.Workshop);
            shaftRoom = rooms.FirstOrDefault(r => r.type == RoomType.Shaft);
            tutorialRooms = rooms.Where(r => r.type == RoomType.Tutorial && r.All(RoomPieces.Entry).Any()).OrderBy(r => r.name).ToList();
        }

        // A bare floor with an entry, an exit and six stands, if no workshop has been made.
        static RoomData PlainRoom(string name, RoomType type)
        {
            var room = new RoomData { name = name, type = type };
            room.pieces.Add(new RoomPiece { kind = "floor", min = new Vector3Int(-10, -1, -10), size = new Vector3Int(20, 1, 20) });
            room.pieces.Add(new RoomPiece { kind = RoomPieces.Entry, min = new Vector3Int(0, 0, -8) });
            room.pieces.Add(new RoomPiece { kind = RoomPieces.Exit, min = new Vector3Int(0, 0, 9), turn = 2 });
            for (int i = 0; i < 6; i++) room.pieces.Add(new RoomPiece { kind = RoomPieces.Shop, min = new Vector3Int(-7 + i * 3, 0, 2) });
            return room;
        }

        // ---- Moving through the run ----

        void ShowMenu()
        {
            state = State.Menu;
            paused = howToPlay = false;
            Time.timeScale = 1f;
            build.allowed = false;
            aim.Suspended = true;
            loadout.Respawn();
            LoadArea(workshopRoom, false);
            exit?.SetOpen(true);
            PlaceRobot();
            if (menuCamera) menuCamera.Priority = 40;
            Juice.PlayMusic(Tune.Calm);
        }

        void StartRun()
        {
            if (combatPool.Count == 0)
            {
                Say("There are no combat rooms in Assets/Resources/Rooms yet");
                return;
            }
            Time.timeScale = 1f;
            paused = false;
            PlanRun();
            roomIndex = 0;
            runTime = 0f;
            if (menuCamera) menuCamera.Priority = 0;
            StartCoroutine(Go(() =>
            {
                loadout.Respawn();
                EnterWorkshop();
            }));
        }

        // Picks this run's combat rooms: no repeats while the pool lasts, small rooms first, and in
        // random order within each size.
        void PlanRun()
        {
            plan.Clear();
            var picks = new List<RoomData>();
            var bag = new List<RoomData>();
            while (picks.Count < combatRooms)
            {
                if (bag.Count == 0) bag = combatPool.OrderBy(_ => random.Next()).ToList();
                picks.Add(bag[0]);
                bag.RemoveAt(0);
            }
            plan.AddRange(picks.Select((room, order) => (room, order)).OrderBy(p => p.room.size).ThenBy(p => p.order).Select(p => p.room));
        }

        void EnterWorkshop()
        {
            state = State.Workshop;
            LoadArea(workshopRoom, false);
            exit?.SetOpen(true);
            PlaceRobot();
            health.RepairParts();
            if (roomIndex > 0) health.Heal(workshopHeal);
            foreach (var part in workshopGift)
                if (part) inventory.Add(part);
            StockStands();
            build.allowed = true;
            Play();
            Juice.PlayMusic(Tune.Calm);
            Say(roomIndex == 0
                ? "The maintenance bay. Walk into a stand to take its part, Tab to build, then take the exit"
                : $"Workshop. Parts repaired. Grab what you like, build, and head out to room {roomIndex + 1}");
        }

        void StockStands()
        {
            var spots = current.All(RoomPieces.Shop).ToList();
            var offers = shopParts.Where(p => p).OrderBy(_ => random.Next()).Take(spots.Count).ToList();
            for (int i = 0; i < offers.Count; i++) stands.Add(ShopStand.Create(area, spots[i].Bottom, offers[i], robot, standMaterial));
        }

        void EnterRoom()
        {
            state = State.Room;
            build.allowed = false;
            var room = plan[roomIndex];
            LoadArea(room, random.Next(2) == 0);
            RoomNav.Bake(area);
            exit?.SetOpen(false);
            PlaceRobot();
            waves = new Waves(current, roomIndex, enemies, robot, waveTime, 2.5f, random);
            Play();
            Juice.PlayMusic(Tune.Combat);
            Say($"Room {roomIndex + 1} of {plan.Count}: {room.name}");
        }

        void EnterShaft()
        {
            state = State.Shaft;
            build.allowed = false;
            if (shaftRoom == null)
            {
                Win();
                return;
            }
            LoadArea(shaftRoom, false);
            PlaceRobot();
            var pod = current.All(RoomPieces.Pod).FirstOrDefault();
            podPoint = pod != null ? pod.Bottom : areaCenter + Vector3.up * 50f;
            BuildPod(podPoint);

            // The self-destruct: a slab filling the shaft, its top starting a few meters below the floor.
            var (min, max) = Bounds(current);
            hazardTop = areaFloor - 4f;
            hazard = RobotBody.CreateBlock(area, new Vector3((min.x + max.x) * 0.5f, hazardTop - 3f, (min.z + max.z) * 0.5f),
                new Vector3(max.x - min.x + 2f, 6f, max.z - min.z + 2f), hazardMaterial).transform;
            hazardStart = Time.time + hazardDelay;
            Play();
            Juice.PlayMusic(Tune.Alarm);
            Say("SELF-DESTRUCT! Climb to the escape pod before it reaches you");
        }

        void Win()
        {
            state = State.Won;
            newBest = bestTime <= 0f || runTime < bestTime;
            if (newBest)
            {
                bestTime = runTime;
                PlayerPrefs.SetFloat(BestTimeKey, bestTime);
                PlayerPrefs.Save();
            }
            Freeze();
            Juice.PlayMusic(Tune.None);
            Juice.Play(Sound.Victory);
        }

        void OnDied()
        {
            if (!InPlay) return;
            if (state == State.Tutorial)
            {
                RetryLesson();
                return;
            }
            deathPlace = state == State.Shaft ? "the escape shaft" : $"room {roomIndex + 1} of {plan.Count}";
            state = State.Dead;
            Freeze();
            Juice.PlayMusic(Tune.None);
            Juice.Play(Sound.Death);
        }

        void Freeze()
        {
            Time.timeScale = 0f;
            aim.Suspended = true;
            build.allowed = false;
        }

        // Hands control back to the player.
        void Play()
        {
            aim.Suspended = false;
            aim.SetLocked(true);
        }

        void Pause()
        {
            paused = true;
            Time.timeScale = 0f;
            aim.Suspended = true;
        }

        void Resume()
        {
            paused = false;
            Time.timeScale = 1f;
            Play();
        }

        void GoToMenu()
        {
            Time.timeScale = 1f;
            StartCoroutine(Go(ShowMenu));
        }

        // Fades to black, swaps what's built, and fades back in.
        IEnumerator Go(System.Action next)
        {
            if (transitioning) yield break;
            transitioning = true;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / FadeTime)
            {
                fade = t;
                yield return null;
            }
            fade = 1f;
            next();
            yield return null;
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / FadeTime)
            {
                fade = t;
                yield return null;
            }
            fade = 0f;
            transitioning = false;
        }

        // ---- What's built ----

        void LoadArea(RoomData room, bool mirror)
        {
            ClearArea();
            current = mirror ? room.Mirrored() : room;
            area = new GameObject($"Area: {room.name}").transform;
            RoomBuilder.Build(current, kit, area, random);
            if (current.pieces.Count > 0)
            {
                var (min, max) = Bounds(current);
                areaCenter = ((Vector3)min + (Vector3)max) * 0.5f;
                areaFloor = min.y;
                // Inside the outer walls, from the floor's top to the ceiling's underside.
                var arena = new Bounds();
                arena.SetMinMax((Vector3)min + Vector3.one, (Vector3)max - Vector3.one);
                Enemy.Arena = arena;
            }
            var exitMarker = current.All(RoomPieces.Exit).FirstOrDefault();
            exit = exitMarker != null ? ExitDoor.Create(area, exitMarker, kit.wall, glowMaterial) : null;
        }

        // Leaving a room: its enemies, shots and knocked-off parts go with it.
        void ClearArea()
        {
            Enemy.ClearAll();
            PartChunk.ClearAll();
            Projectile.ClearAll();
            RoomNav.Clear();
            stands.Clear();
            waves = null;
            hazard = null;
            if (area) Destroy(area.gameObject);
        }

        // Stands the robot on the entry, facing the way it points.
        void PlaceRobot()
        {
            var entry = current?.All(RoomPieces.Entry).FirstOrDefault();
            var spot = entry != null ? entry.Bottom : areaCenter;
            robot.Motor.Teleport(spot + Vector3.up * (0.05f - robot.LocalBounds.min.y));
            aim.Face(entry != null ? entry.turn * 90f : 0f);
        }

        void BuildPod(Vector3 at)
        {
            var pod = new GameObject("Escape Pod").transform;
            pod.SetParent(area, false);
            pod.position = at;
            RobotBody.CreateBlock(pod, new Vector3(0f, 1.6f, 0f), new Vector3(2.6f, 3.2f, 2.6f), podMaterial);
            RobotBody.CreateBlock(pod, new Vector3(0f, 3.5f, 0f), new Vector3(1.6f, 0.6f, 1.6f), podMaterial);
            var window = RobotBody.CreateBlock(pod, new Vector3(0f, 2f, -1.32f), new Vector3(1.6f, 1.2f, 0.1f), glowMaterial).GetComponent<Renderer>();
            var tint = new MaterialPropertyBlock();
            tint.SetColor(Shader.PropertyToID("_BaseColor"), new Color(0.4f, 0.9f, 1f));
            window.SetPropertyBlock(tint);
        }

        static (Vector3Int min, Vector3Int max) Bounds(RoomData room)
        {
            var min = room.pieces[0].min;
            var max = room.pieces[0].Max;
            foreach (var p in room.pieces)
            {
                min = Vector3Int.Min(min, p.min);
                max = Vector3Int.Max(max, p.Max);
            }
            return (min, max);
        }

        // ---- Every frame ----

        void Update()
        {
            menuAngle += Time.unscaledDeltaTime * 8f;
            if (state == State.Menu) CircleMenuCamera();
            var kb = Keyboard.current;

            if (InPlay)
            {
                // Esc frees the mouse; that pauses the run, unless building has the mouse.
                if (!transitioning && !paused && !build.IsOpen && !aim.Suspended && Cursor.lockState != CursorLockMode.Locked) Pause();
                if (kb != null && kb.tabKey.wasPressedThisFrame && !build.allowed && !paused) Say("You can only build in the workshop, between rooms");
                if (!paused && !transitioning) Track();
                runTime += Time.deltaTime;
            }
            else if (state is State.Dead or State.Won or State.Trained && kb != null && !transitioning)
            {
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) StartRun();
                else if (kb.mKey.wasPressedThisFrame) GoToMenu();
            }
        }

        void Track()
        {
            // Fell out of the room: back to the entry, a little worse for wear.
            if (robot.transform.position.y < areaFloor - 25f)
            {
                PlaceRobot();
                health.TakeHit(null, Vector3.up);
            }

            switch (state)
            {
                case State.Workshop:
                    TakeFromStands();
                    if (exit && exit.Contains(RobotFeet)) StartCoroutine(Go(roomIndex < plan.Count ? EnterRoom : EnterShaft));
                    break;

                case State.Room:
                    waves.Tick();
                    if (waves.Clear && exit && !exit.IsOpen)
                    {
                        exit.SetOpen(true);
                        Say("Room clear! Head for the EXIT");
                        Juice.Play(Sound.RoomClear);
                    }
                    bool leaving = exit ? exit.IsOpen && exit.Contains(RobotFeet) : waves.Clear;
                    if (leaving)
                    {
                        roomIndex++;
                        StartCoroutine(Go(EnterWorkshop));
                    }
                    break;

                case State.Shaft:
                    if (Time.time > hazardStart) hazardTop += hazardSpeed * Time.deltaTime;
                    hazard.position = new Vector3(hazard.position.x, hazardTop - hazard.localScale.y * 0.5f, hazard.position.z);
                    if (RobotFeet.y < hazardTop && Time.time >= nextBurn)
                    {
                        nextBurn = Time.time + 0.5f;
                        health.TakeHit(null, Vector3.up, 2);
                        robot.Motor.Kick(Vector3.up * 14f);
                    }
                    if (state == State.Shaft && Vector3.Distance(RobotFeet, podPoint) < 3.5f) Win();
                    break;

                case State.Tutorial:
                    TrackTutorial();
                    break;
            }
        }

        // Walking into a workshop stand takes its part.
        void TakeFromStands()
        {
            foreach (var stand in stands)
            {
                if (stand.Taken || !Near(stand.transform.position, 1.9f)) continue;
                var part = stand.Take();
                inventory.Add(part);
                Say($"Got a {part.displayName}. Tab to fit it");
                Juice.Play(Sound.Pickup);
                Juice.Flash(stand.LabelPoint + Vector3.down, 1.4f);
            }
        }

        bool Near(Vector3 point, float radius)
        {
            var feet = RobotFeet;
            var flat = new Vector2(point.x - feet.x, point.z - feet.z);
            return flat.magnitude < radius && Mathf.Abs(point.y - feet.y) < 2.5f;
        }

        void CircleMenuCamera()
        {
            if (!menuCamera) return;
            var focus = areaCenter + Vector3.up * 1.5f;
            var from = focus + Quaternion.Euler(0f, menuAngle, 0f) * new Vector3(0f, 6f, -15f);
            menuCamera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(focus - from));
        }

        void Say(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 4f;
        }

        static string Clock(float seconds) => $"{(int)(seconds / 60f)}:{(int)(seconds % 60f):00}";

        // ---- On screen ----

        void OnGUI()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Orange } };
            headingStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            textStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, alignment = TextAnchor.UpperCenter, normal = { textColor = new Color(0.88f, 0.88f, 0.9f) } };
            hudStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            switch (state)
            {
                case State.Menu:
                    if (howToPlay) DrawHowToPlay();
                    else DrawMenu();
                    break;
                case State.Dead:
                    DrawCard("UNIT 7 DESTROYED", $"Made it to {deathPlace}    Time {Clock(runTime)}", null);
                    break;
                case State.Won:
                    DrawCard("ESCAPED WITH THE DATA", $"Time {Clock(runTime)}    Best {Clock(bestTime)}", newBest ? "A new best time!" : null);
                    break;
                case State.Trained:
                    DrawCard("TUTORIAL COMPLETE", "You know the basics. Now get the data off the ship.", null);
                    break;
                default:
                    if (!build.IsOpen) DrawHud();
                    if (state == State.Tutorial && !paused)
                    {
                        if (build.IsOpen) DrawStepBanner();
                        else DrawLesson();
                    }
                    if (paused) DrawPause();
                    break;
            }

            if (Time.unscaledTime < messageUntil && !build.IsOpen && !paused)
            {
                float width = Mathf.Min(Screen.width - 40f, Mathf.Max(420f, GUI.skin.box.CalcSize(new GUIContent(message)).x + 30f));
                GUI.Box(new Rect(Screen.width * 0.5f - width * 0.5f, Screen.height * 0.72f, width, 28f), message);
            }
            if (fade > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, fade);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        void Dim(float alpha)
        {
            GUI.color = new Color(0.02f, 0.02f, 0.04f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        bool BigButton(float y, string text)
        {
            bool pressed = GUI.Button(new Rect(Screen.width * 0.5f - 130f, y, 260f, 44f), text);
            if (pressed) Juice.Play(Sound.Click);
            return pressed;
        }

        // Sound and music volume, saved between sessions.
        void DrawVolume(float y)
        {
            float x = Screen.width * 0.5f - 130f;
            GUI.Label(new Rect(x, y, 70f, 20f), "Sound");
            float sound = GUI.HorizontalSlider(new Rect(x + 70f, y + 5f, 190f, 20f), Juice.SoundVolume, 0f, 1f);
            if (!Mathf.Approximately(sound, Juice.SoundVolume)) Juice.SoundVolume = sound;
            GUI.Label(new Rect(x, y + 26f, 70f, 20f), "Music");
            float music = GUI.HorizontalSlider(new Rect(x + 70f, y + 31f, 190f, 20f), Juice.MusicVolume, 0f, 1f);
            if (!Mathf.Approximately(music, Juice.MusicVolume)) Juice.MusicVolume = music;
        }

        void DrawMenu()
        {
            Dim(0.45f);
            float y = Screen.height * 0.22f;
            GUI.Label(new Rect(0f, y, Screen.width, 80f), "CORE DUMP", titleStyle);
            GUI.Label(new Rect(Screen.width * 0.5f - 300f, y + 84f, 600f, 50f),
                "The ship is lost. The crew's data is in your core. Escape with it.", textStyle);
            y += 160f;
            if (BigButton(y, "Start a run")) StartRun();
            if (BigButton(y + 54f, "Tutorial")) StartTutorial();
            if (BigButton(y + 108f, "How to play")) howToPlay = true;
            if (BigButton(y + 162f, "Quit")) Application.Quit();
            if (bestTime > 0f) GUI.Label(new Rect(0f, y + 216f, Screen.width, 24f), $"Best time {Clock(bestTime)}", textStyle);
            DrawVolume(y + 250f);
        }

        void DrawHowToPlay()
        {
            Dim(0.8f);
            float width = Mathf.Min(720f, Screen.width - 40f);
            var rect = new Rect(Screen.width * 0.5f - width * 0.5f, Screen.height * 0.12f, width, Screen.height * 0.7f);
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 40f), "HOW TO PLAY", headingStyle);
            const string text =
                "New here? The Tutorial on the main menu walks you through everything.\n\n" +
                "Get your robot off the ship. Each room throws waves of aliens at you: clear them all and the EXIT opens.\n\n" +
                "Between rooms is the workshop. Its stands hold free parts: walk into one to take it. Then press Tab to build: " +
                "drag parts from your inventory onto the robot. You can only build in the workshop, and it repairs every part.\n\n" +
                "Your parts are your guns, your legs and your armor. A hit knocks a part right off; " +
                "get close to it to snap it back on, before a Skitter runs off with it.\n\n" +
                "After the last room, climb the escape shaft to the pod before the self-destruct catches you.\n\n" +
                "WASD move    Mouse aim    Ctrl slide    R reload\n" +
                "Left mouse, right mouse, Space and Shift fire the parts on each button\n" +
                "Tab build (workshop only)    Esc pause";
            GUI.Label(new Rect(rect.x, rect.y + 56f, rect.width, rect.height - 120f), text, textStyle);
            if (BigButton(rect.yMax - 40f, "Back")) howToPlay = false;
        }

        void DrawPause()
        {
            Dim(0.6f);
            float y = Screen.height * 0.3f;
            GUI.Label(new Rect(0f, y, Screen.width, 50f), "PAUSED", headingStyle);
            if (BigButton(y + 70f, "Resume")) Resume();
            if (state == State.Tutorial)
            {
                if (BigButton(y + 124f, "Skip this lesson")) SkipLesson();
            }
            else if (BigButton(y + 124f, "Restart the run")) StartRun();
            if (BigButton(y + 178f, "Main menu")) GoToMenu();
            DrawVolume(y + 240f);
        }

        void DrawCard(string title, string stats, string note)
        {
            Dim(0.65f);
            float y = Screen.height * 0.28f;
            GUI.Label(new Rect(0f, y, Screen.width, 60f), title, headingStyle);
            GUI.Label(new Rect(0f, y + 64f, Screen.width, 26f), stats, textStyle);
            if (note != null) GUI.Label(new Rect(0f, y + 92f, Screen.width, 26f), note, textStyle);
            if (BigButton(y + 130f, "New run (Space)")) StartRun();
            if (BigButton(y + 184f, "Main menu (M)")) GoToMenu();
        }

        // The run's status at the top of the screen, the time, and labels on what matters in the room.
        void DrawHud()
        {
            bool tutorial = state == State.Tutorial;
            string status = state switch
            {
                State.Workshop => $"WORKSHOP    Next: room {roomIndex + 1} of {plan.Count}    Walk into a stand to take its part    Tab to build",
                State.Room when waves == null => "",
                State.Room when waves.Clear => $"ROOM {roomIndex + 1} OF {plan.Count} CLEAR    Head for the EXIT",
                State.Room when waves.Wave == 0 => $"ROOM {roomIndex + 1} OF {plan.Count}    Get ready",
                State.Room when waves.AllIn => $"ROOM {roomIndex + 1} OF {plan.Count}    LAST WAVE    {Enemy.AliveCount} left",
                State.Room => $"ROOM {roomIndex + 1} OF {plan.Count}    WAVE {waves.Wave} OF {waves.Count}    Next wave in {Mathf.CeilToInt(waves.TimeToNext)}    {Enemy.AliveCount} aliens",
                State.Shaft => $"SELF-DESTRUCT    Climb to the pod    It's {Mathf.Max(0f, RobotFeet.y - hazardTop):0} m below you",
                State.Tutorial => $"TUTORIAL    Lesson {lessonIndex + 1} of {lessons.Length}    Esc to skip a lesson or leave",
                _ => "",
            };
            float width = Mathf.Min(Screen.width - 420f, hudStyle.CalcSize(new GUIContent(status)).x + 40f);
            if (status.Length > 0) GUI.Box(new Rect(Screen.width * 0.5f - width * 0.5f, 12f, width, 30f), status, hudStyle);
            if (!tutorial) GUI.Box(new Rect(Screen.width - 110f, 12f, 98f, 30f), Clock(runTime), hudStyle);

            var cam = Camera.main;
            if (!cam) return;
            if (state == State.Workshop || tutorial && CurrentLesson.workshop)
                foreach (var stand in stands)
                    if (!stand.Taken) Label(cam, stand.LabelPoint, stand.Part.displayName, Orange, 18f);
            if (exit && (exit.IsOpen || state == State.Room || tutorial)) Label(cam, exit.Middle + Vector3.up * 3f, exit.IsOpen ? "EXIT" : "EXIT (locked)",
                exit.IsOpen ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.4f, 0.35f), 200f, pinToEdge: exit.IsOpen);
            if (state == State.Shaft) Label(cam, podPoint + Vector3.up * 4.5f, "ESCAPE POD", new Color(0.5f, 0.95f, 1f), 200f, pinToEdge: true);
        }

        // A tag over a point in the room, with its distance. Pinned ones stay on the screen's edge
        // when the point is off screen, so you can always find them.
        void Label(Camera cam, Vector3 point, string text, Color color, float maxDistance, bool pinToEdge = false)
        {
            float distance = Vector3.Distance(cam.transform.position, point);
            if (distance > maxDistance) return;
            var screen = cam.WorldToScreenPoint(point);
            var at = new Vector2(screen.x, Screen.height - screen.y);
            if (screen.z < 0f)
            {
                if (!pinToEdge) return;
                at = new Vector2(Screen.width - at.x, Screen.height - at.y);
            }
            const float margin = 40f;
            bool off = screen.z < 0f || at.x < margin || at.x > Screen.width - margin || at.y < margin || at.y > Screen.height - margin;
            if (off && !pinToEdge) return;
            at.x = Mathf.Clamp(at.x, margin, Screen.width - margin);
            at.y = Mathf.Clamp(at.y, margin + 40f, Screen.height - margin);
            string full = $"{text}  {distance:0} m";
            float width = labelStyle.CalcSize(new GUIContent(full)).x + 14f;
            var rect = new Rect(at.x - width * 0.5f, at.y - 10f, width, 20f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.Label(rect, full, labelStyle);
            GUI.color = Color.white;
        }
    }
}
