using UnityEngine;

namespace CoreDump
{
    // Lightning that jumps to the nearest enemy, then on to more. Aims itself. Weak, short range.
    [CreateAssetMenu(menuName = "Core Dump/Parts/Zapper")]
    public class ZapperDefinition : WeaponDefinition
    {
        [Tooltip("How many more enemies the bolt jumps on to after the first.")]
        public int jumps = 2;
        public float jumpRange = 8f;
        [Tooltip("It looks for its first enemy within this many degrees of where it points.")]
        public float seekAngle = 80f;

        public override PartModule AddModule(GameObject host) => host.AddComponent<Zapper>();
    }
}
