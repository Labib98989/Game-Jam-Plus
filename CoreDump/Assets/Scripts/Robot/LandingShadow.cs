using UnityEngine;

namespace CoreDump
{
    // A dark disc on the ground straight below the robot, so you can judge where you'll land.
    public class LandingShadow : MonoBehaviour
    {
        public Transform target;
        public LayerMask groundMask = ~0;
        public float maxDistance = 60f;
        public float size = 1.4f;

        Renderer disc;

        void Awake() => disc = GetComponent<Renderer>();

        void LateUpdate()
        {
            if (!target) return;
            var origin = target.position + Vector3.up * 0.5f;
            bool found = Physics.Raycast(origin, Vector3.down, out var hit, maxDistance, groundMask, QueryTriggerInteraction.Ignore);
            if (disc) disc.enabled = found;
            if (!found) return;

            // Shrinks a little with height, so it also tells you how high you are.
            float scale = size * Mathf.Lerp(1f, 0.5f, (origin.y - hit.point.y) / 25f);
            transform.SetPositionAndRotation(hit.point + hit.normal * 0.02f, Quaternion.FromToRotation(Vector3.up, hit.normal));
            transform.localScale = new Vector3(scale, 0.01f, scale);
        }
    }
}
