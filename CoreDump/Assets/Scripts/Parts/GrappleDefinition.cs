using UnityEngine;

namespace CoreDump
{
    [CreateAssetMenu(menuName = "Core Dump/Parts/Grapple")]
    public class GrappleDefinition : PartDefinition
    {
        public float range = 40f;
        [Tooltip("It hooks the surface nearest the crosshair within this many degrees, so you don't have to aim exactly.")]
        public float coneAngle = 18f;
        [Tooltip("Pull toward the hook while held, in meters per second squared.")]
        public float pull = 80f;
        [Tooltip("Each extra Grapple on the same button adds this much of the one before it.")]
        public float stackFalloff = 0.8f;
        public float maxPull = 160f;
        public float maxSpeed = 40f;
        [Tooltip("Lets go by itself this close to the hook.")]
        public float releaseDistance = 2.5f;
        [Tooltip("How fast a hooked knocked-off part flies back to you.")]
        public float catchSpeed = 18f;
        public Material ropeMaterial;
        public Material markerMaterial;

        public override PartModule AddModule(GameObject host) => host.AddComponent<Grapple>();
    }
}
