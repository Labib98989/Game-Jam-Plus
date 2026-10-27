using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoreDump
{
    // The parts the robot starts with, respawning, and the test-room HUD.
    [RequireComponent(typeof(RobotBody))]
    public class StartingLoadout : MonoBehaviour
    {
        [System.Serializable]
        public struct Mount
        {
            public PartDefinition part;
            public Vector3Int anchor;
            [Tooltip("Rotation in the robot's own space, in steps of 90 degrees. (0,0,0) points the part forward.")]
            public Vector3 rotation;
        }

        public Mount[] mounts;
        public float respawnBelowY = -20f;
        [Tooltip("The test room's extras: its help box, Backspace to respawn, and respawning after a fall. A run handles those itself.")]
        public bool testControls = true;

        RobotBody robot;
        BuildMode build;
        Vector3 spawnPosition;
        float peakHeight;
        float lastPeak;

        void Start()
        {
            robot = GetComponent<RobotBody>();
            build = GetComponent<BuildMode>();
            spawnPosition = transform.position;
            Fit();
        }

        // Fits the starting parts and stands the robot on its lowest cube (its Legs, say) at the spawn point.
        void Fit()
        {
            foreach (var mount in mounts)
                if (mount.part) robot.Place(mount.part, mount.anchor, Quaternion.Euler(mount.rotation));
            robot.Motor.Teleport(spawnPosition + Vector3.up * -robot.LocalBounds.min.y);
        }

        // Back to the start: the body where it began, starting parts and inventory, full health,
        // knocked-off parts cleared away.
        public void Respawn()
        {
            PartChunk.ClearAll();
            robot.RemoveAll();
            robot.MoveBody(Vector3Int.zero, Quaternion.identity);
            GetComponent<PartInventory>()?.ResetStock();
            Fit();
            GetComponent<RobotHealth>()?.ResetHealth();
        }

        // Respawns somewhere else, from now on. The room editor drops the robot in at a room's entry.
        public void RespawnAt(Vector3 position)
        {
            spawnPosition = position;
            Respawn();
        }

        bool Building => build && build.IsOpen;
        // Height of the robot's lowest point, so Legs underneath don't count as height.
        float FeetHeight => transform.position.y + robot.LocalBounds.min.y;

        void Update()
        {
            if (Building || !testControls) return;
            var kb = Keyboard.current;
            if (kb != null && kb.backspaceKey.wasPressedThisFrame) Respawn();
            if (transform.position.y < respawnBelowY) Respawn();

            // Highest point of the last jump, to compare tuning changes.
            if (robot.Motor.IsGrounded)
            {
                if (peakHeight > 0f) lastPeak = peakHeight;
                peakHeight = 0f;
            }
            else peakHeight = Mathf.Max(peakHeight, FeetHeight);
        }

        void OnGUI()
        {
            if (Building || !testControls) return;
            var v = robot.Motor.Velocity;
            var buttons = new[] { ButtonSlot.LeftMouse, ButtonSlot.RightMouse, ButtonSlot.Space, ButtonSlot.Shift }
                .Select(b => $"{b.Label()}: {Describe(b)}");
            var fuel = robot.Parts.OfType<Jet>().Select(j => j.Fuel).DefaultIfEmpty(0f).Min();
            string text =
                "WASD move    Mouse aim    Ctrl slide    R reload    Tab build mode    Backspace respawn    Esc free the mouse\n" +
                string.Join("    ", buttons) + "\n" +
                $"Walking: {robot.Motor.WalkMode}    Speed {new Vector2(v.x, v.z).magnitude:0.0}    Height {FeetHeight:0.0}    Last peak {lastPeak:0.0}    Jet fuel {fuel * 100f:0}%";
            GUI.Box(new Rect(10f, 10f, 780f, 62f), GUIContent.none);
            GUI.Label(new Rect(18f, 14f, 770f, 58f), text);
        }

        string Describe(ButtonSlot button)
        {
            var names = robot.PartsOn(button).GroupBy(p => p.Definition.displayName).Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key).ToList();
            return names.Count > 0 ? string.Join(", ", names) : "-";
        }
    }
}
