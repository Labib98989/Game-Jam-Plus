using System.Collections.Generic;
using UnityEngine;

namespace CoreDump
{
    // Lightning that aims itself at the nearest enemy the way it points, then jumps on to the next
    // nearest ones. With nobody in range it just crackles.
    public class Zapper : Weapon
    {
        readonly List<Target> chain = new();

        ZapperDefinition Tuning => (ZapperDefinition)Definition;

        protected override void Fire()
        {
            var muzzle = Nozzle;
            chain.Clear();
            var target = Targeting.Nearest(muzzle, Direction, Tuning.seekAngle, Tuning.range);
            if (!target)
            {
                ShotFx.Lightning(muzzle, muzzle + Direction * 2f, Tuning.shotMaterial);
                return;
            }

            var from = muzzle;
            while (target && chain.Count <= Tuning.jumps)
            {
                chain.Add(target);
                ShotFx.Lightning(from, target.AimPoint, Tuning.shotMaterial);
                target.TakeHit(ShotDamage, (target.AimPoint - from).normalized);
                from = target.AimPoint;
                target = Targeting.Nearest(from, Vector3.forward, 180f, Tuning.jumpRange, chain);
            }
            KickBack((chain[0].AimPoint - muzzle).normalized, Tuning.kick);
        }
    }
}
