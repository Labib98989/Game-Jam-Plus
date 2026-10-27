using System.Linq;
using UnityEngine;

namespace CoreDump
{
    // Pushes off whatever surface its pad touches: pointing down it jumps, pointing at a wall it's a
    // wall-jump. While wall-running the Legs are on the wall, so every Spring kicks off it. Springs
    // on the same button stack, and a press just before reaching a surface still counts.
    public class Spring : PartModule
    {
        float pressedAt = -1f;

        SpringDefinition Tuning => (SpringDefinition)Definition;

        public override void OnButtonDown() => pressedAt = Time.time;

        void FixedUpdate()
        {
            if (pressedAt < 0f) return;
            if (Time.time > pressedAt + Tuning.bufferTime)
            {
                pressedAt = -1f;
                return;
            }
            if (!CanPush(out var normal)) return;
            pressedAt = -1f;

            // Each Spring that can push adds its share of the stack's total.
            int count = Robot.PartsOn(Button).OfType<Spring>().Count(s => s.CanPush(out _));
            float height = Stacking.Total(Tuning.jumpHeight, count, Tuning.stackFalloff, Tuning.maxJumpHeight) / count * Boost(ModType.Power);
            Juice.Play(Sound.Spring);
            if (normal.y > 0.7f)
            {
                Robot.Motor.Jump(height);
                return;
            }
            float speed = Robot.Motor.JumpSpeed(height);
            var away = new Vector3(normal.x, 0f, normal.z).normalized;
            Robot.Motor.SpringOff(away * speed * Tuning.wallPush + Vector3.up * speed * Tuning.wallLift);
        }

        // Whether the pad is against a surface (or the robot is wall-running), and which way that surface faces.
        bool CanPush(out Vector3 normal)
        {
            normal = Vector3.up;
            if (!IsWorking) return false;
            var pad = transform.TransformPoint(Definition.nozzleCube);
            if (Physics.Raycast(pad, Direction, out var hit, 0.5f * Robot.cellSize + Tuning.reach, Robot.shotMask, QueryTriggerInteraction.Ignore))
            {
                normal = hit.normal;
                return true;
            }
            if (!Robot.Motor.IsWallRunning) return false;
            normal = Robot.Motor.WallNormal;
            return true;
        }
    }
}
