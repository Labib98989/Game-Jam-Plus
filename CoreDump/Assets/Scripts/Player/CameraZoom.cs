using Unity.Cinemachine;
using UnityEngine;

namespace CoreDump
{
    // Pulls the camera back and up as the robot grows, so the whole build stays in view.
    public class CameraZoom : MonoBehaviour
    {
        public RobotBody robot;
        public AimRig aim;
        public CinemachineThirdPersonFollow follow;
        public float baseDistance = 4f;
        [Tooltip("Extra distance per meter of the robot's half-size (from its center to a corner of its bounds).")]
        public float distancePerSize = 1.4f;
        [Tooltip("How far above the robot's middle the camera looks from, at least. Taller robots raise it by half their height.")]
        public float pivotAboveCenter = 1.2f;
        public float smoothing = 3f;

        void LateUpdate()
        {
            if (!robot || !follow) return;
            var bounds = robot.LocalBounds;
            float t = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            follow.CameraDistance = Mathf.Lerp(follow.CameraDistance, baseDistance + distancePerSize * bounds.extents.magnitude, t);
            // Pivot from the robot's middle, not its top, so Legs underneath stay in frame.
            if (aim) aim.offset.y = Mathf.Lerp(aim.offset.y, bounds.center.y + Mathf.Max(pivotAboveCenter, bounds.extents.y * 0.5f), t);
        }
    }
}
