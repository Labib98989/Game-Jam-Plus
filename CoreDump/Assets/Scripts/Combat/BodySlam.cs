using UnityEngine;

namespace CoreDump
{
    // Crashing into something that can take damage hurts it, and the faster the robot was going,
    // the more it hurts.
    [RequireComponent(typeof(Rigidbody))]
    public class BodySlam : MonoBehaviour
    {
        [Tooltip("Impacts slower than this do nothing. It sits above walking speed, so only blasts, boosts and falls count.")]
        public float minSpeed = 10f;
        public float damagePerSpeed = 1f;
        public float knockback = 0.6f;

        IDamageable lastTarget;
        float lastHitTime;

        void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minSpeed) return;
            var target = collision.collider.GetComponentInParent<IDamageable>();
            if (target == null) return;
            // Several of the robot's cubes can hit at once; it's still one slam.
            if (target == lastTarget && Time.time - lastHitTime < 0.25f) return;
            lastTarget = target;
            lastHitTime = Time.time;

            var direction = -collision.relativeVelocity.normalized;
            target.TakeHit((speed - minSpeed + 1f) * damagePerSpeed, direction);
            var at = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Juice.Play(Sound.Slam, at, 0.7f);
            Juice.Sparks(at, 8, 8f);
            Juice.Shake(0.3f);
            if (collision.rigidbody) collision.rigidbody.AddForce(direction * speed * knockback, ForceMode.Impulse);
        }
    }
}
