using System.Collections.Generic;
using UnityEngine;

namespace CoreDump
{
    // A short cone of flame while you hold it, burning everything in the cone a little each tick.
    public class Welder : Weapon
    {
        readonly HashSet<IDamageable> burned = new();

        WelderDefinition Tuning => (WelderDefinition)Definition;

        protected override void Fire()
        {
            var muzzle = Nozzle;
            var direction = AimFrom(muzzle);
            burned.Clear();
            foreach (var collider in Physics.OverlapSphere(muzzle, Tuning.range, Robot.shotMask, QueryTriggerInteraction.Ignore))
            {
                var to = collider.ClosestPoint(muzzle) - muzzle;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(direction, to) > Tuning.flameAngle) continue;
                var target = collider.GetComponentInParent<IDamageable>();
                if (target != null && burned.Add(target)) target.TakeHit(ShotDamage, direction);
            }
            for (int i = 0; i < 4; i++)
            {
                var dir = ScatterBlaster.Spread(direction, Tuning.flameAngle);
                ShotFx.Tracer(muzzle, muzzle + dir * Tuning.range * Random.Range(0.6f, 1f), Tuning.shotMaterial, 0.35f, 0.08f);
            }
            KickBack(direction, Tuning.kick);
        }
    }
}
