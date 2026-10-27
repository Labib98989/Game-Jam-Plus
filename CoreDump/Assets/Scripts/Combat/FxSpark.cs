using UnityEngine;
using UnityEngine.Rendering;

namespace CoreDump
{
    // A placeholder spark: a tiny cube that flies out, falls and is gone.
    public class FxSpark : MonoBehaviour
    {
        Vector3 velocity;
        float dieTime;

        public static void Burst(Vector3 position, int count, float speed, Material material, float size = 0.08f)
        {
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Spark";
                Destroy(go.GetComponent<Collider>());
                go.transform.position = position;
                go.transform.localScale = Vector3.one * size;
                var renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                var spark = go.AddComponent<FxSpark>();
                spark.velocity = (Random.onUnitSphere + Vector3.up * 0.6f) * (speed * Random.Range(0.5f, 1f));
                spark.dieTime = Time.time + Random.Range(0.25f, 0.5f);
            }
        }

        void Update()
        {
            velocity += Vector3.down * (20f * Time.deltaTime);
            transform.position += velocity * Time.deltaTime;
            if (Time.time > dieTime) Destroy(gameObject);
        }
    }
}
