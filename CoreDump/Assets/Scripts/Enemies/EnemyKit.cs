using UnityEngine;

namespace CoreDump
{
    // What enemies are made of, and what they can stand on, see and hit.
    [CreateAssetMenu(menuName = "Core Dump/Enemy Kit")]
    public class EnemyKit : ScriptableObject
    {
        public Material skin;
        [Tooltip("The Brute's front plate.")]
        public Material armor;
        [Tooltip("Unlit and bright: eyes, and the glow before a Spitter fires.")]
        public Material eye;
        public Material shot;
        [Tooltip("The flash where an enemy is about to come in, and where one dies.")]
        public Material spawnFx;
        [Tooltip("See-through red: the ground a Bomber is about to blast.")]
        public Material dangerZone;
        [Tooltip("Bright red: a danger zone filling up as its blast nears, the Bomber's bolts and blasts.")]
        public Material dangerFill;
        [Tooltip("The layer enemies are on.")]
        public int layer;
        [Tooltip("What enemies land on: the room, not the robot or each other.")]
        public LayerMask groundMask = ~0;
        [Tooltip("What enemy shots and sight lines hit: the room and the robot, not other enemies.")]
        public LayerMask shotMask = ~0;
    }
}
