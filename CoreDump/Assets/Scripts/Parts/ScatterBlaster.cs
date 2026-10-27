using UnityEngine;

namespace CoreDump
{
    // A shotgun blast with a big kick the other way.
    public class ScatterBlaster : Weapon
    {
        ScatterBlasterDefinition Tuning => (ScatterBlasterDefinition)Definition;

        protected override void Fire()
        {
            var muzzle = Nozzle;
            var direction = AimFrom(muzzle);
            for (int i = 0; i < Tuning.pellets; i++)
            {
                var dir = Spread(direction, Tuning.spreadAngle);
                var end = muzzle + dir * Tuning.range;
                if (Hitscan(muzzle, dir, Tuning.range, out var hit))
                {
                    end = hit.point;
                    Damage(hit, ShotDamage, dir, Tuning.knockback);
                }
                ShotFx.Tracer(muzzle, end, Tuning.shotMaterial);
            }
            KickBack(direction, Tuning.kick);
        }

        public static Vector3 Spread(Vector3 direction, float maxAngle)
        {
            var axis = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
            var tilt = Quaternion.AngleAxis(Random.Range(0f, maxAngle), axis);
            var roll = Quaternion.AngleAxis(Random.Range(0f, 360f), direction);
            return roll * tilt * direction;
        }
    }
}
