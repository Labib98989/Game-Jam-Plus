namespace CoreDump
{
    // Drops a bomb the way it points, carried along with the robot's own speed.
    public class BombBay : Weapon
    {
        BombBayDefinition Tuning => (BombBayDefinition)Definition;

        protected override void Fire()
        {
            Bomb.Drop(Robot, Nozzle, Direction * Tuning.dropSpeed + Robot.Motor.Velocity, Tuning.fuse,
                Tuning.blastRadius, ShotDamage, Tuning.blastForce, Tuning.shotMaterial, Tuning.blastMaterial);
            KickBack(Direction, Tuning.kick);
        }
    }
}
