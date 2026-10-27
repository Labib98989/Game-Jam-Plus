using UnityEngine;

namespace CoreDump
{
    // Makes the robot's movement feel physical: a thud, a shake and a puff of dust on a hard landing,
    // and a scrape with sparks while sliding or wall-running.
    [RequireComponent(typeof(RobotMotor))]
    public class RobotJuice : MonoBehaviour
    {
        [Tooltip("Landings slower than this, in meters per second, make no fuss.")]
        public float softLanding = 9f;
        [Tooltip("Landings this fast get the biggest thud.")]
        public float hardLanding = 40f;

        RobotMotor motor;
        RobotBody robot;
        bool wasSliding, wasWallRunning;
        float nextSpark;

        Vector3 Feet => transform.position + Vector3.up * robot.LocalBounds.min.y;

        void Awake()
        {
            motor = GetComponent<RobotMotor>();
            robot = GetComponent<RobotBody>();
            motor.Landed += OnLanded;
        }

        void OnDestroy()
        {
            if (motor) motor.Landed -= OnLanded;
        }

        void OnLanded()
        {
            if (motor.LandingSpeed < softLanding) return;
            float strength = Mathf.InverseLerp(softLanding, hardLanding, motor.LandingSpeed);
            Juice.Play(Sound.Land, null, 0.5f + 0.5f * strength);
            Juice.Dust(Feet, 1.5f + 3f * strength);
            Juice.Shake(0.1f + 0.3f * strength);
        }

        void Update()
        {
            if (motor.IsSliding && !wasSliding) Juice.Play(Sound.Slide);
            if (motor.IsWallRunning && !wasWallRunning) Juice.Play(Sound.WallRun);
            wasSliding = motor.IsSliding;
            wasWallRunning = motor.IsWallRunning;
            if (!(motor.IsSliding || motor.IsWallRunning) || Time.time < nextSpark) return;
            nextSpark = Time.time + 0.05f;
            var at = motor.IsWallRunning ? Feet + Vector3.up * 0.6f - motor.WallNormal * 0.4f : Feet;
            Juice.Sparks(at, 2, 5f);
        }
    }
}
