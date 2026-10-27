using UnityEngine;

namespace CoreDump
{
    // A shotgun blast with a big kick. Short range.
    [CreateAssetMenu(menuName = "Core Dump/Parts/Scatter Blaster")]
    public class ScatterBlasterDefinition : WeaponDefinition
    {
        public int pellets = 8;
        public float spreadAngle = 7f;
        [Tooltip("Push on loose physics objects per pellet.")]
        public float knockback = 4f;

        public override PartModule AddModule(GameObject host) => host.AddComponent<ScatterBlaster>();
    }
}
