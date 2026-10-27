namespace CoreDump
{
    // Rockets always fly at the crosshair, wherever the launcher is mounted, and kick the robot back.
    // With a Thermal Eye chain touching the launcher, they home in on the enemy nearest where they're aimed.
    public class RocketLauncher : Weapon
    {
        RocketLauncherDefinition Tuning => (RocketLauncherDefinition)Definition;

        protected override void Fire()
        {
            var muzzle = Nozzle;
            var direction = (Robot.AimPoint - muzzle).normalized;
            float homing = Tuning.homingPerLevel * ModLevel(ModType.ThermalEye);
            var target = homing > 0f ? Targeting.Nearest(muzzle, direction, 30f, Tuning.range) : null;
            Rocket.Fire(Robot, muzzle, direction, Tuning.rocketSpeed, Tuning.blastRadius, ShotDamage, Tuning.blastForce, Tuning.shotMaterial, Tuning.blastMaterial, target, homing);
            KickBack(direction, Tuning.kick);
        }
    }
}
