using UnityEngine;
using UnityEngine.Rendering;

namespace CoreDump
{
    // A placeholder effect: a shape that grows to its full size and vanishes, like a blast or a shockwave.
    public class FxGrow : MonoBehaviour
    {
        Vector3 size;
        float duration;
        float start;

        public static void Spawn(PrimitiveType shape, Vector3 position, Vector3 size, float duration, Material material)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = "Effect";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = Vector3.zero;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var fx = go.AddComponent<FxGrow>();
            fx.size = size;
            fx.duration = duration;
            fx.start = Time.time;
        }

        void Update()
        {
            float t = (Time.time - start) / duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            // Fast at first, then easing out.
            transform.localScale = size * (1f - (1f - t) * (1f - t));
        }
    }
}
