using UnityEngine;

namespace CoreDump
{
    [CreateAssetMenu(menuName = "Core Dump/Parts/Jet")]
    public class JetDefinition : PartDefinition
    {
        [Header("Dash (tap)")]
        [Tooltip("Speed of the dash a tap gives, the way the Jet pushes.")]
        public float dashSpeed = 26f;
        public float maxDashSpeed = 40f;
        [Tooltip("Seconds you can't steer after a dash.")]
        public float dashLock = 0.12f;
        [Tooltip("Share of the dash's speed you keep once it ends.")]
        public float dashKeep = 0.5f;
        public float dashCooldown = 0.35f;
        [Tooltip("Share of the fuel a dash uses in the air.")]
        public float dashFuel = 0.3f;

        [Header("Thrust (hold)")]
        [Tooltip("Push from one Jet while held, in meters per second squared. Gravity is 32, so one Jet underneath hovers.")]
        public float thrust = 40f;
        [Tooltip("Each extra Jet on the same button adds this much of the one before it.")]
        public float stackFalloff = 0.8f;
        [Tooltip("The most any stack of Jets can push.")]
        public float maxThrust = 90f;
        [Tooltip("Jets stop adding speed past this, along the way they push.")]
        public float maxSpeed = 32f;
        [Tooltip("Seconds of pushing in the air before the fuel runs out. It refills when you touch the ground.")]
        public float fuelTime = 1.2f;
        public Material flameMaterial;

        public override PartModule AddModule(GameObject host) => host.AddComponent<Jet>();
    }
}
