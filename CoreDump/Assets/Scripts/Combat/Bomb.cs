using UnityEngine;
using UnityEngine.Rendering;

namespace CoreDump
{
    // A dropped bomb: falls, stops where it lands, and goes off a moment after it was dropped.
    public class Bomb : MonoBehaviour
    {
        RobotBody owner;
        Vector3 velocity;
        float radius, damage, force, fuseEnds;
        bool landed;
        Material blastMaterial;

        public static void Drop(RobotBody owner, Vector3 position, Vector3 velocity, float fuse, float radius, float damage, float force, Material material, Material blastMaterial)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Bomb";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.55f;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var bomb = go.AddComponent<Bomb>();
            bomb.owner = owner;
            bomb.velocity = velocity;
            bomb.radius = radius;
            bomb.damage = damage;
            bomb.force = force;
            bomb.blastMaterial = blastMaterial;
            bomb.fuseEnds = Time.time + fuse;
        }

        void Update()
        {
            if (Time.time >= fuseEnds)
            {
                Explosion.Blast(transform.position, radius, damage, force, owner, blastMaterial);
                Destroy(gameObject);
                return;
            }
            // Blink faster as it's about to go off.
            float left = fuseEnds - Time.time;
            transform.localScale = Vector3.one * (Mathf.Repeat(Time.time, Mathf.Max(0.08f, left * 0.3f)) < 0.05f ? 0.7f : 0.55f);
            if (landed) return;

            velocity += Vector3.down * 30f * Time.deltaTime;
            var step = velocity * Time.deltaTime;
            if (Physics.SphereCast(transform.position, 0.25f, velocity.normalized, out var hit, step.magnitude, owner.shotMask, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + hit.normal * 0.28f;
                landed = true;
                return;
            }
            transform.position += step;
        }
    }
}
