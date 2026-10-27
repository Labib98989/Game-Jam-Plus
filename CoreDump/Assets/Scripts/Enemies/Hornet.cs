using UnityEngine;

namespace CoreDump
{
    // The hardest one. It keeps its distance and never stops moving, darting from spot to spot
    // around the robot, and fires fast bursts aimed where the robot is heading.
    public class Hornet : Flyer
    {
        public float speed = 26f;
        [Tooltip("How close and how far from the robot it likes to be.")]
        public Vector2 distance = new(20f, 34f);
        public float range = 60f;
        public float shotSpeed = 62f;
        public float burstEvery = 1.1f;
        public int burstShots = 3;
        public float burstGap = 0.09f;

        Vector3 goal;
        float nextGoal, nextBurst, nextShot;
        int shotsLeft;
        Transform stinger;

        public static Hornet Spawn(EnemyKit kit, RobotBody robot, Vector3 position)
        {
            const float scale = 1.5f;
            var go = NewBody("Hornet", kit, position, Vector3.zero, new Vector3(2.6f, 1.6f, 2.6f) * scale);
            var t = Model(go, scale);
            Box(t, new Vector3(0f, 0f, 0.3f), new Vector3(0.8f, 0.7f, 0.8f), kit.skin);
            Box(t, new Vector3(0f, -0.05f, -0.5f), new Vector3(0.55f, 0.55f, 0.9f), kit.armor);
            Box(t, new Vector3(-0.8f, 0.3f, 0.1f), new Vector3(1.2f, 0.05f, 0.5f), kit.skin);
            Box(t, new Vector3(0.8f, 0.3f, 0.1f), new Vector3(1.2f, 0.05f, 0.5f), kit.skin);
            Box(t, new Vector3(-0.15f, 0.1f, 0.72f), Vector3.one * 0.12f, kit.eye);
            Box(t, new Vector3(0.15f, 0.1f, 0.72f), Vector3.one * 0.12f, kit.eye);
            var stinger = Box(t, new Vector3(0f, -0.3f, 0.75f), new Vector3(0.15f, 0.15f, 0.4f), kit.eye).transform;

            var hornet = go.AddComponent<Hornet>();
            hornet.maxHealth = 10f;
            hornet.radius = 1.1f;
            hornet.stinger = stinger;
            hornet.Init(kit, robot);
            hornet.nextBurst = Time.time + Random.Range(0.8f, 1.6f);
            return hornet;
        }

        protected override void Update()
        {
            base.Update();
            if (!Hunting) return;
            var target = RobotCenter;
            if (Time.time >= nextGoal || (goal - transform.position).sqrMagnitude < 9f) PickGoal(target);
            FlyTo(goal, speed, 70f);
            FaceAlong(target - transform.position, 10f);

            var from = stinger.position;
            if (shotsLeft == 0 && Time.time >= nextBurst)
            {
                nextBurst = Time.time + burstEvery;
                if ((target - from).sqrMagnitude < range * range && CanSee(from, target)) shotsLeft = burstShots;
            }
            if (shotsLeft > 0 && Time.time >= nextShot)
            {
                shotsLeft--;
                nextShot = Time.time + burstGap;
                Shoot(from, Lead(from, shotSpeed), shotSpeed, 0.35f, 1.5f);
                Juice.Play(Sound.EnemyShot, from);
            }
        }

        // A new spot somewhere around the robot, a good way round from where it is now.
        void PickGoal(Vector3 target)
        {
            nextGoal = Time.time + Random.Range(0.8f, 1.5f);
            var from = transform.position - target;
            float angle = Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg + Random.Range(40f, 100f) * (Random.value < 0.5f ? -1f : 1f);
            float reach = Random.Range(distance.x, distance.y);
            goal = target + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * reach + Vector3.up * Random.Range(6f, 14f);
        }
    }
}
