using UnityEngine;

namespace CoreDump
{
    // Rapid fire while held, long range, small kick, low damage per shot.
    [CreateAssetMenu(menuName = "Core Dump/Parts/Rivet Gun")]
    public class RivetGunDefinition : WeaponDefinition
    {
        public float spreadAngle = 1.2f;

        public override PartModule AddModule(GameObject host) => host.AddComponent<RivetGun>();
    }
}
