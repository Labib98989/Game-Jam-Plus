using System.Linq;
using UnityEngine;

namespace CoreDump
{
    // Shows a part's hidden health on the part itself, since there are no numbers: a white flash on
    // every hit, orange once it's worn down, and a flickering red with sparks and smoke when it's
    // about to break off.
    public class PartWear : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color Worn = new(1f, 0.5f, 0.1f);
        static readonly Color Breaking = new(1f, 0.12f, 0.08f);

        PartModule part;
        RobotHealth health;
        Renderer[] renderers;
        Color[] baseColors;
        MaterialPropertyBlock block;
        bool tinted;
        float nextSpark;

        void Start()
        {
            part = GetComponent<PartModule>();
            health = part && part.Robot ? part.Robot.GetComponent<RobotHealth>() : null;
            renderers = GetComponentsInChildren<Renderer>();
            baseColors = renderers.Select(r => r.sharedMaterial && r.sharedMaterial.HasColor(BaseColor) ? r.sharedMaterial.GetColor(BaseColor) : Color.white).ToArray();
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            // Building pauses the action and lights parts up itself, so the colors are left to it.
            if (Time.timeScale == 0f || !part) return;
            float wear = part.Wear;
            bool flash = Time.time - part.LastHitTime < 0.08f;
            if (!flash && wear < 0.4f)
            {
                if (!tinted) return;
                foreach (var r in renderers)
                    if (r) r.SetPropertyBlock(null);
                tinted = false;
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i]) continue;
                var color = flash ? Color.white
                    : wear < 0.7f ? Color.Lerp(baseColors[i], Worn, 0.65f)
                    : Color.Lerp(baseColors[i], Breaking, 0.5f + 0.4f * Mathf.PingPong(Time.time * 6f, 1f));
                block.SetColor(BaseColor, color);
                renderers[i].SetPropertyBlock(block);
            }
            tinted = true;

            if (wear < 0.7f || Time.time < nextSpark || !health || renderers.Length == 0) return;
            nextSpark = Time.time + Random.Range(0.15f, 0.4f);
            var at = renderers[Random.Range(0, renderers.Length)].bounds.center;
            if (health.sparkMaterial) FxSpark.Burst(at, 2, 4f, health.sparkMaterial);
            if (health.smokeMaterial && Random.value < 0.4f) FxGrow.Spawn(PrimitiveType.Sphere, at + Vector3.up * 0.3f, Vector3.one * 0.5f, 0.5f, health.smokeMaterial);
        }
    }
}
