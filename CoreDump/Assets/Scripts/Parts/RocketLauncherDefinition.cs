using UnityEngine;

namespace CoreDump
{
    // Rockets with splash damage that always fly forward, wherever the launcher is mounted. Long reload.
    [CreateAssetMenu(menuName = "Core Dump/Parts/Rocket Launcher")]
    public class RocketLauncherDefinition : WeaponDefinition
    {
        public float rocketSpeed = 40f;
        public float blastRadius = 4f;
        [Tooltip("Push from the blast, including on you: a rocket at your feet is a rocket jump.")]
        public float blastForce = 18f;
        public Material blastMaterial;
        [Tooltip("With a Thermal Eye chain touching the launcher, rockets home in on enemies, turning this many degrees per second per level of the chain.")]
        public float homingPerLevel = 90f;

        // A hybrid: offense mods like any gun, and vision mods help its rockets find targets.
        public override ModFamily TakesMods => ModFamily.Offense | ModFamily.Vision;
        public override PartModule AddModule(GameObject host) => host.AddComponent<RocketLauncher>();
    }
}
