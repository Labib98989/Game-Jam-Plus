using System.Collections.Generic;
using UnityEngine;

namespace CoreDump
{
    // A part fitted to the robot. Subclasses decide what its button does.
    public abstract class PartModule : MonoBehaviour
    {
        public PartDefinition Definition { get; private set; }
        public RobotBody Robot { get; private set; }
        public Vector3Int Anchor { get; private set; }
        // Rotation in the robot's own space, always one of the 24 grid rotations.
        public Quaternion Orientation { get; private set; }
        public ButtonSlot Button { get; set; }
        public IReadOnlyList<Vector3Int> Cells => cells;
        // Hidden health, in hits. The part shows it on itself (PartWear), and breaks off at zero.
        public int Health { get; private set; }
        public int MaxHealth => Definition.Toughness;
        // 0 when new, 1 when about to break.
        public float Wear => 1f - (float)Health / MaxHealth;
        public float LastHitTime { get; private set; } = -10f;

        readonly List<Vector3Int> cells = new();

        internal void Bind(PartDefinition definition, RobotBody robot, Vector3Int anchor, Quaternion orientation, ButtonSlot button, IEnumerable<Vector3Int> footprint)
        {
            Definition = definition;
            Robot = robot;
            Anchor = anchor;
            Orientation = orientation;
            Button = button;
            cells.Clear();
            cells.AddRange(footprint);
            Health = MaxHealth;
            OnFitted();
        }

        // Wears the part down. True when that breaks it.
        public bool Hit(int damage)
        {
            Health = Mathf.Max(0, Health - damage);
            LastHitTime = Time.time;
            return Health == 0;
        }

        public void Repair() => Health = MaxHealth;

        // The way the part points, in world space and in the robot's grid.
        public Vector3 Direction => transform.forward;
        public Vector3Int Facing => GridMath.Rotate(Orientation, GridMath.Forward);

        // Why the part can't work where it is (a blocked barrel, a Leg off the ground), or null if it can.
        public string Problem => Definition.ProblemAt(Robot, Anchor, Orientation, Robot.BottomLayer);
        public bool IsWorking => Problem == null;

        // What the strongest touching chain of a mod type gives this part (a share, degrees or
        // meters, depending on the type), and as a multiplier for the shares.
        public float ModBonus(ModType type) => Robot && Robot.ChainBoosting(this, type) is { } chain ? chain.Amount : 0f;
        public float ModLevel(ModType type) => Robot && Robot.ChainBoosting(this, type) is { } chain ? chain.level : 0f;
        public float Boost(ModType type) => 1f + ModBonus(type);

        // Just outside the nozzle cube's +Z face: where shots and thrust come out.
        public Vector3 Nozzle => transform.TransformPoint((Vector3)Definition.nozzleCube + new Vector3(0f, 0f, 0.55f));

        protected virtual void OnFitted() { }
        public virtual void OnButtonDown() { }
        public virtual void OnButtonHeld() { }
        public virtual void OnButtonUp() { }
    }
}
