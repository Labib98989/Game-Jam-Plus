using UnityEngine;

namespace CoreDump
{
    // Throws the robot into the air when it steps on: straight up from a jump pad, or up and away
    // the way a launch pad faces. Every launch from the same pad flies the same, whatever speed
    // you came in at.
    public class JumpPad : MonoBehaviour
    {
        public Vector3 launch;

        // Every launch, with its velocity. The tutorial listens for it.
        public static event System.Action<Vector3> Launched;

        float readyAt;

        void OnTriggerEnter(Collider other) => TryLaunch(other);
        void OnTriggerStay(Collider other) => TryLaunch(other);

        void TryLaunch(Collider other)
        {
            if (Time.time < readyAt || !other.attachedRigidbody) return;
            var motor = other.attachedRigidbody.GetComponent<RobotMotor>();
            if (!motor) return;
            readyAt = Time.time + 0.4f;
            motor.Launch(launch);
            Launched?.Invoke(launch);
            Juice.Play(Sound.PadLaunch);
            Juice.Ring(transform.position, 6f);
        }
    }
}
