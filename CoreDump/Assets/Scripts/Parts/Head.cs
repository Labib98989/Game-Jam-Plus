namespace CoreDump
{
    // The robot's head: every robot has exactly one, and the camera and crosshair depend on it.
    // Losing it doesn't kill you, but the screen fills with static until it's back (see RobotHealth).
    public class Head : PartModule { }
}
