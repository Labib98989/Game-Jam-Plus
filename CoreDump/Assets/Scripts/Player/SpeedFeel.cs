using Unity.Cinemachine;
using UnityEngine;

namespace CoreDump
{
    // Makes speed feel like speed: the field of view widens as the robot goes faster, the camera
    // tilts away from a wall while wall-running, and speed lines streak in from the screen's edges.
    // A wall-run along a wall on the camera's shoulder swings the camera over to the other shoulder,
    // so it doesn't end up inside the wall.
    [RequireComponent(typeof(CinemachineCamera))]
    public class SpeedFeel : MonoBehaviour
    {
        public RobotMotor motor;
        public float baseFov = 70f;
        public float maxFov = 92f;
        [Tooltip("Speeds (meters per second) where the field of view starts widening, and where it's widest.")]
        public Vector2 fovSpeedRange = new(12f, 40f);
        public float wallRunTilt = 9f;
        [Tooltip("How long the camera stays on the other shoulder after a wall-run, so it doesn't swing back into the wall.")]
        public float shoulderHold = 0.5f;
        [Tooltip("Speed lines appear above this speed.")]
        public float speedLinesFrom = 20f;
        public float smoothing = 6f;

        CinemachineCamera cam;
        CinemachineThirdPersonFollow follow;
        RobotBody robot;
        float speed;
        float shoulder;
        float otherShoulderUntil;

        void Awake()
        {
            cam = GetComponent<CinemachineCamera>();
            follow = GetComponent<CinemachineThirdPersonFollow>();
            if (follow) shoulder = follow.CameraSide;
            if (motor) robot = motor.GetComponent<RobotBody>();
        }

        void LateUpdate()
        {
            if (!motor) return;
            float t = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            speed = Mathf.Lerp(speed, motor.Velocity.magnitude, t);

            var lens = cam.Lens;
            // Wide Lens mods on the head widen the view, at any speed.
            var head = robot ? robot.Head : null;
            float wider = head ? head.ModBonus(ModType.WideLens) : 0f;
            float targetFov = Mathf.Lerp(baseFov, maxFov, Mathf.InverseLerp(fovSpeedRange.x, fovSpeedRange.y, speed)) + wider;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFov, t);
            float tilt = 0f;
            if (motor.IsWallRunning)
            {
                tilt = Vector3.Dot(motor.WallNormal, motor.transform.right) > 0f ? -wallRunTilt : wallRunTilt;
                // The wall's normal points away from it, so a wall on the camera's right points left.
                bool wallOnRight = Vector3.Dot(motor.WallNormal, transform.right) < 0f;
                if (wallOnRight == (shoulder > 0.5f)) otherShoulderUntil = Time.time + shoulderHold;
            }
            lens.Dutch = Mathf.Lerp(lens.Dutch, tilt, t);
            cam.Lens = lens;
            if (follow) follow.CameraSide = Mathf.Lerp(follow.CameraSide, Time.time < otherShoulderUntil ? 1f - shoulder : shoulder, t);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || speed < speedLinesFrom) return;
            float strength = Mathf.InverseLerp(speedLinesFrom, speedLinesFrom + 25f, speed);
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float reach = center.magnitude;
            var matrix = GUI.matrix;
            for (int i = 0; i < 28; i++)
            {
                float angle = Random.Range(0f, 360f);
                var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                float from = reach * Random.Range(0.55f, 0.85f);
                float length = reach * Random.Range(0.1f, 0.25f) * (0.5f + strength);
                var start = center + direction * from;
                GUI.color = new Color(1f, 1f, 1f, 0.12f + 0.25f * strength);
                GUI.matrix = matrix;
                GUIUtility.RotateAroundPivot(angle, start);
                GUI.DrawTexture(new Rect(start.x, start.y - 1f, length, 2f), Texture2D.whiteTexture);
            }
            GUI.matrix = matrix;
            GUI.color = Color.white;
        }
    }
}
