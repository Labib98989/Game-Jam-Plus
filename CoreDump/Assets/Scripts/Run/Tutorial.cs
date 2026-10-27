using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoreDump
{
    // The tutorial, from the main menu: a short course of small rooms (the Tutorial rooms in
    // Resources/Rooms, in name order), each teaching one thing. A panel lists the lesson's steps and
    // waits for you to do each one; when they're all done, the exit opens onto the next lesson. The
    // last lesson is the workshop: take a part, build it on, and leave. Each lesson starts with the
    // starting robot, and dying starts the lesson again.
    public partial class RunManager
    {
        // One thing to do: what the panel says, how to tell it's done, and anything to set up first.
        class Step
        {
            public string text;
            public System.Func<bool> done;
            public System.Action begin;
        }

        class Lesson
        {
            public string title;
            // Takes place in the workshop, where building is allowed, instead of a tutorial room.
            public bool workshop;
            public System.Action begin;
            public Step[] steps;
        }

        // A dash from a Jet goes faster than running or sliding can.
        const float DashSpeed = 23f;

        List<RoomData> tutorialRooms = new();
        Lesson[] lessons;
        int lessonIndex, stepIndex;
        // What the robot has done since the step began.
        float walked, slid, wallRan, wallRunEndedAt;
        bool dashed, wallJumped, launchedAway, reloaded, enemiesIn, wasWallRunning;
        Vector3 lastFeet, lastWallNormal;
        // The height of the floor the lesson starts on, to tell when the robot has climbed something.
        float groundLevel;
        int partsAtStart;
        readonly List<TargetDummy> targets = new();
        GUIStyle lessonStyle, stepStyle, bannerStyle;

        Lesson CurrentLesson => lessons[lessonIndex];
        bool LessonDone => stepIndex >= CurrentLesson.steps.Length;

        void BuildLessons()
        {
            lessons = new[]
            {
                new Lesson
                {
                    title = "Moving",
                    steps = new[]
                    {
                        new Step { text = "WASD moves you, and the mouse turns you. Run 25 m.", done = () => walked >= 25f },
                        new Step { text = "Space fires your Spring, which jumps. Jump up onto the big block.", done = () => robot.Motor.IsGrounded && RobotFeet.y > groundLevel + 1.5f },
                        new Step { text = "Get running, then hold Ctrl to slide. A slide gives you a burst of speed.", done = () => slid >= 0.6f },
                    },
                },
                new Lesson
                {
                    title = "Jet and wall-run",
                    steps = new[]
                    {
                        new Step { text = "Shift fires your Jet. It's on your back, so a tap dashes you forward.", done = () => dashed },
                        new Step { text = "Run at a wall, jump, and keep holding W to run along it. Wall-running needs two Legs.", done = () => wallRan >= 0.8f },
                        new Step { text = "While wall-running, press Space to kick off the wall.", done = () => wallJumped },
                    },
                },
                new Lesson
                {
                    title = "Pads",
                    steps = new[]
                    {
                        new Step { text = "Step on the launch pad. The arrows show where it throws you.", done = () => launchedAway },
                        new Step { text = "A jump pad throws you straight up. Use it to get on top of the tower: steer onto it on the way down.", done = () => robot.Motor.IsGrounded && RobotFeet.y > groundLevel + 10f },
                    },
                },
                new Lesson
                {
                    title = "Shooting",
                    begin = SpawnTargets,
                    steps = new[]
                    {
                        new Step { text = "Left mouse fires your Scatter Blaster. Destroy every target.", done = () => targets.Count > 0 && targets.All(t => !t || !t.IsAlive) },
                        new Step { text = "A gun reloads by itself once it's empty. R reloads every gun at once.", done = () => reloaded },
                    },
                },
                new Lesson
                {
                    title = "Losing parts",
                    steps = new[]
                    {
                        new Step
                        {
                            text = "A hit knocks a part right off. There goes your Jet! Walk up to it and it snaps back on.",
                            begin = KnockOffJet,
                            done = () => PartChunk.Live.Count == 0,
                        },
                        new Step
                        {
                            text = "Skitters! Kill them all. They bite off whatever part is nearest, and grab any part left lying for 3 seconds. Kill the thief and it drops the part.",
                            begin = () => StartCoroutine(SendSkitters(lessonIndex, 4)),
                            done = () => enemiesIn && Enemy.AliveCount == 0,
                        },
                        new Step { text = "Get all your parts back on.", done = () => PartChunk.Live.Count == 0 },
                    },
                },
                new Lesson
                {
                    title = "The workshop",
                    workshop = true,
                    begin = () =>
                    {
                        foreach (var part in workshopGift)
                            if (part) inventory.Add(part);
                        StockStands();
                        partsAtStart = robot.Parts.Count;
                    },
                    steps = new[]
                    {
                        new Step { text = "Between rooms, the workshop gives out free parts. Walk into a stand to take its part.", done = () => stands.Any(s => s.Taken) },
                        new Step { text = "Press Tab to open the build screen. You can only build here in the workshop.", done = () => build.IsOpen },
                        new Step { text = "Drag a part from your inventory onto your robot, then press Tab to close the build screen.", done = () => robot.Parts.Count > partsAtStart && !build.IsOpen },
                    },
                },
            };
        }

        void StartTutorial()
        {
            Time.timeScale = 1f;
            paused = false;
            if (menuCamera) menuCamera.Priority = 0;
            StartCoroutine(Go(() => BeginLesson(0)));
        }

        void BeginLesson(int index)
        {
            state = State.Tutorial;
            lessonIndex = index;
            stepIndex = 0;
            var lesson = CurrentLesson;
            loadout.Respawn();
            var room = lesson.workshop ? workshopRoom
                : index < tutorialRooms.Count ? tutorialRooms[index]
                : PlainRoom($"Tutorial {index + 1}", RoomType.Tutorial);
            LoadArea(room, false);
            RoomNav.Bake(area);
            exit?.SetOpen(false);
            PlaceRobot();
            groundLevel = RobotFeet.y;
            build.allowed = lesson.workshop;
            lesson.begin?.Invoke();
            StartStep();
            Play();
            Juice.PlayMusic(Tune.Calm);
        }

        void StartStep()
        {
            walked = slid = wallRan = 0f;
            dashed = wallJumped = launchedAway = reloaded = enemiesIn = false;
            wallRunEndedAt = -1f;
            lastFeet = RobotFeet;
            if (!LessonDone) CurrentLesson.steps[stepIndex].begin?.Invoke();
        }

        void TrackTutorial()
        {
            if (CurrentLesson.workshop) TakeFromStands();
            Measure();
            if (!LessonDone && CurrentLesson.steps[stepIndex].done())
            {
                stepIndex++;
                if (LessonDone)
                {
                    exit?.SetOpen(true);
                    Say(lessonIndex + 1 < lessons.Length ? "Lesson done! Head for the EXIT" : "That's everything! Take the EXIT to finish");
                    Juice.Play(Sound.ExitOpen);
                }
                else
                {
                    Say("Nice!");
                    Juice.Play(Sound.StepDone);
                }
                StartStep();
            }
            if (!LessonDone || !exit || !exit.IsOpen || !exit.Contains(RobotFeet)) return;
            int next = lessonIndex + 1;
            StartCoroutine(Go(next < lessons.Length ? () => BeginLesson(next) : FinishTutorial));
        }

        void FinishTutorial()
        {
            state = State.Trained;
            Freeze();
            Juice.Play(Sound.Victory);
        }

        // Starts the lesson again, after the robot is destroyed.
        void RetryLesson()
        {
            Say("Destroyed! Starting the lesson again");
            int index = lessonIndex;
            StartCoroutine(Go(() => BeginLesson(index)));
        }

        void SkipLesson()
        {
            Resume();
            int next = lessonIndex + 1;
            StartCoroutine(Go(next < lessons.Length ? () => BeginLesson(next) : FinishTutorial));
        }

        // Keeps count of what the steps ask for.
        void Measure()
        {
            var motor = robot.Motor;
            var feet = RobotFeet;
            if (motor.IsGrounded) walked += new Vector2(feet.x - lastFeet.x, feet.z - lastFeet.z).magnitude;
            lastFeet = feet;
            if (motor.IsSliding) slid += Time.deltaTime;
            if (motor.HorizontalSpeed >= DashSpeed && !motor.IsSliding) dashed = true;
            if (motor.IsWallRunning)
            {
                wallRan += Time.deltaTime;
                lastWallNormal = motor.WallNormal;
            }
            else if (wasWallRunning) wallRunEndedAt = Time.time;
            wasWallRunning = motor.IsWallRunning;
            // A wall-run that just ended with the robot flying off away from the wall: a kick, not a drop.
            if (wallRunEndedAt > 0f && Time.time - wallRunEndedAt < 0.25f && Vector3.Dot(motor.Velocity, lastWallNormal) > 5f) wallJumped = true;
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) reloaded = true;
        }

        void OnPadLaunch(Vector3 launch)
        {
            if (new Vector2(launch.x, launch.z).sqrMagnitude > 1f) launchedAway = true;
        }

        // A target on each ground spawn spot. They stay down once destroyed.
        void SpawnTargets()
        {
            targets.Clear();
            foreach (var spot in current.All(RoomPieces.Ground))
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Target";
                go.transform.SetParent(area, false);
                go.transform.position = spot.Bottom + Vector3.up * 1.25f;
                go.transform.localScale = new Vector3(1.4f, 2.5f, 1.4f);
                go.GetComponent<Renderer>().sharedMaterial = enemies.skin;
                var target = go.AddComponent<TargetDummy>();
                target.respawnDelay = 1e6f;
                targets.Add(target);
            }
        }

        void KnockOffJet()
        {
            var part = robot.Parts.OfType<Jet>().FirstOrDefault() ?? robot.Parts.FirstOrDefault(p => p is not Head);
            if (part) health.KnockOff(part, -robot.transform.forward);
        }

        // A pack out of a vent, after the spawn flash, unless the robot has moved on by then.
        IEnumerator SendSkitters(int lesson, int count)
        {
            var vents = current.All(RoomPieces.Vent).ToList();
            var at = vents.Count > 0 ? vents[random.Next(vents.Count)].Bottom : areaCenter;
            FxGrow.Spawn(PrimitiveType.Sphere, at + Vector3.up * 0.6f, Vector3.one * 3f, 1f, enemies.spawnFx);
            yield return new WaitForSeconds(1f);
            if (state != State.Tutorial || lessonIndex != lesson) yield break;
            for (int i = 0; i < count; i++)
                Skitter.Spawn(enemies, robot, at + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f)));
            enemiesIn = true;
        }

        // The lesson's steps, top left: done ones ticked off, the current one lit up.
        void DrawLesson()
        {
            lessonStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = Orange } };
            stepStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            var lesson = CurrentLesson;
            const float width = 420f, pad = 12f, box = 14f;
            float textWidth = width - pad * 2f - box - 10f;
            var heights = lesson.steps.Select(s => stepStyle.CalcHeight(new GUIContent(s.text), textWidth)).ToList();
            float height = 40f + heights.Sum() + 8f * heights.Count + (LessonDone ? 30f : 0f);
            float x = 20f, y = 56f;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + pad, y + 10f, width - pad * 2f, 22f), $"LESSON {lessonIndex + 1} OF {lessons.Length}: {lesson.title.ToUpper()}", lessonStyle);
            y += 40f;
            for (int i = 0; i < lesson.steps.Length; i++)
            {
                bool done = i < stepIndex, now = i == stepIndex;
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
                GUI.color = done ? new Color(0.4f, 1f, 0.5f) : now ? new Color(1f, 0.6f, 0.15f, pulse) : new Color(0.45f, 0.45f, 0.5f);
                GUI.DrawTexture(new Rect(x + pad, y + 4f, box, box), Texture2D.whiteTexture);
                stepStyle.normal.textColor = done ? new Color(0.55f, 0.8f, 0.6f) : now ? Color.white : new Color(0.55f, 0.55f, 0.6f);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + pad + box + 10f, y, textWidth, heights[i]), lesson.steps[i].text, stepStyle);
                y += heights[i] + 8f;
            }
            if (!LessonDone) return;
            stepStyle.normal.textColor = new Color(0.4f, 1f, 0.5f);
            GUI.Label(new Rect(x + pad, y, width - pad * 2f, 24f), lessonIndex + 1 < lessons.Length ? "Done! Head for the EXIT" : "Done! Take the EXIT to finish", stepStyle);
        }

        // While the build screen is up, just the step to do, at the top between its panels.
        void DrawStepBanner()
        {
            if (LessonDone) return;
            bannerStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            float width = Mathf.Min(560f, Screen.width - 640f);
            if (width < 200f) return;
            GUI.Box(new Rect(Screen.width * 0.5f - width * 0.5f, 12f, width, 52f), CurrentLesson.steps[stepIndex].text, bannerStyle);
        }
    }
}
