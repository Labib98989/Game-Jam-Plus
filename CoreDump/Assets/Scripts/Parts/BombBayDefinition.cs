using UnityEngine;

namespace CoreDump
{
    // Drops bombs the way it points. Pointing down while you hover with a Jet, it's a bombing run.
    // Bombs take a moment to go off.
    [CreateAssetMenu(menuName = "Core Dump/Parts/Bomb Bay")]
    public class BombBayDefinition : WeaponDefinition
    {
        [Tooltip("How fast a bomb leaves the bay, on top of the robot's own speed.")]
        public float dropSpeed = 6f;
        public float fuse = 1f;
        public float blastRadius = 4.5f;
        [Tooltip("Push from the blast, including on you.")]
        public float blastForce = 16f;
        public Material blastMaterial;

        public override PartModule AddModule(GameObject host) => host.AddComponent<BombBay>();
    }
}
