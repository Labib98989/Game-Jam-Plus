using UnityEngine;
using UnityEngine.InputSystem;

namespace CoreDump
{
    // Mouse look. A pivot that follows the robot and turns with the mouse; the Cinemachine
    // camera follows this pivot, and the robot turns to match its yaw.
    [DefaultExecutionOrder(-100)]
    public class AimRig : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new(0f, 2.6f, 0f);
        public float sensitivity = 0.12f;
        public float minPitch = -70f;
        public float maxPitch = 80f;
        public LayerMask aimMask = ~0;
        public float maxAimDistance = 300f;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; } = 8f;
        // Locked mouse, and not the frame the click that locked it happened.
        public bool HasControl => !suspended && Cursor.lockState == CursorLockMode.Locked && Time.frameCount != lockFrame;

        // While build mode has the mouse: no aiming, and clicks don't grab the mouse.
        public bool Suspended
        {
            get => suspended;
            set
            {
                suspended = value;
                if (value) SetLocked(false);
            }
        }

        Camera cam;
        int lockFrame = -1;
        bool suspended;

        void Start()
        {
            cam = Camera.main;
            Yaw = target ? target.eulerAngles.y : transform.eulerAngles.y;
        }

        void Update()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null || suspended) return;

            if (Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame) SetLocked(true);
            else if (Cursor.lockState == CursorLockMode.Locked && kb != null && kb.escapeKey.wasPressedThisFrame) SetLocked(false);

            if (HasControl)
            {
                var delta = mouse.delta.ReadValue() * sensitivity;
                Yaw += delta.x;
                Pitch = Mathf.Clamp(Pitch - delta.y, minPitch, maxPitch);
            }
        }

        void LateUpdate()
        {
            if (target) transform.position = target.position + offset;
            transform.rotation = Quaternion.Euler(Pitch, Yaw, 0f);
        }

        // What's under the crosshair, from the camera's point of view.
        public Vector3 GetAimPoint()
        {
            if (!cam) cam = Camera.main;
            if (!cam) return transform.position + transform.forward * maxAimDistance;
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            return Physics.Raycast(ray, out var hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.GetPoint(maxAimDistance);
        }

        // Turns the view to face this way. The room editor's test faces the way the room's entry points.
        public void Face(float yaw) => Yaw = yaw;

        public void SetLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            if (locked) lockFrame = Time.frameCount;
        }

        // The crosshair itself is part of RobotHud, so it can break loose with the rest of the HUD.
        void OnGUI()
        {
            if (suspended || Cursor.lockState == CursorLockMode.Locked) return;
            GUI.Label(new Rect(Screen.width * 0.5f - 60f, Screen.height * 0.5f + 16f, 120f, 22f), "Click to play");
        }
    }
}
