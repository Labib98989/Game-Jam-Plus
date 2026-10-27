using UnityEngine;

namespace CoreDump
{
    // Tuning every gun shares: shots per reload, reload time, damage, range, kick and aiming.
    public abstract class WeaponDefinition : PartDefinition
    {
        public override PartCategory Category => PartCategory.Weapons;

        [Header("Firing")]
        [Tooltip("Shots before the gun has to reload. Empty guns reload by themselves; R reloads every gun.")]
        public int shotsPerReload = 2;
        public float reloadTime = 1.2f;
        [Tooltip("Seconds between shots.")]
        public float fireInterval = 0.3f;
        [Tooltip("Keeps firing while the button is held.")]
        public bool automatic;
        public float damage = 1f;
        public float range = 30f;
        [Tooltip("Instant change in the robot's speed, opposite to where each shot goes. Bigger guns kick harder.")]
        public float kick = 5f;

        [Header("Aiming")]
        [Tooltip("Pointing within this many degrees of the robot's front, it aims at the crosshair.")]
        public float forwardCone = 50f;
        [Tooltip("Pointing anywhere else, it aims itself at the nearest enemy within this many degrees of where it points.")]
        public float autoAimCone = 35f;
        public Material shotMaterial;
        [Tooltip("What each shot sounds like.")]
        public Sound fireSound = Sound.Blaster;
    }
}
