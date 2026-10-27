using UnityEngine;
using UnityEngine.Rendering;

namespace CoreDump
{
    // Placeholder shot effects for the prototype.
    public static class ShotFx
    {
        public static void Tracer(Vector3 from, Vector3 to, Material material, float width = 0.06f, float life = 0.07f)
        {
            var line = NewLine("Tracer", material, width, life);
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.endWidth = width * 0.5f;
        }

        // A jagged bolt from one point to another.
        public static void Lightning(Vector3 from, Vector3 to, Material material, float life = 0.12f)
        {
            const int segments = 7;
            var line = NewLine("Lightning", material, 0.12f, life);
            line.positionCount = segments + 1;
            var side = Vector3.Cross(to - from, Vector3.up).normalized;
            if (side == Vector3.zero) side = Vector3.right;
            var up = Vector3.Cross(side, to - from).normalized;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                var jitter = i == 0 || i == segments ? Vector3.zero : (side * Random.Range(-0.5f, 0.5f) + up * Random.Range(-0.5f, 0.5f));
                line.SetPosition(i, Vector3.Lerp(from, to, t) + jitter);
            }
        }

        static LineRenderer NewLine(string name, Material material, float width, float life)
        {
            var go = new GameObject(name);
            var line = go.AddComponent<LineRenderer>();
            line.startWidth = line.endWidth = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            Object.Destroy(go, life);
            return line;
        }
    }
}
