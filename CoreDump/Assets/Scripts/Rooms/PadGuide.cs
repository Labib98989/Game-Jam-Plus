using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoreDump
{
    // Shows where a pad throws you: arrowheads stream from the pad along the path the robot will
    // fly, each turned to face the camera so it reads from any side, and a launch pad marks the spot
    // where you'll come down. The path is worked out once, with the robot's own gravity, when the
    // pad appears. A jump pad's path stops at the top, since it comes straight back down.
    public class PadGuide : MonoBehaviour
    {
        const float Spacing = 3.2f;
        const float Speed = 7f;
        const float ArrowSize = 1.4f;
        const int MaxArrows = 12;
        const float TraceStep = 0.025f, TraceTime = 4f;

        public Vector3 launch;
        public Material material;
        [Tooltip("Height of the pad's surface above its middle, where the path starts.")]
        public float surface;

        static RobotMotor motor;

        readonly List<Vector3> path = new();
        readonly List<float> along = new();
        readonly List<Mesh> meshes = new();
        Transform[] arrows;
        Transform landing;

        float Length => along.Count > 0 ? along[^1] : 0f;

        void Start()
        {
            if (!motor) motor = FindAnyObjectByType<RobotMotor>(FindObjectsInactive.Include);
            Trace();
            if (Length < 1f) return;
            var chevron = Own(RoomBuilder.ChevronMesh(ArrowSize));
            arrows = new Transform[Mathf.Clamp(Mathf.CeilToInt(Length / Spacing), 2, MaxArrows)];
            for (int i = 0; i < arrows.Length; i++) arrows[i] = Part("Arrow", chevron);
        }

        void Trace()
        {
            float gravity = motor ? motor.gravity : 32f;
            float fall = motor ? motor.fallGravityMultiplier : 1.4f;
            float maxFall = motor ? motor.maxFallSpeed : 50f;
            int mask = Physics.DefaultRaycastLayers & ~(motor ? 1 << motor.gameObject.layer : 0);
            bool straightUp = new Vector2(launch.x, launch.z).sqrMagnitude < 0.01f;

            var p = transform.position + Vector3.up * surface;
            var v = launch;
            Add(p);
            for (float t = 0f; t < TraceTime; t += TraceStep)
            {
                v.y = Mathf.Max(v.y - gravity * (v.y < 0f ? fall : 1f) * TraceStep, -maxFall);
                if (straightUp && v.y <= 0f) return;
                var next = p + v * TraceStep;
                if (Physics.Linecast(p, next, out var hit, mask, QueryTriggerInteraction.Ignore))
                {
                    Add(hit.point);
                    if (!straightUp && hit.normal.y > 0.5f) MarkLanding(hit.point, hit.normal);
                    return;
                }
                Add(next);
                p = next;
            }
        }

        void Add(Vector3 point)
        {
            along.Add(path.Count == 0 ? 0f : along[^1] + Vector3.Distance(path[^1], point));
            path.Add(point);
        }

        // A flat diamond where a launch pad drops you, pulsing.
        void MarkLanding(Vector3 point, Vector3 normal)
        {
            landing = Part("Landing", Own(RoomBuilder.BoxMesh(new Vector3(2.4f, 0.08f, 2.4f), Vector3.zero)));
            landing.SetPositionAndRotation(point + normal * 0.06f, Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, 45f, 0f));
        }

        Transform Part(string name, Mesh mesh)
        {
            var go = new GameObject(name) { layer = gameObject.layer };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        Mesh Own(Mesh mesh)
        {
            meshes.Add(mesh);
            return mesh;
        }

        void OnDestroy()
        {
            foreach (var mesh in meshes)
                if (mesh) Destroy(mesh);
        }

        void Update()
        {
            if (landing) landing.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(Time.time * 6f));
            if (arrows == null) return;
            var cam = Camera.main;
            float length = Length;
            float offset = Mathf.Repeat(Time.time * Speed, Spacing);
            for (int i = 0; i < arrows.Length; i++)
            {
                float d = offset + i * Spacing;
                var arrow = arrows[i];
                bool shown = d < length;
                if (arrow.gameObject.activeSelf != shown) arrow.gameObject.SetActive(shown);
                if (!shown) continue;
                Sample(d, out var at, out var direction);
                // Lay the arrowhead flat toward the camera, around the way it's flying.
                var facing = Vector3.ProjectOnPlane(cam ? cam.transform.position - at : Vector3.up, direction);
                if (facing.sqrMagnitude < 0.001f) facing = Vector3.ProjectOnPlane(Vector3.up, direction);
                if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
                arrow.SetPositionAndRotation(at, Quaternion.LookRotation(direction, facing));
                // Grows in as it leaves the pad and shrinks away at the end of the path.
                arrow.localScale = Vector3.one * Mathf.Clamp01(Mathf.Min(d, length - d) / 2f);
            }
        }

        // The point `distance` meters along the path, and the way the path runs there.
        void Sample(float distance, out Vector3 point, out Vector3 direction)
        {
            int i = along.BinarySearch(distance);
            if (i < 0) i = ~i;
            i = Mathf.Clamp(i, 1, path.Count - 1);
            float span = along[i] - along[i - 1];
            float t = span > 0f ? (distance - along[i - 1]) / span : 0f;
            point = Vector3.Lerp(path[i - 1], path[i], t);
            direction = (path[i] - path[i - 1]).normalized;
            if (direction.sqrMagnitude < 0.5f) direction = Vector3.up;
        }
    }
}
