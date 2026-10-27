using UnityEngine;

namespace CoreDump
{
    // Hold for a short cone of flame that burns through packs. Very short range.
    // Its "shots" are ticks of flame: shotsPerReload x fireInterval is how many seconds it burns.
    [CreateAssetMenu(menuName = "Core Dump/Parts/Welder")]
    public class WelderDefinition : WeaponDefinition
    {
        [Tooltip("Half-angle of the flame cone, in degrees.")]
        public float flameAngle = 25f;

        public override PartModule AddModule(GameObject host) => host.AddComponent<Welder>();
    }
}
