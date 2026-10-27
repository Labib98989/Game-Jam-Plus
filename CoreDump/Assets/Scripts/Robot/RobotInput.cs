using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CoreDump
{
    // WASD, the four part buttons, Ctrl to slide and R to reload every gun. Every part on a button
    // fires together, except parts that can't work where they are (a blocked barrel, a Leg off the ground).
    [RequireComponent(typeof(RobotBody))]
    public class RobotInput : MonoBehaviour
    {
        public AimRig aim;

        RobotBody robot;
        bool hadControl;
        // Which buttons went down while we had control, so the click that grabs the mouse doesn't fire.
        readonly bool[] down = new bool[5];
        readonly List<PartModule> firing = new();

        void Awake() => robot = GetComponent<RobotBody>();

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (aim) robot.Motor.SetFacing(aim.Yaw);

            if (!aim || !aim.HasControl || kb == null || mouse == null)
            {
                robot.Motor.SetMoveInput(Vector2.zero);
                robot.Motor.SetSlideInput(false);
                // Let go of everything, so a held Jet doesn't keep pushing while you build.
                if (hadControl)
                    foreach (var part in robot.Parts) part.OnButtonUp();
                hadControl = false;
                System.Array.Clear(down, 0, down.Length);
                return;
            }
            hadControl = true;

            robot.Motor.SetMoveInput(new Vector2(
                (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f)));
            robot.Motor.SetSlideInput(kb.leftCtrlKey.isPressed || kb.cKey.isPressed);
            if (kb.rKey.wasPressedThisFrame)
                foreach (var weapon in robot.Parts.OfType<Weapon>()) weapon.Reload();

            Fire(ButtonSlot.LeftMouse, mouse.leftButton);
            Fire(ButtonSlot.RightMouse, mouse.rightButton);
            Fire(ButtonSlot.Space, kb.spaceKey);
            Fire(ButtonSlot.Shift, kb.leftShiftKey);
        }

        void Fire(ButtonSlot slot, ButtonControl button)
        {
            int i = (int)slot;
            if (button.wasPressedThisFrame) down[i] = true;
            else if (!down[i]) return;

            firing.Clear();
            firing.AddRange(robot.PartsOn(slot));
            foreach (var part in firing)
            {
                if (button.wasPressedThisFrame && part.IsWorking) part.OnButtonDown();
                else if (button.isPressed && part.IsWorking) part.OnButtonHeld();
                // Also on a tap so quick it went down and up in one frame.
                if (!button.isPressed) part.OnButtonUp();
            }
            if (!button.isPressed) down[i] = false;
        }
    }
}
