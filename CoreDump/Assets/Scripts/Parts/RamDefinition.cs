using UnityEngine;

namespace CoreDump
{
    [CreateAssetMenu(menuName = "Core Dump/Parts/Ram")]
    public class RamDefinition : PartDefinition
    {
        [Header("Lunge")]
        [Tooltip("Speed of one Ram's lunge, toward wherever it points.")]
        public float lungeSpeed = 26f;
        [Tooltip("Each extra Ram on the same button adds this much of the one before it.")]
        public float stackFalloff = 0.8f;
        public float maxLungeSpeed = 40f;
        [Tooltip("Seconds you can't steer after a lunge.")]
        public float steerLock = 0.25f;
        [Tooltip("Share of the lunge's speed you keep once it ends.")]
        public float keep = 0.6f;
        [Tooltip("Seconds between lunges on the ground. In the air you get one lunge per jump.")]
        public float groundCooldown = 0.6f;

        [Header("Hits")]
        [Tooltip("Damage from the tip during a lunge.")]
        public float tipDamage = 6f;
        [Tooltip("The tip hits anything this close to it, so near misses count.")]
        public float tipRadius = 0.9f;
        [Tooltip("A lunge pointing down in mid-air is a butt slam: landing hits everything in this radius.")]
        public float slamRadius = 4.5f;
        public float slamDamage = 4f;
        public float slamKnockback = 14f;
        public Material shockwaveMaterial;

        // A hybrid: movement mods make it lunge further, offense mods make it hit harder.
        public override ModFamily TakesMods => ModFamily.Movement | ModFamily.Offense;
        public override PartModule AddModule(GameObject host) => host.AddComponent<Ram>();
    }
}
