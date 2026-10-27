using UnityEngine;

namespace CoreDump
{
    // The robot's one health bar, and parts wearing down. A hit on the body lowers health. A hit on
    // a part wears it down instead, and when it breaks it comes off in one chunk with anything it
    // was holding up. RobotHud draws the bar; each part shows its own wear (PartWear).
    [RequireComponent(typeof(RobotBody))]
    public class RobotHealth : MonoBehaviour
    {
        public int maxHealth = 10;
        [Tooltip("After a hit on the body, more body hits are ignored for this long. Parts take every hit.")]
        public float invulnerableTime = 0.3f;
        [Tooltip("How fast a knocked-off part flies away from the hit.")]
        public float knockOffSpeed = 7f;
        [Tooltip("Sparks from worn parts and parts breaking off.")]
        public Material sparkMaterial;
        [Tooltip("Smoke from parts about to break off.")]
        public Material smokeMaterial;
        [Tooltip("The huge glow around a knocked-off head, seen through walls (the CoreDump/Beacon shader).")]
        public Material beaconMaterial;

        public int Health { get; private set; }
        public bool IsDead => Health <= 0;
        // When health runs out. With nobody listening (the test room), the robot just respawns.
        public event System.Action Died;

        RobotBody robot;
        float invulnerableUntil;
        // The robot's solid colliders, found again whenever its parts change.
        Collider[] colliders;

        void Awake()
        {
            robot = GetComponent<RobotBody>();
            robot.Changed += () => colliders = null;
            Health = maxHealth;
        }

        Collider[] Colliders => colliders ??= GetComponentsInChildren<Collider>();

        public void ResetHealth()
        {
            Health = maxHealth;
            invulnerableUntil = 0f;
        }

        public void Heal(int amount) => Health = Mathf.Min(maxHealth, Health + amount);

        // Every part back to full health.
        public void RepairParts()
        {
            foreach (var part in robot.Parts) part.Repair();
        }

        // `hit` is the robot collider that was struck: a body cube or one of a part's cubes. With no
        // collider (a hazard), the body takes it.
        public void TakeHit(Collider hit, Vector3 direction, int damage = 1)
        {
            if (IsDead) return;
            var part = hit ? hit.GetComponentInParent<PartModule>() : null;
            if (part && part.Robot == robot)
            {
                if (part.Hit(damage)) KnockOff(part, direction);
                else
                {
                    Juice.Play(Sound.HullHit, null, 0.4f);
                    Juice.Shake(0.15f);
                }
                return;
            }
            if (Time.time < invulnerableUntil) return;
            invulnerableUntil = Time.time + invulnerableTime;
            Health = Mathf.Max(0, Health - damage);
            Juice.Play(Sound.HullHit);
            Juice.Shake(0.5f);
            Juice.Stop(0.05f);
            Juice.Hurt(1f);
            if (Health > 0) return;
            if (Died != null) Died();
            else GetComponent<StartingLoadout>().Respawn();
        }

        // A bite or a slash: whatever part of the robot is closest to `point` takes it.
        public void HitNearest(Vector3 point, Vector3 direction, int damage = 1) => TakeHit(Nearest(point, out _), direction, damage);

        // The point on the robot closest to `point`.
        public Vector3 NearestPoint(Vector3 point)
        {
            Nearest(point, out var closest);
            return closest;
        }

        public float DistanceTo(Vector3 point) => Vector3.Distance(NearestPoint(point), point);

        Collider Nearest(Vector3 point, out Vector3 closest)
        {
            Collider nearest = null;
            closest = transform.position;
            float best = float.MaxValue;
            foreach (var c in Colliders)
            {
                if (!c || !c.enabled || c.isTrigger) continue;
                var p = c.ClosestPoint(point);
                float distance = (p - point).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = c;
                closest = p;
            }
            return nearest;
        }

        public void KnockOff(PartModule part, Vector3 direction)
        {
            if (sparkMaterial) FxSpark.Burst(part.transform.position, 10, 7f, sparkMaterial, 0.1f);
            Juice.Play(Sound.PartLost);
            Juice.Shake(0.4f);
            Juice.Stop(0.06f);
            Juice.Hurt(0.6f);
            // Whatever it held up comes off with it in one piece, so it all snaps back on together.
            var falling = robot.FindDisconnected(part);
            falling.Insert(0, part);
            PartChunk.Spawn(robot, falling, Away(part, direction) * knockOffSpeed + Vector3.up * 5f, beaconMaterial);
        }

        // Flat direction from the body out through the part, so it never lands back on the robot.
        Vector3 Away(PartModule part, Vector3 hitDirection)
        {
            var away = Vector3.ProjectOnPlane(part.transform.position - (robot.transform.position + Vector3.up), Vector3.up);
            if (away.sqrMagnitude < 0.01f) away = Vector3.ProjectOnPlane(hitDirection, Vector3.up);
            return away.normalized;
        }
    }
}
