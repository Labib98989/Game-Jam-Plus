using UnityEngine;

namespace CoreDump
{
    // Always spinning, and hurts anything it touches. No button, never reloads.
    // Pairs with body slams and the Ram. Only hurts what it actually touches.
    [CreateAssetMenu(menuName = "Core Dump/Parts/Buzz Saw")]
    public class BuzzSawDefinition : PartDefinition
    {
        public float damagePerSecond = 15f;
        [Tooltip("Seconds between hits on the same thing.")]
        public float hitInterval = 0.2f;
        public float spinSpeed = 900f;
        public Material bladeMaterial;

        public override PartCategory Category => PartCategory.Weapons;
        public override PartModule AddModule(GameObject host) => host.AddComponent<BuzzSaw>();
    }
}
