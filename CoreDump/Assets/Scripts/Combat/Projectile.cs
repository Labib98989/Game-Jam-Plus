using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoreDump
{
    // A slow enemy shot you can see coming: straight, or lobbed in an arc. A hit on the robot goes
    // to RobotHealth: a part wears down, or the body loses health.
    public class Projectile : MonoBehaviour
    {
        public float radius = 0.25f;
        public LayerMask hitMask = ~0;

        static readonly List<Projectile> live = new();

        Vector3 velocity;
        float gravity;
        int damage = 1;
        float dieTime;

        public static Projectile Fire(Vector3 position, Vector3 direction, float speed, Material material, LayerMask hitMask) =>
            Lob(position, direction.normalized * speed, 0f, 0.5f, 1, material, hitMask);

        // A shot that falls under `gravity` (meters per second squared) as it flies.
        public static Projectile Lob(Vector3 position, Vector3 velocity, float gravity, float size, int damage, Material material, LayerMask hitMask)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Enemy Shot";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var shot = go.AddComponent<Projectile>();
            shot.radius = size * 0.5f;
            shot.velocity = velocity;
            shot.gravity = gravity;
            shot.damage = damage;
            shot.hitMask = hitMask;
            shot.dieTime = Time.time + 6f;
            return shot;
        }

        void OnEnable() => live.Add(this);
        void OnDisable() => live.Remove(this);

        // Every shot still flying, gone, when the robot leaves a room.
        public static void ClearAll()
        {
            foreach (var shot in live.ToList())
                if (shot) Destroy(shot.gameObject);
            live.Clear();
        }

        void Update()
        {
            velocity += Vector3.down * (gravity * Time.deltaTime);
            var step = velocity * Time.deltaTime;
            if (Physics.SphereCast(transform.position, radius, velocity.normalized, out var hit, step.magnitude, hitMask, QueryTriggerInteraction.Ignore))
            {
                var robot = hit.collider.GetComponentInParent<RobotHealth>();
                if (robot) robot.TakeHit(hit.collider, velocity.normalized, damage);
                else
                {
                    // A splash where it hits the room.
                    FxGrow.Spawn(PrimitiveType.Sphere, hit.point, Vector3.one * radius * 5f, 0.15f, GetComponent<Renderer>().sharedMaterial);
                    Juice.Play(Sound.Splat, hit.point);
                }
                Destroy(gameObject);
                return;
            }
            transform.position += step;
            if (Time.time > dieTime) Destroy(gameObject);
        }
    }
}
