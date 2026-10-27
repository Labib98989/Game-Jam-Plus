using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoreDump
{
    // An alien. It flashes when hit and dies when its health runs out. Every live one is listed, so
    // the waves know when a room is clear. Each kind builds its own body and decides what it does.
    public abstract class Enemy : Target
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly List<Enemy> live = new();

        public float maxHealth = 4f;
        [Tooltip("How much each attack takes off the robot: hits on a part, or health off the body.")]
        public int damage = 1;

        protected EnemyKit kit;
        protected RobotBody robot;
        protected RobotHealth robotHealth;
        float health;
        float flashUntil;
        bool flashing;
        MaterialPropertyBlock flash;

        // The room the fight is in, inside its walls and under its ceiling. Flyers stay inside it.
        public static Bounds Arena = new(Vector3.zero, Vector3.one * 100f);

        public static IReadOnlyList<Enemy> Live => live;
        public static int AliveCount => live.Count(e => e && e.IsAlive);
        public override bool IsAlive => health > 0f;
        // Whether there's a robot to go after.
        protected bool Hunting => IsAlive && robot && robotHealth && !robotHealth.IsDead;
        protected Vector3 RobotCenter => robot.transform.TransformPoint(robot.LocalBounds.center);

        protected void Init(EnemyKit kit, RobotBody robot)
        {
            this.kit = kit;
            this.robot = robot;
            robotHealth = robot ? robot.GetComponent<RobotHealth>() : null;
            health = maxHealth;
            renderers = GetComponentsInChildren<Renderer>();
            flash = new MaterialPropertyBlock();
            flash.SetColor(BaseColor, Color.white);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            live.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            live.Remove(this);
        }

        public override void TakeHit(float amount, Vector3 direction)
        {
            if (!IsAlive) return;
            health -= amount;
            flashUntil = Time.time + 0.07f;
            Juice.Play(Sound.EnemyHit);
            Juice.Hit(health <= 0f);
            Juice.Sparks(AimPoint, 3, 6f);
            if (health <= 0f) Die();
        }

        // Bursts, bigger and louder for the big ones, which also land with a shake and a split-second freeze.
        protected virtual void Die()
        {
            bool big = maxHealth >= 14f;
            float size = GetComponent<BoxCollider>() is { } box ? box.size.magnitude * 0.4f : 1.5f;
            FxGrow.Spawn(PrimitiveType.Sphere, AimPoint, Vector3.one * 1.8f, 0.3f, kit.spawnFx);
            Juice.Goo(AimPoint, size);
            Juice.Play(big ? Sound.BigDie : Sound.EnemyDie, AimPoint);
            if (big)
            {
                Juice.ShakeAt(AimPoint, 0.4f, 60f);
                Juice.Stop(0.04f);
            }
            Destroy(gameObject);
        }

        protected virtual void Update()
        {
            bool lit = Time.time < flashUntil;
            if (lit == flashing) return;
            flashing = lit;
            foreach (var r in renderers)
                if (r) r.SetPropertyBlock(lit ? flash : null);
        }

        // Every enemy gone at once, when the robot leaves a room.
        public static void ClearAll()
        {
            foreach (var enemy in live.ToList())
                if (enemy) Destroy(enemy.gameObject);
            live.Clear();
        }

        // Where a shot of `speed` fired from `from` meets the robot if it keeps moving the way it is.
        protected Vector3 Lead(Vector3 from, float speed)
        {
            var target = RobotCenter;
            float time = Vector3.Distance(from, target) / Mathf.Max(1f, speed);
            return target + robot.Motor.Velocity * time;
        }

        // A shot straight at `target`, with a little spread so bursts fan out.
        protected void Shoot(Vector3 from, Vector3 target, float speed, float size, float spread = 0f)
        {
            var direction = (target - from).normalized;
            if (spread > 0f) direction = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f) * direction;
            Projectile.Lob(from, direction * speed, 0f, size, damage, kit.shot, kit.shotMask);
        }

        // Whether a straight line from `from` reaches the robot without hitting the room first.
        protected bool CanSee(Vector3 from, Vector3 to)
        {
            if (!Physics.Linecast(from, to, out var hit, kit.shotMask, QueryTriggerInteraction.Ignore)) return true;
            return hit.rigidbody && hit.rigidbody.gameObject == robot.gameObject;
        }

        // An enemy's root: on the enemy layer, with a hitbox bigger than it looks (hits lean the
        // player's way) and a kinematic body, since it moves itself.
        protected static GameObject NewBody(string name, EnemyKit kit, Vector3 position, Vector3 hitboxCenter, Vector3 hitboxSize)
        {
            var go = new GameObject(name) { layer = kit.layer };
            go.transform.position = position;
            var hitbox = go.AddComponent<BoxCollider>();
            hitbox.center = hitboxCenter;
            hitbox.size = hitboxSize;
            go.AddComponent<Rigidbody>().isKinematic = true;
            return go;
        }

        // What the body's blocks hang off, scaled up or down as a whole. The root stays at scale 1,
        // so the walking map and the hitbox aren't scaled with it.
        protected static Transform Model(GameObject root, float scale)
        {
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            model.localScale = Vector3.one * scale;
            return model;
        }

        // A plain block of the body, with no collider of its own.
        protected static GameObject Box(Transform parent, Vector3 localPosition, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(go.GetComponent<Collider>());
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }
    }
}
