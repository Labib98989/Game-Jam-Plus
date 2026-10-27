using UnityEngine;

namespace CoreDump
{
    // A huge punch at range that knocks things back. It fires your actual fist (the cube at the
    // working end); reloading calls it back, and it smacks anything on the way.
    [CreateAssetMenu(menuName = "Core Dump/Parts/Rocket Fist")]
    public class RocketFistDefinition : WeaponDefinition
    {
        public float fistSpeed = 45f;
        [Tooltip("Push on loose physics objects it punches.")]
        public float punchForce = 20f;
        [Tooltip("Damage to anything it passes on the way back.")]
        public float returnDamage = 6f;
        [Tooltip("Seconds the fist waits where it landed before it comes back.")]
        public float hangTime = 0.25f;

        public override PartModule AddModule(GameObject host) => host.AddComponent<RocketFist>();
    }
}
