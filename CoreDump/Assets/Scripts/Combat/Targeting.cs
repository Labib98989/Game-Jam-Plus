using System.Collections.Generic;
using UnityEngine;

namespace CoreDump
{
    // Finding enemies for guns that aim themselves.
    public static class Targeting
    {
        // The closest live enemy within `range` and within `cone` degrees of `direction`, or null.
        public static Target Nearest(Vector3 from, Vector3 direction, float cone, float range, ICollection<Target> skip = null)
        {
            Target best = null;
            float bestDistance = range;
            foreach (var target in Target.All)
            {
                if (!target.IsAlive || (skip != null && skip.Contains(target))) continue;
                var to = target.AimPoint - from;
                float distance = to.magnitude;
                if (distance > bestDistance || Vector3.Angle(direction, to) > cone) continue;
                best = target;
                bestDistance = distance;
            }
            return best;
        }
    }
}
