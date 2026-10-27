using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CoreDump
{
    // Treads tuning lives on RobotMotor, next to the Legs it combines with.
    // Treads only work lying flat on the ground: every cube on the robot's bottom row.
    [CreateAssetMenu(menuName = "Core Dump/Parts/Treads")]
    public class TreadsDefinition : PartDefinition
    {
        public override PartModule AddModule(GameObject host) => host.AddComponent<Treads>();

        public override string ProblemAt(RobotBody robot, Vector3Int anchor, Quaternion orientation, int bottomLayer, ICollection<Vector3Int> footprint = null)
        {
            bool flat = cubes.All(c => (anchor + GridMath.Rotate(orientation, c)).y == bottomLayer);
            return flat ? base.ProblemAt(robot, anchor, orientation, bottomLayer, footprint) : "Treads have to lie flat on the ground";
        }
    }
}
