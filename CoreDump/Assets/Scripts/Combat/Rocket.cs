using UnityEngine;
using UnityEngine.Rendering;

namespace CoreDump
{
    // A rocket that explodes on whatever it touches first, or after a few seconds. It flies straight,
    // or turns toward a target if it was given one. It counts anything passing close to it as a
    // hit, so near misses still land.
    public class Rocket : MonoBehaviour
    {
        RobotBody owner;
        Vector3 velocity;
        float radius, damage, force, dieTime;
        Material blastMaterial;
        Target target;
        float turnRate;

        // `turnRate` is in degrees per second; with no `target` the rocket flies straight.
        public static void Fire(RobotBody owner, Vector3 position, Vector3 direction, float speed, float radius, float damage, float force,
            Material material, Material blastMaterial, Target target = null, float turnRate = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Rocket";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction) * Quaternion.Euler(90f, 0f, 0f));
            go.transform.localScale = new Vector3(0.3f, 0.4f, 0.3f);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var rocket = go.AddComponent<Rocket>();
            rocket.owner = owner;
            rocket.velocity = direction.normalized * speed;
            rocket.radius = radius;
            rocket.damage = damage;
            rocket.force = force;
            rocket.blastMaterial = blastMaterial;
            rocket.dieTime = Time.time + 4f;
            rocket.target = target;
            rocket.turnRate = turnRate;
        }

        void Update()
        {
            if (target && target.IsAlive)
            {
                velocity = Vector3.RotateTowards(velocity, target.AimPoint - transform.position, turnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
                transform.rotation = Quaternion.LookRotation(velocity) * Quaternion.Euler(90f, 0f, 0f);
            }
            var step = velocity * Time.deltaTime;
            if (Physics.SphereCast(transform.position, 0.35f, velocity.normalized, out var hit, step.magnitude, owner.shotMask, QueryTriggerInteraction.Ignore))
            {
                Explode(hit.point);
                return;
            }
            transform.position += step;
            if (Time.time > dieTime) Explode(transform.position);
        }

        void Explode(Vector3 at)
        {
            Explosion.Blast(at, radius, damage, force, owner, blastMaterial);
            Destroy(gameObject);
        }
    }
}
