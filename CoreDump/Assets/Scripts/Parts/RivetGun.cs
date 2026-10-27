namespace CoreDump
{
    // Rapid fire while you hold it. Long range, small kick.
    public class RivetGun : Weapon
    {
        RivetGunDefinition Tuning => (RivetGunDefinition)Definition;

        protected override void Fire()
        {
            var muzzle = Nozzle;
            var direction = ScatterBlaster.Spread(AimFrom(muzzle), Tuning.spreadAngle);
            var end = muzzle + direction * Tuning.range;
            if (Hitscan(muzzle, direction, Tuning.range, out var hit))
            {
                end = hit.point;
                Damage(hit, ShotDamage, direction, 1f);
            }
            ShotFx.Tracer(muzzle, end, Tuning.shotMaterial, 0.04f, 0.05f);
            KickBack(direction, Tuning.kick);
        }
    }
}
