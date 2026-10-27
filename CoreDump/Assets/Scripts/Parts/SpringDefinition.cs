using UnityEngine;

namespace CoreDump
{
    [CreateAssetMenu(menuName = "Core Dump/Parts/Spring")]
    public class SpringDefinition : PartDefinition
    {
        [Tooltip("How high one Spring jumps off the ground, in meters.")]
        public float jumpHeight = 3f;
        [Tooltip("Each extra Spring on the same button adds this much of the one before it.")]
        public float stackFalloff = 0.8f;
        [Tooltip("The highest any stack of Springs can jump, in meters.")]
        public float maxJumpHeight = 12f;
        [Tooltip("How far past the pad a surface still counts as touching.")]
        public float reach = 0.6f;
        [Tooltip("A press this many seconds before the pad reaches a surface still jumps.")]
        public float bufferTime = 0.15f;
        [Header("Wall-jumps")]
        [Tooltip("Push away from the wall, as a share of the jump's speed.")]
        public float wallPush = 1.1f;
        [Tooltip("Push upward off a wall, as a share of the jump's speed.")]
        public float wallLift = 0.8f;

        public override PartModule AddModule(GameObject host) => host.AddComponent<Spring>();
    }
}
