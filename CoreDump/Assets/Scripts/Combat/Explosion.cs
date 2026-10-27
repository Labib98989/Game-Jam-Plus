using System.Collections.Generic;
using UnityEngine;

namespace CoreDump
{
    // Splash damage and knockback around a point. Your own explosions never hurt you, but they do
    // push you, so a rocket or bomb at your feet is a rocket jump.
    public static class Explosion
    {
        public static void Blast(Vector3 center, float radius, float damage, float force, RobotBody owner, Material material)
        {
            var hit = new HashSet<IDamageable>();
            foreach (var collider in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (owner && collider.attachedRigidbody && collider.attachedRigidbody.gameObject == owner.gameObject) continue;
                float falloff = 1f - Mathf.Clamp01(Vector3.Distance(center, collider.ClosestPoint(center)) / radius) * 0.6f;
                var target = collider.GetComponentInParent<IDamageable>();
                if (target != null && hit.Add(target)) target.TakeHit(damage * falloff, (collider.transform.position - center).normalized);
                if (collider.attachedRigidbody && !collider.attachedRigidbody.isKinematic)
                    collider.attachedRigidbody.AddExplosionForce(force, center, radius, 0.5f, ForceMode.VelocityChange);
            }

            if (owner)
            {
                var ownerCenter = owner.transform.TransformPoint(owner.LocalBounds.center);
                var away = ownerCenter - center;
                float reach = radius + owner.LocalBounds.extents.magnitude * 0.5f;
                if (away.magnitude < reach)
                {
                    float strength = 1f - away.magnitude / reach;
                    owner.Motor.Kick(away.normalized * force * (0.4f + 0.6f * strength));
                }
            }

            FxGrow.Spawn(PrimitiveType.Sphere, center, Vector3.one * radius * 2f, 0.25f, material);
            Juice.Play(Sound.Explosion, center);
            Juice.Sparks(center, 14, 12f, 0.12f);
            Juice.ShakeAt(center, 0.6f, radius * 6f);
        }
    }
}
