using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoreDump.EditorTools
{
    // Builds the scenes from scratch: the game itself (menu: Core Dump > Build Game), the test room
    // (Core Dump > Build Test Room) and the room editor (Core Dump > Build Room Editor). Running one
    // again rebuilds its scene but keeps part tuning saved in Assets/Data.
    public static class TestRoomBuilder
    {
        const string GamePath = "Assets/Scenes/Game.unity";
        const string ScenePath = "Assets/Scenes/TestRoom.unity";
        const string RoomEditorPath = "Assets/Scenes/RoomEditor.unity";
        const string MaterialsFolder = "Assets/Art/Materials";
        const string DataFolder = "Assets/Data";
        const string RobotLayerName = "Robot";
        const string EnemyLayerName = "Enemy";

        enum Mode { TestRoom, RoomEditor, Game }

        // The whole game: the main menu, then a run through the rooms in Assets/Resources/Rooms.
        [MenuItem("Core Dump/Build Game")]
        public static void BuildGame() => BuildScene(Mode.Game);

        [MenuItem("Core Dump/Build Test Room")]
        public static void Build() => BuildScene(Mode.TestRoom);

        // The same robot, switched off until a room is tested, and no level: the editor builds it.
        [MenuItem("Core Dump/Build Room Editor")]
        public static void BuildRoomEditor() => BuildScene(Mode.RoomEditor);

        static void BuildScene(Mode mode)
        {
            bool roomEditor = mode == Mode.RoomEditor;
            bool game = mode == Mode.Game;
            int robotLayer = EnsureLayer(RobotLayerName);
            int enemyLayer = EnsureLayer(EnemyLayerName);
            LayerMask world = ~(1 << robotLayer);
            // The room itself: what the camera dodges, shadows land on and enemies stand on.
            LayerMask scenery = world & ~(1 << enemyLayer);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(DataFolder + "/Parts");
            EnsureFolder("Assets/Scenes");

            var floorMat = CheckerMaterial("Floor", new Color(0.42f, 0.43f, 0.45f), new Color(0.35f, 0.36f, 0.38f), new Vector2(60f, 60f));
            var blockMat = LitMaterial("Block", new Color(0.56f, 0.57f, 0.6f));
            var wallMat = LitMaterial("Wall", new Color(0.3f, 0.31f, 0.34f));
            var runWallMat = LitMaterial("RunWall", new Color(0.36f, 0.42f, 0.5f));
            var accentMat = LitMaterial("Accent", new Color(0.95f, 0.5f, 0.12f));
            var bodyMat = LitMaterial("RobotBody", new Color(0.95f, 0.55f, 0.15f), 0.35f);
            var visorMat = LitMaterial("RobotVisor", new Color(0.06f, 0.07f, 0.09f), 0.85f);
            var headMat = LitMaterial("PartHead", new Color(0.32f, 0.34f, 0.38f), 0.5f);
            var dummyMat = LitMaterial("Dummy", new Color(0.55f, 0.3f, 0.7f));
            var alienMat = LitMaterial("Alien", new Color(0.45f, 0.6f, 0.28f), 0.4f);
            var shadowMat = UnlitMaterial("LandingShadow", new Color(0.05f, 0.05f, 0.07f));
            var tracerMat = UnlitMaterial("Tracer", new Color(1f, 0.85f, 0.35f));
            var flameMat = UnlitMaterial("Flame", new Color(1f, 0.55f, 0.15f));
            var blastMat = UnlitMaterial("Blast", new Color(1f, 0.6f, 0.2f));
            var rocketMat = UnlitMaterial("RocketShot", new Color(1f, 0.95f, 0.8f));
            var lightningMat = UnlitMaterial("Lightning", new Color(0.55f, 0.9f, 1f));
            var bombMat = LitMaterial("BombShot", new Color(0.12f, 0.12f, 0.12f), 0.6f);
            var bladeMat = LitMaterial("SawBlade", new Color(0.82f, 0.84f, 0.88f), 0.8f);
            var enemyShotMat = UnlitMaterial("EnemyShot", new Color(0.6f, 1f, 0.3f));
            var ropeMat = UnlitMaterial("GrappleRope", new Color(0.85f, 0.9f, 0.95f));
            var markerMat = UnlitMaterial("GrappleMarker", new Color(0.3f, 1f, 0.9f));
            var shockwaveMat = UnlitMaterial("Shockwave", new Color(1f, 0.85f, 0.5f));
            var previewMat = TransparentMaterial("BuildPreview");
            var sparkMat = UnlitMaterial("Spark", new Color(1f, 0.8f, 0.3f));
            var smokeMat = LitMaterial("Smoke", new Color(0.18f, 0.18f, 0.2f));

            var frictionless = LoadOrCreate($"{DataFolder}/RobotFrictionless.asset", () => new PhysicsMaterial("Robot Frictionless"), out _);
            frictionless.dynamicFriction = 0f;
            frictionless.staticFriction = 0f;
            frictionless.frictionCombine = PhysicsMaterialCombine.Minimum;
            frictionless.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(frictionless);

            // Shapes, buttons, looks and descriptions are set every time. Tuning numbers are only set
            // when a part's asset is first made, so changes made in the Inspector are kept.
            var zero = Vector3Int.zero;
            var line2 = new[] { zero, new Vector3Int(0, 0, 1) };
            var line3 = new[] { zero, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, 2) };

            var leg = Part<LegDefinition>("Leg", "Leg", "Walking. Two walk properly, more go faster. Has to point down with its foot on the ground.",
                ButtonSlot.None, LitMaterial("PartLeg", new Color(0.5f, 0.52f, 0.56f), 0.6f), PartMarking.Nozzle, new Vector3Int(0, 0, 1), line2);
            var spring = Part<SpringDefinition>("Spring", "Spring", "Pushes off whatever its pad touches: underneath it jumps, against a wall it wall-jumps.",
                ButtonSlot.Space, LitMaterial("PartSpring", new Color(0.95f, 0.8f, 0.2f), 0.4f), PartMarking.Nozzle, zero, zero);
            var jet = Part<JetDefinition>("Jet", "Jet", "Tap to dash, hold to thrust, away from where its nozzle points. Fuel refills on the ground.",
                ButtonSlot.Shift, LitMaterial("PartJet", new Color(0.25f, 0.55f, 0.95f), 0.5f), PartMarking.Nozzle, zero, zero);
            jet.flameMaterial = flameMat;
            var ram = Part<RamDefinition>("Ram", "Ram", "A burst lunge toward its tip. Down in mid-air it's a butt slam, up it's a rocket jump.",
                ButtonSlot.Shift, LitMaterial("PartRam", new Color(0.8f, 0.22f, 0.18f), 0.5f), PartMarking.Nozzle, new Vector3Int(0, 0, 2), line3);
            ram.shockwaveMaterial = shockwaveMat;
            var grapple = Part<GrappleDefinition>("Grapple", "Grapple", "Hooks the surface nearest the crosshair and pulls you there. Catches knocked-off parts.",
                ButtonSlot.RightMouse, LitMaterial("PartGrapple", new Color(0.2f, 0.75f, 0.68f), 0.5f), PartMarking.Nozzle, new Vector3Int(0, 0, 1), line2);
            grapple.ropeMaterial = ropeMat;
            grapple.markerMaterial = markerMat;
            var treads = Part<TreadsDefinition>("Treads", "Treads", "Drives faster than Legs and can't be knocked back. Has to lie flat on the ground.",
                ButtonSlot.None, LitMaterial("PartTreads", new Color(0.16f, 0.16f, 0.18f), 0.3f), PartMarking.None, zero, zero, new Vector3Int(1, 0, 0));

            var blaster = Part<ScatterBlasterDefinition>("ScatterBlaster", "Scatter Blaster", "Shotgun blast with a big kick. Short range.",
                ButtonSlot.LeftMouse, LitMaterial("PartBlaster", new Color(0.66f, 0.68f, 0.72f), 0.5f), PartMarking.Nozzle, new Vector3Int(0, 0, 1),
                new[] { zero, new Vector3Int(0, 0, 1), new Vector3Int(0, 1, 0) },
                d => Gun(d, shots: 2, reload: 1.1f, interval: 0.3f, damage: 1.3f, range: 22f, kick: 13f));
            blaster.shotMaterial = tracerMat;
            blaster.fireSound = Sound.Blaster;
            var rockets = Part<RocketLauncherDefinition>("RocketLauncher", "Rocket Launcher", "Rockets with splash damage that always fly forward. Long reload.",
                ButtonSlot.LeftMouse, LitMaterial("PartRocketLauncher", new Color(0.45f, 0.5f, 0.32f), 0.5f), PartMarking.Nozzle, new Vector3Int(0, 0, 2), line3,
                d => Gun(d, shots: 3, reload: 2.4f, interval: 0.45f, damage: 9f, range: 80f, kick: 5f));
            rockets.shotMaterial = rocketMat;
            rockets.blastMaterial = blastMat;
            rockets.fireSound = Sound.Rocket;
            var fist = Part<RocketFistDefinition>("RocketFist", "Rocket Fist", "Fires your actual fist. Reloading calls it back, smacking anything on the way.",
                ButtonSlot.LeftMouse, LitMaterial("PartRocketFist", new Color(0.95f, 0.45f, 0.25f), 0.5f), PartMarking.Nozzle, new Vector3Int(0, 0, 2),
                new[] { zero, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, 2), new Vector3Int(0, 1, 0) },
                d => Gun(d, shots: 1, reload: 1f, interval: 0.3f, damage: 14f, range: 30f, kick: 10f));
            var rivets = Part<RivetGunDefinition>("RivetGun", "Rivet Gun", "Rapid fire while held, long range, small kick. Low damage per shot.",
                ButtonSlot.LeftMouse, LitMaterial("PartRivetGun", new Color(0.45f, 0.55f, 0.65f), 0.5f), PartMarking.Nozzle, new Vector3Int(0, 0, 1), line2,
                d => Gun(d, shots: 30, reload: 1.8f, interval: 0.08f, damage: 0.6f, range: 70f, kick: 0.8f, automatic: true));
            rivets.shotMaterial = tracerMat;
            rivets.fireSound = Sound.Rivet;
            fist.fireSound = Sound.Fist;
            var welder = Part<WelderDefinition>("Welder", "Welder", "Hold for a short cone of flame. Very short range.",
                ButtonSlot.LeftMouse, LitMaterial("PartWelder", new Color(0.9f, 0.6f, 0.15f), 0.5f), PartMarking.Nozzle, new Vector3Int(0, 0, 1), line2,
                d => Gun(d, shots: 30, reload: 2f, interval: 0.1f, damage: 0.8f, range: 6f, kick: 0.4f, automatic: true));
            welder.shotMaterial = flameMat;
            welder.fireSound = Sound.Welder;
            var zapper = Part<ZapperDefinition>("Zapper", "Zapper", "Lightning that aims itself and jumps from enemy to enemy. Weak, short range.",
                ButtonSlot.LeftMouse, LitMaterial("PartZapper", new Color(0.2f, 0.85f, 1f), 0.6f), PartMarking.Nozzle, zero, new[] { zero },
                d => Gun(d, shots: 5, reload: 1.6f, interval: 0.35f, damage: 2f, range: 14f, kick: 2f));
            zapper.shotMaterial = lightningMat;
            zapper.fireSound = Sound.Zap;
            var bombs = Part<BombBayDefinition>("BombBay", "Bomb Bay", "Drops bombs the way it points. Bombs take a second to go off.",
                ButtonSlot.LeftMouse, LitMaterial("PartBombBay", new Color(0.22f, 0.24f, 0.22f), 0.5f), PartMarking.Nozzle, zero, new[] { zero },
                d => Gun(d, shots: 3, reload: 2f, interval: 0.25f, damage: 8f, range: 10f, kick: 3f));
            bombs.shotMaterial = bombMat;
            bombs.blastMaterial = blastMat;
            bombs.fireSound = Sound.BombDrop;
            var saw = Part<BuzzSawDefinition>("BuzzSaw", "Buzz Saw", "Always spinning, hurts anything it touches. No button.",
                ButtonSlot.None, LitMaterial("PartBuzzSaw", new Color(0.28f, 0.28f, 0.3f), 0.5f), PartMarking.None, zero,
                new[] { zero, new Vector3Int(1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0) });
            saw.bladeMaterial = bladeMat;
            var head = Part<HeadDefinition>("Head", "Head", "Every robot has exactly one. Lose it and the HUD breaks loose.",
                ButtonSlot.None, headMat, PartMarking.Visor, zero, zero);

            // Connectors: the only parts other parts can attach to, besides the body.
            var connectorMat = LitMaterial("PartConnector", new Color(0.62f, 0.36f, 0.12f), 0.35f);
            const string connectorText = "A plain block to build the robot out. Parts attach only to the body and to connectors.";
            var connector = Part<ConnectorDefinition>("Connector", "Connector", connectorText, ButtonSlot.None, connectorMat, PartMarking.None, zero, zero);
            var connectorBar = Part<ConnectorDefinition>("ConnectorBar", "Connector Bar", connectorText, ButtonSlot.None, connectorMat, PartMarking.None, zero, line3);
            var connectorElbow = Part<ConnectorDefinition>("ConnectorElbow", "Connector Elbow", connectorText, ButtonSlot.None, connectorMat, PartMarking.None, zero,
                zero, new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 1));

            // Mods: one cube each, colored by family. Their type is set every time; their numbers
            // only when the asset is first made.
            ModDefinition Mod(string file, string name, ModType type, string description, Color color, float amountPerLevel)
            {
                var mod = Part<ModDefinition>(file, name, description, ButtonSlot.None, LitMaterial(file, color, 0.55f), PartMarking.None, zero,
                    new[] { zero }, d => d.amountPerLevel = amountPerLevel);
                mod.type = type;
                EditorUtility.SetDirty(mod);
                return mod;
            }
            const string chainText = " Mods of the same type that touch form a chain, and longer chains boost more.";
            var mods = new[]
            {
                Mod("ModPower", "Power", ModType.Power, "Movement parts touching it get stronger: faster Legs and Treads, higher Springs, harder Jets, longer Ram lunges, stronger Grapple pulls." + chainText,
                    new Color(0.2f, 0.75f, 0.6f), 0.15f),
                Mod("ModRecharge", "Recharge", ModType.Recharge, "Jets touching it hold more fuel and dash again sooner, and a touching Ram lunges again sooner." + chainText,
                    new Color(0.15f, 0.55f, 0.75f), 0.3f),
                Mod("ModDamage", "Damage", ModType.Damage, "Weapons touching it hit harder, and so does a touching Ram." + chainText,
                    new Color(0.85f, 0.2f, 0.2f), 0.25f),
                Mod("ModFireRate", "Fire Rate", ModType.FireRate, "Weapons touching it fire faster, and a touching Buzz Saw spins faster." + chainText,
                    new Color(0.95f, 0.45f, 0.15f), 0.25f),
                Mod("ModReloadSpeed", "Reload Speed", ModType.ReloadSpeed, "Weapons touching it reload faster." + chainText,
                    new Color(0.9f, 0.7f, 0.2f), 0.3f),
                Mod("ModRecoil", "Recoil", ModType.Recoil, "Weapons touching it kick harder: more push for recoil jumps and boosts." + chainText,
                    new Color(0.7f, 0.25f, 0.45f), 0.4f),
                Mod("ModWideLens", "Wide Lens", ModType.WideLens, "Touching the head, it widens your view. Vision mods can sit on the head." + chainText,
                    new Color(0.6f, 0.45f, 0.9f), 12f),
                Mod("ModThermalEye", "Thermal Eye", ModType.ThermalEye, "Touching the head, it marks enemies through walls. Touching a Rocket Launcher, it makes the rockets home in." + chainText,
                    new Color(0.85f, 0.35f, 0.8f), 30f),
            };
            var roomKit = RoomKitAsset(previewMat);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (mode == Mode.TestRoom) BuildLevel(blockMat, wallMat, runWallMat, accentMat, floorMat, dummyMat);

            // The robot: a body 1 wide, 1 deep and 4 tall, in half-meter cells, that builds its own
            // cubes (each with a frictionless box collider) when play starts, then fits its starting parts.
            var robotGo = new GameObject("Robot") { layer = robotLayer };
            robotGo.transform.position = game ? new Vector3(0f, 0.05f, 0f) : new Vector3(0f, 0.05f, -45f);
            robotGo.AddComponent<Rigidbody>().mass = 1f;
            var body = robotGo.AddComponent<RobotBody>();
            body.size = new Vector3Int(1, 4, 1);
            body.cellSize = 0.5f;
            body.bodyMaterial = bodyMat;
            body.detailMaterial = visorMat;
            body.physicsMaterial = frictionless;
            body.shotMask = world;
            var motor = robotGo.AddComponent<RobotMotor>();
            var input = robotGo.AddComponent<RobotInput>();
            var health = robotGo.AddComponent<RobotHealth>();
            health.maxHealth = 10;
            health.invulnerableTime = 0.3f;
            health.sparkMaterial = sparkMat;
            health.smokeMaterial = smokeMat;
            health.beaconMaterial = BeaconMaterial("HeadBeacon", new Color(1f, 0.5f, 0.15f, 0.9f));
            robotGo.AddComponent<RobotHud>();
            robotGo.AddComponent<BodySlam>();
            robotGo.AddComponent<RobotJuice>();
            // The body is the column x = 0, y = 0 to 3. A Leg hangs down each side of its bottom cube,
            // with a Spring under it between them, so all three reach the ground. The head sits on
            // top, the Scatter Blaster on the right side facing forward, and the Jet on the back.
            var down = new Vector3(90f, 0f, 0f);
            var loadout = robotGo.AddComponent<StartingLoadout>();
            loadout.testControls = !game;
            loadout.mounts = new[]
            {
                new StartingLoadout.Mount { part = leg, anchor = new Vector3Int(-1, 0, 0), rotation = down },
                new StartingLoadout.Mount { part = leg, anchor = new Vector3Int(1, 0, 0), rotation = down },
                new StartingLoadout.Mount { part = spring, anchor = new Vector3Int(0, -1, 0), rotation = down },
                new StartingLoadout.Mount { part = head, anchor = new Vector3Int(0, 4, 0) },
                new StartingLoadout.Mount { part = blaster, anchor = new Vector3Int(1, 2, 0) },
                new StartingLoadout.Mount { part = jet, anchor = new Vector3Int(0, 2, -1), rotation = new Vector3(0f, 180f, 0f) },
            };
            // Everything is unlimited in the test room; set counts on the robot's inventory to try limits.
            // A run starts with nothing spare: the workshop's stands fill the inventory.
            var allParts = new PartDefinition[] { connector, connectorBar, connectorElbow, leg, spring, jet, ram, grapple, treads, blaster, rockets, rivets, fist, welder, zapper, bombs, saw }
                .Concat(mods).ToArray();
            robotGo.AddComponent<PartInventory>().stock = game
                ? new System.Collections.Generic.List<PartInventory.Stock>()
                : allParts.Select(p => new PartInventory.Stock { part = p, count = -1 }).ToList();
            // The room editor's robot doesn't build: there, Tab goes back to editing the room.
            BuildMode build = null;
            if (!roomEditor)
            {
                build = robotGo.AddComponent<BuildMode>();
                build.previewMaterial = previewMat;
                build.robotMask = 1 << robotLayer;
            }

            var aimGo = new GameObject("Aim Pivot");
            var aim = aimGo.AddComponent<AimRig>();
            aim.target = robotGo.transform;
            aim.aimMask = world;
            aimGo.transform.position = robotGo.transform.position + aim.offset;
            body.aim = aim;
            input.aim = aim;
            if (build) build.aim = aim;

            // Over-the-shoulder camera: follows the aim pivot and turns with it. The brain ignores
            // time scale so the switch to the build camera still blends while the action is paused.
            var brain = Camera.main.gameObject.AddComponent<CinemachineBrain>();
            brain.IgnoreTimeScale = true;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.35f);
            var cameraGo = new GameObject("Player Camera");
            var vcam = cameraGo.AddComponent<CinemachineCamera>();
            vcam.Priority = 10;
            vcam.Follow = aimGo.transform;
            var lens = LensSettings.Default;
            lens.FieldOfView = 70f;
            lens.NearClipPlane = 0.2f;
            lens.FarClipPlane = 600f;
            vcam.Lens = lens;
            var follow = cameraGo.AddComponent<CinemachineThirdPersonFollow>();
            follow.ShoulderOffset = new Vector3(1.6f, 0.4f, 0f);
            follow.VerticalArmLength = 0.3f;
            follow.CameraSide = 1f;
            follow.CameraDistance = 8f;
            follow.Damping = new Vector3(0.05f, 0.15f, 0.05f);
            follow.AvoidObstacles.Enabled = true;
            follow.AvoidObstacles.CollisionFilter = scenery;
            follow.AvoidObstacles.CameraRadius = 0.3f;
            cameraGo.AddComponent<CinemachineRotateWithFollowTarget>();
            var zoom = cameraGo.AddComponent<CameraZoom>();
            zoom.robot = body;
            zoom.aim = aim;
            zoom.follow = follow;
            cameraGo.AddComponent<SpeedFeel>().motor = motor;
            cameraGo.AddComponent<CameraShake>();

            if (!roomEditor)
            {
                var buildCameraGo = new GameObject("Build Camera");
                var buildCamera = buildCameraGo.AddComponent<CinemachineCamera>();
                buildCamera.Priority = 0;
                var buildLens = lens;
                buildLens.FieldOfView = 50f;
                buildCamera.Lens = buildLens;
                build.buildCamera = buildCamera;

                if (mode == Mode.TestRoom)
                {
                    // One alien turret that fires slow shots, to knock parts off. Out of range of the spawn point.
                    var turretGo = new GameObject("Turret");
                    turretGo.transform.position = new Vector3(18f, 0f, -18f);
                    var turretBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    turretBase.name = "Base";
                    turretBase.transform.SetParent(turretGo.transform, false);
                    turretBase.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                    turretBase.transform.localScale = new Vector3(1.6f, 0.6f, 1.6f);
                    turretBase.GetComponent<Renderer>().sharedMaterial = alienMat;
                    var turretHead = new GameObject("Head").transform;
                    turretHead.SetParent(turretGo.transform, false);
                    turretHead.localPosition = new Vector3(0f, 1.7f, 0f);
                    Block(turretHead, "Housing", turretHead.position, new Vector3(1.2f, 1f, 1.2f), alienMat, isStatic: false);
                    Block(turretHead, "Barrel", turretHead.position + new Vector3(0f, 0f, 0.9f), new Vector3(0.3f, 0.3f, 1.2f), alienMat, isStatic: false);
                    turretGo.AddComponent<TargetDummy>().maxHealth = 20f;
                    var turret = turretGo.AddComponent<Turret>();
                    turret.target = robotGo.transform;
                    turret.head = turretHead;
                    turret.shotMaterial = enemyShotMat;
                }
            }

            var shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shadow.name = "Landing Shadow";
            Object.DestroyImmediate(shadow.GetComponent<Collider>());
            var shadowRenderer = shadow.GetComponent<Renderer>();
            shadowRenderer.sharedMaterial = shadowMat;
            shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var landing = shadow.AddComponent<LandingShadow>();
            landing.target = robotGo.transform;
            landing.groundMask = scenery;

            // Sound, music, shake and the little effects that go with them.
            var juice = new GameObject("Juice").AddComponent<Juice>();
            juice.flashMaterial = UnlitMaterial("MuzzleFlash", new Color(1f, 0.92f, 0.6f));
            juice.sparkMaterial = sparkMat;
            juice.dustMaterial = UnlitMaterial("Dust", new Color(0.55f, 0.52f, 0.48f));
            juice.gooMaterial = UnlitMaterial("AlienGoo", new Color(0.55f, 0.95f, 0.3f));
            juice.ringMaterial = roomKit.pad;

            var sun = Object.FindAnyObjectByType<Light>();
            if (sun)
            {
                sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
                sun.shadows = LightShadows.Soft;
            }

            if (game)
            {
                // Circles the workshop behind the main menu.
                var menuCameraGo = new GameObject("Menu Camera");
                var menuCamera = menuCameraGo.AddComponent<CinemachineCamera>();
                menuCamera.Priority = 40;
                var menuLens = lens;
                menuLens.FieldOfView = 55f;
                menuCamera.Lens = menuLens;

                var enemies = LoadOrCreate($"{DataFolder}/EnemyKit.asset", () => ScriptableObject.CreateInstance<EnemyKit>(), out _);
                enemies.skin = alienMat;
                enemies.armor = LitMaterial("AlienArmor", new Color(0.34f, 0.36f, 0.4f), 0.65f);
                enemies.eye = UnlitMaterial("AlienEye", new Color(0.8f, 1f, 0.35f));
                enemies.shot = enemyShotMat;
                enemies.spawnFx = UnlitMaterial("SpawnFlash", new Color(0.8f, 0.45f, 1f));
                enemies.dangerZone = TransparentMaterial("DangerZone");
                enemies.dangerZone.SetColor("_BaseColor", new Color(1f, 0.1f, 0.05f, 0.35f));
                enemies.dangerFill = UnlitMaterial("DangerFill", new Color(1f, 0.15f, 0.08f));
                enemies.layer = enemyLayer;
                enemies.groundMask = scenery;
                enemies.shotMask = ~(1 << enemyLayer);
                EditorUtility.SetDirty(enemies);
                AssetDatabase.SaveAssets();

                var run = new GameObject("Run").AddComponent<RunManager>();
                run.kit = roomKit;
                run.enemies = enemies;
                run.robot = body;
                run.aim = aim;
                run.menuCamera = menuCamera;
                // Anything but the head and connectors can be on a stand; connectors come a few every visit.
                run.shopParts = allParts.Where(p => p is not ConnectorDefinition).ToArray();
                run.workshopGift = new PartDefinition[] { connector, connector, connectorBar, connectorElbow };
                run.standMaterial = LitMaterial("ShopStand", new Color(0.3f, 0.32f, 0.36f), 0.4f);
                run.glowMaterial = UnlitMaterial("Glow", Color.white);
                run.hazardMaterial = UnlitMaterial("Hazard", new Color(1f, 0.38f, 0.08f));
                run.podMaterial = LitMaterial("EscapePod", new Color(0.9f, 0.9f, 0.92f), 0.6f);
                EnsureFolder("Assets/Resources/Rooms");
                EditorSceneManager.SaveScene(scene, GamePath);
                ListScenes();
                Debug.Log($"Core Dump: built the game at {GamePath}");
                return;
            }

            if (roomEditor)
            {
                // The editor's own camera, which flies around. The player camera only comes on for a test.
                var editorCameraGo = new GameObject("Editor Camera");
                var editorCamera = editorCameraGo.AddComponent<CinemachineCamera>();
                editorCamera.Priority = 30;
                var editorLens = lens;
                editorLens.FieldOfView = 60f;
                editorLens.FarClipPlane = 1000f;
                editorCamera.Lens = editorLens;

                var editor = new GameObject("Room Editor").AddComponent<RoomEditor>();
                editor.kit = roomKit;
                editor.editorCamera = editorCamera;
                editor.robot = body;
                editor.aim = aim;
                editor.testObjects = new[] { robotGo, cameraGo, shadow };
                foreach (var go in editor.testObjects) go.SetActive(false);
                EnsureFolder("Assets/Resources/Rooms");
                EditorSceneManager.SaveScene(scene, RoomEditorPath);
                Debug.Log($"Core Dump: built the room editor at {RoomEditorPath}");
                return;
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            ListScenes();
            Debug.Log($"Core Dump: built the test room at {ScenePath}");
        }

        // The game first, so a build opens on the main menu, then the test room.
        static void ListScenes()
        {
            var first = new[] { GamePath, ScenePath }.Where(File.Exists).ToList();
            var rest = EditorBuildSettings.scenes.Where(scene => !first.Contains(scene.path));
            EditorBuildSettings.scenes = first.Select(path => new EditorBuildSettingsScene(path, true)).Concat(rest).ToArray();
        }

        // What rooms are made of. Room UVs are in meters and each checker tile holds 2 x 2 squares,
        // so a scale of 0.5 makes the squares 1 m, the size of the editor's grid.
        static RoomKit RoomKitAsset(Material preview)
        {
            var kit = LoadOrCreate($"{DataFolder}/RoomKit.asset", () => ScriptableObject.CreateInstance<RoomKit>(), out _);
            var meters = new Vector2(0.5f, 0.5f);
            kit.floor = CheckerMaterial("RoomFloor", new Color(0.44f, 0.45f, 0.47f), new Color(0.38f, 0.39f, 0.41f), meters);
            kit.wall = CheckerMaterial("RoomWall", new Color(0.36f, 0.4f, 0.46f), new Color(0.32f, 0.36f, 0.42f), meters);
            kit.ramp = CheckerMaterial("RoomRamp", new Color(0.93f, 0.52f, 0.16f), new Color(0.84f, 0.45f, 0.12f), meters);
            kit.prop = LitMaterial("RoomProp", new Color(0.62f, 0.44f, 0.24f), 0.25f);
            kit.lamp = UnlitMaterial("RoomLamp", new Color(1f, 0.93f, 0.78f));
            kit.pad = UnlitMaterial("RoomPad", new Color(0.3f, 0.95f, 1f));
            kit.preview = preview;
            EditorUtility.SetDirty(kit);
            return kit;
        }

        // A 120 x 120 room made for fast movement: steps, a pair of long walls to wall-run along, a
        // ramp to slide down, towers with a catwalk and tall pillars to grapple onto, and a high ledge.
        static void BuildLevel(Material block, Material wall, Material runWall, Material accent, Material floor, Material dummyMat)
        {
            var level = new GameObject("Level").transform;
            Block(level, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(120f, 1f, 120f), floor);
            Block(level, "Wall North", new Vector3(0f, 8f, 60.5f), new Vector3(122f, 16f, 1f), wall);
            Block(level, "Wall South", new Vector3(0f, 8f, -60.5f), new Vector3(122f, 16f, 1f), wall);
            Block(level, "Wall East", new Vector3(60.5f, 8f, 0f), new Vector3(1f, 16f, 120f), wall);
            Block(level, "Wall West", new Vector3(-60.5f, 8f, 0f), new Vector3(1f, 16f, 120f), wall);

            for (int i = 0; i < 4; i++)
            {
                float height = 2f * (i + 1);
                Block(level, $"Step {height}m", new Vector3(-48f, height * 0.5f, -36f + i * 9f), new Vector3(10f, height, 7f), block);
            }

            Block(level, "Run Wall A", new Vector3(-24f, 6f, 5f), new Vector3(1f, 12f, 44f), runWall);
            Block(level, "Run Wall B", new Vector3(-12f, 6f, 15f), new Vector3(1f, 12f, 44f), runWall);

            // A ramp up to a platform: run up, then slide back down it.
            var ramp = Block(level, "Slide Ramp", new Vector3(32f, 3.4f, 8f), new Vector3(12f, 1f, 30f), accent);
            ramp.transform.rotation = Quaternion.Euler(-15f, 0f, 0f);
            Block(level, "Ramp Platform", new Vector3(32f, 3.88f, 30.5f), new Vector3(16f, 7.76f, 16f), block);

            Block(level, "Tower A", new Vector3(48f, 8f, -30f), new Vector3(7f, 16f, 7f), block);
            Block(level, "Tower B", new Vector3(48f, 8f, -6f), new Vector3(7f, 16f, 7f), block);
            Block(level, "Catwalk", new Vector3(48f, 15.75f, -18f), new Vector3(3f, 0.5f, 17f), accent);
            Block(level, "Pillar 1", new Vector3(8f, 11f, 22f), new Vector3(3f, 22f, 3f), block);
            Block(level, "Pillar 2", new Vector3(-36f, 11f, 44f), new Vector3(3f, 22f, 3f), block);
            Block(level, "Pillar 3", new Vector3(40f, 11f, 50f), new Vector3(3f, 22f, 3f), block);
            Block(level, "Divider Wall", new Vector3(10f, 6f, 0f), new Vector3(24f, 12f, 1f), wall);
            Block(level, "High Ledge", new Vector3(-44f, 20f, 48f), new Vector3(14f, 0.5f, 12f), accent);
            Block(level, "Cover 1", new Vector3(-8f, 1f, -24f), new Vector3(5f, 2f, 1f), block);
            Block(level, "Cover 2", new Vector3(10f, 1f, -30f), new Vector3(5f, 2f, 1f), block);
            Block(level, "Cover 3", new Vector3(0f, 1.5f, 40f), new Vector3(8f, 3f, 1f), block);

            var dummies = new GameObject("Dummies").transform;
            var dummySpots = new[]
            {
                new Vector3(-6f, 0f, -32f), new Vector3(6f, 0f, -32f), new Vector3(0f, 0f, -22f),
                new Vector3(-10f, 0f, -14f), new Vector3(12f, 0f, -10f), new Vector3(-48f, 6f, -18f),
                new Vector3(48f, 16f, -30f), new Vector3(48f, 16f, -12f), new Vector3(32f, 7.76f, 32f),
                new Vector3(-44f, 20.25f, 48f), new Vector3(-18f, 0f, 30f), new Vector3(20f, 0f, 44f),
            };
            foreach (var spot in dummySpots)
            {
                var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                dummy.name = "Target Dummy";
                dummy.transform.SetParent(dummies, false);
                dummy.transform.position = spot + Vector3.up * 1.2f;
                dummy.transform.localScale = Vector3.one * 1.2f;
                dummy.GetComponent<Renderer>().sharedMaterial = dummyMat;
                dummy.AddComponent<TargetDummy>();
            }
        }

        static void Gun(WeaponDefinition gun, int shots, float reload, float interval, float damage, float range, float kick, bool automatic = false)
        {
            gun.shotsPerReload = shots;
            gun.reloadTime = reload;
            gun.fireInterval = interval;
            gun.damage = damage;
            gun.range = range;
            gun.kick = kick;
            gun.automatic = automatic;
        }

        static GameObject Block(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool isStatic = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // A part asset: its cubes, default button, look and description are set every time. `tune` sets
        // its tuning numbers, and only runs when the asset is first made.
        static T Part<T>(string file, string displayName, string description, ButtonSlot button, Material material, PartMarking marking,
            Vector3Int nozzleCube, Vector3Int[] cubes, System.Action<T> tune = null) where T : PartDefinition
        {
            var part = LoadOrCreate($"{DataFolder}/Parts/{file}.asset", () => ScriptableObject.CreateInstance<T>(), out bool created);
            part.displayName = displayName;
            part.description = description;
            part.cubes = cubes;
            part.defaultButton = button;
            part.material = material;
            part.marking = marking;
            part.nozzleCube = nozzleCube;
            if (created) tune?.Invoke(part);
            EditorUtility.SetDirty(part);
            return part;
        }

        static T Part<T>(string file, string displayName, string description, ButtonSlot button, Material material, PartMarking marking,
            Vector3Int nozzleCube, params Vector3Int[] cubes) where T : PartDefinition =>
            Part<T>(file, displayName, description, button, material, marking, nozzleCube, cubes, null);

        // See-through and unlit, for the build mode's preview. Its color is set per renderer.
        static Material TransparentMaterial(string name)
        {
            var mat = UnlitMaterial(name, Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // A glow drawn over everything, walls included (the CoreDump/Beacon shader). Its alpha is its brightness.
        static Material BeaconMaterial(string name, Color color)
        {
            var mat = LoadOrCreate($"{MaterialsFolder}/{name}.mat", () => new Material(Shader.Find("CoreDump/Beacon")), out _);
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material LitMaterial(string name, Color color, float smoothness = 0.15f)
        {
            var mat = LoadOrCreate($"{MaterialsFolder}/{name}.mat", () => new Material(Shader.Find("Universal Render Pipeline/Lit")), out _);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material UnlitMaterial(string name, Color color)
        {
            var mat = LoadOrCreate($"{MaterialsFolder}/{name}.mat", () => new Material(Shader.Find("Universal Render Pipeline/Unlit")), out _);
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // A 1-unit checkerboard, so you can read speed and distance while moving.
        static Material CheckerMaterial(string name, Color a, Color b, Vector2 tiling)
        {
            var texture = LoadOrCreate($"{MaterialsFolder}/{name}Checker.asset", () => CheckerTexture(a, b), out _);
            var mat = LitMaterial(name, Color.white);
            mat.SetTexture("_BaseMap", texture);
            mat.SetTextureScale("_BaseMap", tiling);
            return mat;
        }

        static Texture2D CheckerTexture(Color a, Color b)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 8,
            };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, (x < size / 2) ^ (y < size / 2) ? a : b);
            texture.Apply(true);
            return texture;
        }

        static T LoadOrCreate<T>(string path, System.Func<T> create, out bool created) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = !asset;
            if (created)
            {
                asset = create();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 6; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = name;
                tagManager.ApplyModifiedProperties();
                return i;
            }
            throw new System.InvalidOperationException($"No free layer for '{name}'.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
