using System.Collections.Generic;
using UnityEngine;

namespace CoreDump
{
    // Leg and Treads tuning lives on RobotMotor, since walking adds up across all of them.
    // A Leg only works pointing down with its foot (the nozzle cube) on the ground.
    [CreateAssetMenu(menuName = "Core Dump/Parts/Leg")]
    public class LegDefinition : PartDefinition
    {
        public override PartModule AddModule(GameObject host) => host.AddComponent<Leg>();

        public override string ProblemAt(RobotBody robot, Vector3Int anchor, Quaternion orientation, int bottomLayer, ICollection<Vector3Int> footprint = null)
        {
            if (GridMath.Rotate(orientation, GridMath.Forward) != Vector3Int.down) return "Legs have to point down";
            if (WorkingEnd(anchor, orientation).y != bottomLayer) return "Its foot doesn't reach the ground";
            return base.ProblemAt(robot, anchor, orientation, bottomLayer, footprint);
        }
    }
}
