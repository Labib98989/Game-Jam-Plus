using UnityEngine;

namespace CoreDump
{
    // Something to shoot in the test room. Flashes when hit, vanishes when out of health, then comes back.
    public class TargetDummy : Target
    {
        public float maxHealth = 12f;
        public float respawnDelay = 2f;
        public Color flashColor = Color.white;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        float health;
        float flashUntil;
        Color baseColor = Color.gray;
        Collider[] colliders;
        MaterialPropertyBlock block;

        public override bool IsAlive => health > 0f;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
            block = new MaterialPropertyBlock();
            if (renderers.Length > 0 && renderers[0].sharedMaterial && renderers[0].sharedMaterial.HasColor(BaseColor))
                baseColor = renderers[0].sharedMaterial.GetColor(BaseColor);
            health = maxHealth;
        }

        public override void TakeHit(float damage, Vector3 direction)
        {
            if (health <= 0f) return;
            health -= damage;
            flashUntil = Time.time + 0.06f;
            if (health <= 0f)
            {
                SetAlive(false);
                Invoke(nameof(Respawn), respawnDelay);
            }
        }

        void Respawn()
        {
            health = maxHealth;
            SetAlive(true);
        }

        void SetAlive(bool alive)
        {
            foreach (var r in renderers) r.enabled = alive;
            foreach (var c in colliders) c.enabled = alive;
        }

        void Update()
        {
            block.SetColor(BaseColor, Time.time < flashUntil ? flashColor : baseColor);
            foreach (var r in renderers) r.SetPropertyBlock(block);
        }
    }
}
