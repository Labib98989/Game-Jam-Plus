using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CoreDump
{
    // Clings to ledges, walls and ceilings and lobs globs at the robot, aimed where it's heading. Its
    // mouth swells and glows before it fires, so you can tell where shots come from. Every so often
    // it hops to another perch. Get up high to reach it, or shoot it from afar.
    public class Spitter : Enemy
    {
        public float range = 70f;
        public float interval = 1.2f;
        [Tooltip("How long the mouth glows before a glob comes out.")]
        public float chargeTime = 0.4f;
        [Tooltip("How long a glob takes to reach the robot, at 25 m. Nearer is quicker.")]
        public float flightTime = 0.75f;
        public float globGravity = 18f;
        [Tooltip("Seconds between hops to another perch, picked at random in this range.")]
        public Vector2 hopEvery = new(8f, 13f);
        public float hopTime = 0.9f;
        public float hopRange = 30f;

        static readonly Vector3 MouthSize = new(0.4f, 0.2f, 0.4f);
        // Perches with a Spitter on them.
        static readonly HashSet<RoomPiece> taken = new();

        IReadOnlyList<RoomPiece> perches;
        RoomPiece perch;
        Transform mouth;
        float nextShot, nextHop, chargeStart = -1f, hopStart;
        Vector3 hopFrom, hopTo;
        Quaternion hopFromRotation, hopToRotation;
        bool hopping;

        public static bool IsTaken(RoomPiece spot) => taken.Contains(spot);

        public static Spitter Spawn(EnemyKit kit, RobotBody robot, RoomPiece perch, IReadOnlyList<RoomPiece> perches)
        {
            // Built standing up, its feet at the origin; Settle stands it on the perch's surface.
            const float scale = 1.8f;
            var go = NewBody("Spitter", kit, Vector3.zero, new Vector3(0f, 0.7f, 0f) * scale, new Vector3(1.8f, 1.6f, 1.8f) * scale);
            var t = Model(go, scale);
            Box(t, new Vector3(0f, 0.45f, 0f), new Vector3(1.1f, 0.8f, 1.1f), kit.skin);
            Box(t, new Vector3(0f, 0.95f, 0f), new Vector3(0.7f, 0.4f, 0.7f), kit.skin);
            var mouth = Box(t, new Vector3(0f, 1.2f, 0f), MouthSize, kit.eye).transform;
            for (int i = 0; i < 4; i++)
            {
                var corner = Quaternion.Euler(0f, 45f + i * 90f, 0f) * new Vector3(0f, 0.1f, 0.7f);
                Box(t, corner, new Vector3(0.2f, 0.2f, 0.2f), kit.skin);
            }

            var spitter = go.AddComponent<Spitter>();
            spitter.maxHealth = 8f;
            spitter.mouth = mouth;
            spitter.perches = perches;
            spitter.Init(kit, robot);
            spitter.Settle(perch);
            spitter.nextShot = Time.time + Random.Range(1f, 2.5f);
            spitter.nextHop = Time.time + Random.Range(spitter.hopEvery.x, spitter.hopEvery.y);
            return spitter;
        }

        void Settle(RoomPiece spot)
        {
            if (perch != null) taken.Remove(perch);
            perch = spot;
            taken.Add(spot);
            var (position, rotation) = Pose(spot);
            transform.SetPositionAndRotation(position, rotation);
        }

        // Standing on the surface the perch was put on, its top facing away from it, turned toward the room's middle.
        static (Vector3 position, Quaternion rotation) Pose(RoomPiece spot)
        {
            var up = (Vector3)spot.face;
            var position = spot.Center - up * 0.5f;
            var forward = Vector3.ProjectOnPlane(-position, up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.right;
            return (position, Quaternion.LookRotation(forward.normalized, up));
        }

        protected override void Update()
        {
            base.Update();
            if (!Hunting) return;
            if (hopping)
            {
                Hop();
                return;
            }
            var target = RobotCenter;
            if (chargeStart >= 0f)
            {
                float k = (Time.time - chargeStart) / chargeTime;
                mouth.localScale = MouthSize * (1f + Mathf.Min(k, 1f) * 1.2f);
                if (k < 1f) return;
                Spit(target);
                chargeStart = -1f;
                mouth.localScale = MouthSize;
                nextShot = Time.time + interval;
                return;
            }
            if (Time.time >= nextHop && TryHop()) return;
            if (Time.time < nextShot) return;
            if ((target - mouth.position).sqrMagnitude < range * range && CanSee(mouth.position, target)) chargeStart = Time.time;
            else nextShot = Time.time + 0.5f;
        }

        // Lobs a glob at where the robot will be if it keeps going. Changing direction dodges it.
        void Spit(Vector3 target)
        {
            var from = mouth.position;
            float time = flightTime * Mathf.Clamp(Vector3.Distance(from, target) / 25f, 0.6f, 1.6f);
            target += robot.Motor.Velocity * time;
            var velocity = (target - from) / time + Vector3.up * (0.5f * globGravity * time);
            Projectile.Lob(from, velocity, globGravity, 0.9f, damage, kit.shot, kit.shotMask);
            Juice.Play(Sound.Glob, from);
        }

        bool TryHop()
        {
            nextHop = Time.time + Random.Range(hopEvery.x, hopEvery.y);
            if (perches == null) return false;
            var options = perches.Where(p => p != perch && !taken.Contains(p) && (p.Center - perch.Center).sqrMagnitude < hopRange * hopRange).ToList();
            if (options.Count == 0) return false;
            var next = options[Random.Range(0, options.Count)];
            taken.Remove(perch);
            taken.Add(next);
            perch = next;
            hopFrom = transform.position;
            hopFromRotation = transform.rotation;
            (hopTo, hopToRotation) = Pose(next);
            hopStart = Time.time;
            hopping = true;
            return true;
        }

        void Hop()
        {
            float t = Mathf.Clamp01((Time.time - hopStart) / hopTime);
            float arc = 2f + Vector3.Distance(hopFrom, hopTo) * 0.15f;
            transform.SetPositionAndRotation(Vector3.Lerp(hopFrom, hopTo, t) + Vector3.up * (arc * 4f * t * (1f - t)),
                Quaternion.Slerp(hopFromRotation, hopToRotation, t));
            if (t < 1f) return;
            hopping = false;
            nextShot = Time.time + 1f;
        }

        void OnDestroy()
        {
            if (perch != null) taken.Remove(perch);
        }
    }
}
