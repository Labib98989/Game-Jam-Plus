using System.Collections.Generic;
using UnityEngine;

namespace CoreDump
{
    public enum PartMarking { None, Nozzle, Visor }

    // The folders the build screen sorts parts into, in this order.
    public enum PartCategory { Structure, Movement, Weapons, MovementMods, OffenseMods, VisionMods, Head }

    // Which mods a part takes. Most parts take one family; hybrids such as the Ram (movement and
    // offense) and the Rocket Launcher (offense and vision) take two.
    [System.Flags]
    public enum ModFamily { None = 0, Movement = 1, Offense = 2, Vision = 4 }

    // What a part is: its cube shape, its default button and how it looks. Each part type
    // subclasses this with its own tuning and adds its behaviour when it's fitted.
    public abstract class PartDefinition : ScriptableObject
    {
        public string displayName = "Part";
        [TextArea] public string description = "";
        [Tooltip("The part's cubes in its own grid. (0,0,0) is the first cube, which goes next to the surface you aim at. +Z is the way the part points.")]
        public Vector3Int[] cubes = { Vector3Int.zero };
        public ButtonSlot defaultButton = ButtonSlot.RightMouse;
        public Material material;
        [Tooltip("Nozzle: the part has a working end (barrel, nozzle, tip, hook, pad or foot) on the +Z side of the nozzle cube, marked dark, and it only works while the cell in front of it is empty. Visor: the head's face.")]
        public PartMarking marking = PartMarking.None;
        public Vector3Int nozzleCube;
        [Tooltip("Optional model: 1-unit cubes, pivot at the center of the first cube, pointing along +Z. Plain cubes if empty.")]
        public GameObject modelPrefab;
        [Tooltip("Hits it takes before it breaks off. Every part breaks off at the first hit for now; armor will take more.")]
        public int hits = 1;

        public bool HasWorkingEnd => marking == PartMarking.Nozzle;
        public int Toughness => Mathf.Max(1, hits);
        public virtual PartCategory Category => PartCategory.Movement;
        // Whether `part` can attach to this one. The body holds anything; so do connectors, while
        // mods hold mods and the head holds vision mods. Everything else holds nothing.
        public virtual bool CanHold(PartDefinition part) => false;
        // What the build screen says when this part isn't attached to anything that can hold it.
        public virtual string AttachRule => "Parts attach only to the body or a connector";
        public virtual ModFamily TakesMods => Category switch
        {
            PartCategory.Movement => ModFamily.Movement,
            PartCategory.Weapons => ModFamily.Offense,
            _ => ModFamily.None,
        };

        public abstract PartModule AddModule(GameObject host);

        // Why this part wouldn't work at this spot, or null if it would. `bottomLayer` is the robot's
        // lowest row of cells (the one touching the ground). For a part not placed yet, pass its
        // `footprint` so its own cubes count.
        public virtual string ProblemAt(RobotBody robot, Vector3Int anchor, Quaternion orientation, int bottomLayer, ICollection<Vector3Int> footprint = null)
        {
            if (!HasWorkingEnd) return null;
            var inFront = WorkingEnd(anchor, orientation) + GridMath.Rotate(orientation, GridMath.Forward);
            bool blocked = robot.IsFilled(inFront) || (footprint != null && footprint.Contains(inFront));
            return blocked ? "Its working end is blocked" : null;
        }

        public Vector3Int WorkingEnd(Vector3Int anchor, Quaternion orientation) => anchor + GridMath.Rotate(orientation, nozzleCube);
    }
}
