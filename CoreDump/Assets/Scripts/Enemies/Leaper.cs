using UnityEngine;
using UnityEngine.AI;

namespace CoreDump
{
    // An enemy that runs along the floor and leaps at the robot: Skitters and Stalkers. Leaps also
    // carry it up onto ledges, and off the walking map it gets around by leaping.
    public abstract class Leaper : Enemy
    {
        public float speed = 20f;
        [Tooltip("How far it leaps, at most. It also leaps whenever it's off the walking map.")]
        public float leapRange = 12f;
        public float leapTime = 0.5f;
        [Tooltip("The pause before a leap, so you can see it coming.")]
        public float windUp = 0.25f;
        public float leapCooldown = 1f;
        [Tooltip("A leap that passes this close to the robot hits it.")]
        public float biteReach = 1.6f;
        [Tooltip("How far below the robot it will still leap from.")]
        public float leapDown = 4f;
        [Tooltip("How long it sits stunned after a leap that misses. Zero for none.")]
        public float stuckOnMiss;
        [Tooltip("Its footprint on the walking map, for pushing past others.")]
        public float agentRadius = 0.8f;

        // Busy is for whatever a kind does besides hunting, like a Skitter stealing a part.
        protected enum Mode { Run, WindUp, Leap, Stuck, Busy }

        protected Mode mode;
        protected NavMeshAgent agent;
        protected float nextPath;
        float modeUntil, nextLeap;
        Vector3 leapFrom, leapTo;
        float leapStart, leapArc;
        bool landedHit;

        protected bool OnMesh => agent && agent.enabled && agent.isOnNavMesh;

        // Where a leaper should stand: on the walking map if there's one close by.
        protected static bool OnWalkingMap(ref Vector3 position)
        {
            if (!NavMesh.SamplePosition(position, out var hit, 4f, NavMesh.AllAreas)) return false;
            position = hit.position;
            return true;
        }

        protected void Begin(bool onMesh)
        {
            if (onMesh) agent = AddAgent();
            nextLeap = Time.time + Random.Range(0.3f, 1f);
        }

        NavMeshAgent AddAgent()
        {
            var a = gameObject.AddComponent<NavMeshAgent>();
            a.speed = speed;
            a.acceleration = 70f;
            a.angularSpeed = 900f;
            a.radius = agentRadius;
            a.height = 1.5f;
            a.stoppingDistance = 0.4f;
            a.autoBraking = false;
            a.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            return a;
        }

        protected override void Update()
        {
            base.Update();
            if (!Hunting) return;
            switch (mode)
            {
                case Mode.Run:
                    Run();
                    break;
                case Mode.WindUp:
                    Face(robotHealth.NearestPoint(transform.position));
                    if (Time.time >= modeUntil) StartLeap();
                    break;
                case Mode.Leap:
                    Leap();
                    break;
                case Mode.Stuck:
                    if (Time.time >= modeUntil) mode = Mode.Run;
                    break;
                case Mode.Busy:
                    Busy();
                    break;
            }
        }

        protected virtual void Busy() { }

        // What it sounds like as it crouches to leap.
        protected virtual Sound LeapSound => Sound.Chitter;

        protected virtual void Run()
        {
            var to = robotHealth.NearestPoint(transform.position) - transform.position;
            float flat = new Vector2(to.x, to.z).magnitude;
            // Close enough to jump at it, or off the walking map with nowhere to run: leap.
            if (Time.time >= nextLeap && to.y < leapRange && to.y > -leapDown && (flat < leapRange || !OnMesh))
            {
                if (OnMesh) agent.ResetPath();
                mode = Mode.WindUp;
                modeUntil = Time.time + windUp;
                Juice.Play(LeapSound, transform.position);
                return;
            }
            RunTo(robot.transform.position, 0.25f);
        }

        // Heads for a point along the walking map, finding a new path every `every` seconds.
        protected void RunTo(Vector3 point, float every)
        {
            if (!OnMesh || Time.time < nextPath) return;
            agent.SetDestination(point);
            nextPath = Time.time + every;
        }

        void StartLeap()
        {
            var target = robotHealth.NearestPoint(transform.position);
            var to = target - transform.position;
            if (to.magnitude > leapRange) target = transform.position + to.normalized * leapRange;
            leapFrom = transform.position;
            leapTo = target;
            leapArc = 1f + Mathf.Max(0f, to.y) * 0.5f + to.magnitude * 0.05f;
            leapStart = Time.time;
            landedHit = false;
            if (agent) agent.enabled = false;
            mode = Mode.Leap;
        }

        void Leap()
        {
            float t = Mathf.Clamp01((Time.time - leapStart) / leapTime);
            var p = Vector3.Lerp(leapFrom, leapTo, t) + Vector3.up * (leapArc * 4f * t * (1f - t));
            transform.position = p;
            Face(leapTo);
            if (robotHealth.DistanceTo(p + Vector3.up * 0.5f) < biteReach)
            {
                robotHealth.HitNearest(p, (leapTo - leapFrom).normalized, damage);
                landedHit = true;
                Land();
                return;
            }
            if (t >= 1f) Land();
        }

        // Drops onto whatever is below, and back onto the walking map if there's one nearby. A miss
        // leaves some kinds stuck for a moment.
        protected void Land()
        {
            var p = transform.position;
            if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 60f, kit.groundMask, QueryTriggerInteraction.Ignore)) p = hit.point;
            transform.position = p;
            if (NavMesh.SamplePosition(p, out var nav, 2.5f, NavMesh.AllAreas))
            {
                transform.position = nav.position;
                if (!agent) agent = AddAgent();
                agent.enabled = true;
                agent.Warp(nav.position);
            }
            nextLeap = Time.time + leapCooldown;
            bool stuck = !landedHit && stuckOnMiss > 0f;
            mode = stuck ? Mode.Stuck : Mode.Run;
            if (stuck) modeUntil = Time.time + stuckOnMiss;
        }

        protected void Face(Vector3 point)
        {
            var flat = point - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat);
        }
    }
}
