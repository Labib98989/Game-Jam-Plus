using UnityEngine;
using UnityEngine.AI;

namespace CoreDump
{
    // Big, slow and armored at the front. It plods after the robot, turning slowly, and fires heavy
    // shots the way it faces, its cannon glowing first. Hits on its front plate barely scratch it;
    // circle round and hit its back, which takes extra.
    public class Brute : Enemy
    {
        public float speed = 6f;
        [Tooltip("Degrees a second it turns. Slow enough to get round behind it.")]
        public float turnRate = 70f;
        public float range = 60f;
        public float interval = 1.4f;
        public float chargeTime = 0.45f;
        public float shotSpeed = 38f;
        [Tooltip("It only fires at the robot while the robot is within this many degrees of straight ahead.")]
        public float aimCone = 25f;
        [Tooltip("Share of the damage that gets through the front plate.")]
        public float frontArmor = 0.2f;
        [Tooltip("Damage taken from behind, compared to a normal hit.")]
        public float backBonus = 1.5f;

        static readonly Vector3 TipSize = new(0.5f, 0.5f, 0.2f);

        NavMeshAgent agent;
        Transform tip;
        float nextShot, chargeStart = -1f, nextPath;

        public static Brute Spawn(EnemyKit kit, RobotBody robot, Vector3 position)
        {
            bool onMesh = NavMesh.SamplePosition(position, out var nav, 5f, NavMesh.AllAreas);
            if (onMesh) position = nav.position;
            var go = NewBody("Brute", kit, position, new Vector3(0f, 2.2f, 0f), new Vector3(3.4f, 4.4f, 2.8f));
            var t = Model(go, 1f);
            Box(t, new Vector3(-0.7f, 0.8f, 0f), new Vector3(0.8f, 1.6f, 0.8f), kit.skin);
            Box(t, new Vector3(0.7f, 0.8f, 0f), new Vector3(0.8f, 1.6f, 0.8f), kit.skin);
            Box(t, new Vector3(0f, 2.5f, 0f), new Vector3(2.6f, 2f, 1.8f), kit.skin);
            Box(t, new Vector3(0f, 3.8f, 0.3f), new Vector3(0.9f, 0.7f, 0.8f), kit.skin);
            Box(t, new Vector3(-0.2f, 3.85f, 0.72f), new Vector3(0.18f, 0.12f, 0.06f), kit.eye);
            Box(t, new Vector3(0.2f, 3.85f, 0.72f), new Vector3(0.18f, 0.12f, 0.06f), kit.eye);
            Box(t, new Vector3(0f, 2.5f, 1.05f), new Vector3(2.9f, 2.3f, 0.3f), kit.armor);
            Box(t, new Vector3(1.75f, 2.7f, 0.6f), new Vector3(0.6f, 0.6f, 1.9f), kit.armor);
            var tip = Box(t, new Vector3(1.75f, 2.7f, 1.6f), TipSize, kit.eye).transform;

            var brute = go.AddComponent<Brute>();
            brute.maxHealth = 40f;
            brute.damage = 2;
            brute.tip = tip;
            brute.Init(kit, robot);
            if (onMesh)
            {
                var a = go.AddComponent<NavMeshAgent>();
                a.speed = brute.speed;
                a.acceleration = 20f;
                a.radius = 1.4f;
                a.height = 4.4f;
                a.stoppingDistance = 10f;
                a.updateRotation = false;
                brute.agent = a;
            }
            brute.nextShot = Time.time + Random.Range(1f, 2f);
            return brute;
        }

        // The plate takes most of a hit from the front; the back takes extra.
        public override void TakeHit(float amount, Vector3 direction)
        {
            float along = Vector3.Dot(direction.normalized, transform.forward);
            if (along < -0.4f) amount *= frontArmor;
            else if (along > 0.4f) amount *= backBonus;
            base.TakeHit(amount, direction);
        }

        protected override void Update()
        {
            base.Update();
            if (!Hunting) return;

            var target = RobotCenter;
            var flat = target - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), turnRate * Time.deltaTime);
            if (agent && agent.enabled && agent.isOnNavMesh && Time.time >= nextPath)
            {
                agent.SetDestination(robot.transform.position);
                nextPath = Time.time + 0.5f;
            }

            if (chargeStart >= 0f)
            {
                float k = (Time.time - chargeStart) / chargeTime;
                tip.localScale = TipSize * (1f + Mathf.Min(k, 1f) * 1.5f);
                if (k < 1f) return;
                Fire();
                chargeStart = -1f;
                tip.localScale = TipSize;
                nextShot = Time.time + interval;
                return;
            }
            if (Time.time < nextShot) return;
            bool inFront = Vector3.Angle(transform.forward, flat) < aimCone;
            if (inFront && (target - tip.position).sqrMagnitude < range * range && CanSee(tip.position, target)) chargeStart = Time.time;
            else nextShot = Time.time + 0.3f;
        }

        // A heavy shot the way it faces, bent toward the robot only as far as the cone allows.
        void Fire()
        {
            var from = tip.position + transform.forward * 0.4f;
            var aim = Lead(from, shotSpeed) - from;
            var direction = Vector3.RotateTowards(transform.forward, aim.normalized, aimCone * Mathf.Deg2Rad, 0f);
            Projectile.Lob(from, direction * shotSpeed, 0f, 1.3f, damage, kit.shot, kit.shotMask);
            Juice.Play(Sound.Cannon, from);
            Juice.ShakeAt(from, 0.2f, 40f);
        }
    }
}
