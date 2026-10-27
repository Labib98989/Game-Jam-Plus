using UnityEngine;

namespace CoreDump
{
    // An enemy that flies: Bombers and Hornets. It steers toward a goal, swerves around walls instead
    // of flying through them, and stays inside the room, under its ceiling.
    public abstract class Flyer : Enemy
    {
        [Tooltip("How big it is, for not flying into walls.")]
        public float radius = 1f;

        protected Vector3 velocity;

        // Accelerates toward `goal` at up to `speed`, swerving off walls in the way.
        protected void FlyTo(Vector3 goal, float speed, float acceleration)
        {
            var to = InArena(goal) - transform.position;
            var desired = to.sqrMagnitude > 0.25f ? to.normalized * speed : Vector3.zero;
            if (velocity.sqrMagnitude > 1f &&
                Physics.SphereCast(transform.position, radius, velocity.normalized, out var hit, velocity.magnitude * 0.5f + 3f, kit.groundMask, QueryTriggerInteraction.Ignore))
                desired = Vector3.ProjectOnPlane(desired, hit.normal) + hit.normal * (speed * 0.6f);
            velocity = Vector3.MoveTowards(velocity, desired, acceleration * Time.deltaTime);
            Move(velocity * Time.deltaTime);
        }

        // Moves, stopping at walls rather than going through them. True if it hit one.
        protected bool Move(Vector3 step)
        {
            float length = step.magnitude;
            if (length < 0.0001f) return false;
            bool blocked = Physics.SphereCast(transform.position, radius, step / length, out var hit, length, kit.groundMask, QueryTriggerInteraction.Ignore);
            if (blocked)
            {
                step = step / length * Mathf.Max(0f, hit.distance - 0.05f);
                velocity = Vector3.ProjectOnPlane(velocity, hit.normal);
            }
            transform.position = InArena(transform.position + step);
            return blocked;
        }

        protected void FaceAlong(Vector3 direction, float rate)
        {
            if (direction.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction.normalized, Vector3.up), rate * Time.deltaTime);
        }

        protected static Vector3 InArena(Vector3 p)
        {
            var min = Arena.min + new Vector3(2f, 1f, 2f);
            var max = Arena.max - new Vector3(2f, 2f, 2f);
            return new Vector3(Mathf.Clamp(p.x, min.x, max.x), Mathf.Clamp(p.y, min.y, max.y), Mathf.Clamp(p.z, min.z, max.z));
        }
    }
}
