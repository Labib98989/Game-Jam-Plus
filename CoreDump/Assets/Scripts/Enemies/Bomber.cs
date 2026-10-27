using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoreDump
{
    // Flies high and out of the way, then makes bombing runs. It marks a row of zones on the ground
    // across where the robot is heading, each glowing red and filling up as its blast nears, then
    // swoops low along the row and blasts each zone in turn, just as it fills. Stay out of the red.
    // It's only low during a run, and killing it calls off the zones it marked.
    public class Bomber : Flyer
    {
        public float cruiseSpeed = 22f;
        [Tooltip("How high above the robot it circles between runs, and how far out.")]
        public float cruiseHeight = 18f;
        public float cruiseRadius = 26f;
        [Tooltip("How long the first zone of a run glows before it's blasted.")]
        public float warning = 1.6f;
        [Tooltip("Time between one zone's blast and the next one's along the row.")]
        public float blastGap = 0.3f;
        public int zonesPerRun = 3;
        public float zoneRadius = 4f;
        [Tooltip("Meters between the middles of neighboring zones.")]
        public float zoneSpacing = 7f;
        [Tooltip("How high above the zones it flies during a run.")]
        public float runHeight = 7f;
        [Tooltip("Its top speed while catching up with a run.")]
        public float runSpeed = 48f;
        [Tooltip("Seconds of circling between runs.")]
        public float cooldown = 3.5f;

        // A blast reaches this high above its zone, so a big enough jump clears it.
        const float BlastHeight = 8f;
        // How long its bolt takes to reach a zone; it's fired this long before the blast.
        const float BoltTime = 0.15f;

        enum State { Cruise, Run, Climb }

        class Zone
        {
            public Vector3 point;
            public float markedAt, blastAt;
            public Transform root, fill;
            public bool fired;
        }

        readonly List<Zone> zones = new();
        State state;
        float stateUntil, orbit, railTime, lastBlastAt;
        Vector3 railStart, runDirection;

        public static Bomber Spawn(EnemyKit kit, RobotBody robot, Vector3 position)
        {
            const float scale = 1.6f;
            var go = NewBody("Bomber", kit, position, Vector3.zero, new Vector3(3.2f, 1.2f, 2.4f) * scale);
            var t = Model(go, scale);
            Box(t, Vector3.zero, new Vector3(0.6f, 0.45f, 1.5f), kit.skin);
            Box(t, new Vector3(0f, 0.05f, -0.1f), new Vector3(2.4f, 0.08f, 0.7f), kit.skin);
            Box(t, new Vector3(0f, 0.1f, -0.9f), new Vector3(0.9f, 0.06f, 0.4f), kit.skin);
            Box(t, new Vector3(0f, 0.05f, 0.8f), new Vector3(0.35f, 0.3f, 0.3f), kit.armor);
            Box(t, new Vector3(-0.12f, 0.12f, 0.96f), Vector3.one * 0.08f, kit.eye);
            Box(t, new Vector3(0.12f, 0.12f, 0.96f), Vector3.one * 0.08f, kit.eye);
            // The bomb bay underneath, glowing the red of its zones.
            Box(t, new Vector3(0f, -0.25f, 0f), new Vector3(0.4f, 0.12f, 0.8f), kit.dangerFill);

            var bomber = go.AddComponent<Bomber>();
            bomber.maxHealth = 8f;
            bomber.radius = 1.2f;
            bomber.Init(kit, robot);
            bomber.orbit = Random.Range(0f, 360f);
            bomber.stateUntil = Time.time + Random.Range(1.5f, 3f);
            return bomber;
        }

        protected override void Update()
        {
            base.Update();
            if (!Hunting) return;
            TickZones();
            switch (state)
            {
                case State.Cruise:
                    orbit += 35f * Time.deltaTime;
                    FlyTo(RobotCenter + Quaternion.Euler(0f, orbit, 0f) * new Vector3(0f, cruiseHeight, cruiseRadius), cruiseSpeed, 40f);
                    FaceAlong(velocity, 6f);
                    if (Time.time >= stateUntil) Mark();
                    break;

                case State.Run:
                    FlyTo(RailPoint(Time.time), runSpeed, 110f);
                    FaceAlong(runDirection + Vector3.down * 0.15f, 8f);
                    if (Time.time > lastBlastAt + 0.3f)
                    {
                        state = State.Climb;
                        stateUntil = Time.time + 1.2f;
                    }
                    break;

                case State.Climb:
                    FlyTo(transform.position + Vector3.up * 12f + runDirection * 8f, cruiseSpeed, 50f);
                    FaceAlong(velocity, 6f);
                    if (Time.time > stateUntil)
                    {
                        state = State.Cruise;
                        stateUntil = Time.time + cooldown;
                    }
                    break;
            }
        }

        // Lays a row of zones on the ground across where the robot is heading, lined up the way the
        // bomber will fly over them, and starts the run.
        void Mark()
        {
            var heading = robot.Motor.Velocity;
            heading.y = 0f;
            var aim = RobotCenter + heading * 0.7f;
            var direction = aim - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1f) direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            runDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;

            float first = -(zonesPerRun - 1) * 0.5f * zoneSpacing;
            float highest = float.NegativeInfinity;
            railTime = Time.time + warning;
            for (int i = 0; i < zonesPerRun; i++)
            {
                var spot = aim + runDirection * (first + i * zoneSpacing);
                if (!Physics.Raycast(new Vector3(spot.x, aim.y + 2f, spot.z), Vector3.down, out var hit, 60f, kit.groundMask, QueryTriggerInteraction.Ignore)) continue;
                zones.Add(NewZone(hit.point, hit.normal, railTime + i * blastGap));
                highest = Mathf.Max(highest, hit.point.y);
            }
            if (float.IsNegativeInfinity(highest))
            {
                stateUntil = Time.time + 0.5f;
                return;
            }
            lastBlastAt = railTime + (zonesPerRun - 1) * blastGap;
            railStart = aim + runDirection * first;
            railStart.y = highest + runHeight;
            state = State.Run;
            Juice.Play(Sound.BombMark, zones[^1].point);
        }

        // Where to be at `time` to pass over each zone just as it blows.
        Vector3 RailPoint(float time) => railStart + runDirection * (zoneSpacing / blastGap * (time - railTime));

        Zone NewZone(Vector3 point, Vector3 normal, float blastAt)
        {
            var root = new GameObject("Bomb Zone").transform;
            root.SetPositionAndRotation(point + normal * 0.05f, Quaternion.FromToRotation(Vector3.up, normal));
            Disc(root, "Area", 0f, kit.dangerZone).localScale = new Vector3(zoneRadius * 2f, 0.01f, zoneRadius * 2f);
            var fill = Disc(root, "Fill", 0.03f, kit.dangerFill);
            fill.localScale = Vector3.zero;
            return new Zone { point = point, markedAt = Time.time, blastAt = blastAt, root = root, fill = fill };
        }

        static Transform Disc(Transform parent, string name, float lift, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * lift;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        // Fills each zone as its blast nears, fires a bolt into it just before, then blasts it.
        void TickZones()
        {
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                var zone = zones[i];
                float filled = Mathf.InverseLerp(zone.markedAt, zone.blastAt, Time.time) * zoneRadius * 2f;
                zone.fill.localScale = new Vector3(filled, 0.015f, filled);
                if (!zone.fired && Time.time >= zone.blastAt - BoltTime)
                {
                    zone.fired = true;
                    var from = transform.position + Vector3.down * 0.8f;
                    Projectile.Lob(from, (zone.point - from) / BoltTime, 0f, 0.9f, 0, kit.dangerFill, kit.groundMask);
                }
                if (Time.time < zone.blastAt) continue;
                Blast(zone);
                Destroy(zone.root.gameObject);
                zones.RemoveAt(i);
            }
        }

        // Hits the robot if any of it is inside the zone's column, on whatever part is nearest the blast.
        void Blast(Zone zone)
        {
            FxGrow.Spawn(PrimitiveType.Sphere, zone.point, new Vector3(zoneRadius * 2f, zoneRadius * 1.2f, zoneRadius * 2f), 0.35f, kit.dangerFill);
            FxSpark.Burst(zone.point + Vector3.up * 0.3f, 12, 12f, kit.dangerFill, 0.2f);
            Juice.Play(Sound.BombBlast, zone.point);
            Juice.ShakeAt(zone.point, 0.45f, 35f);
            var center = RobotCenter;
            var axis = new Vector3(zone.point.x, Mathf.Clamp(center.y, zone.point.y, zone.point.y + BlastHeight), zone.point.z);
            var nearest = robotHealth.NearestPoint(axis);
            var flat = new Vector2(nearest.x - zone.point.x, nearest.z - zone.point.z);
            if (flat.magnitude > zoneRadius || nearest.y < zone.point.y - 1f || nearest.y > zone.point.y + BlastHeight) return;
            robotHealth.HitNearest(axis, (nearest - axis).normalized, damage);
        }

        void OnDestroy()
        {
            foreach (var zone in zones)
                if (zone.root) Destroy(zone.root.gameObject);
        }
    }
}
