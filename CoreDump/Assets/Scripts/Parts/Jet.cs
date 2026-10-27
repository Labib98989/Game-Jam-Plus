using System.Linq;
using UnityEngine;

namespace CoreDump
{
    // Pushes the robot away from the way its nozzle points: on the back it shoves you forward,
    // underneath it lifts you. A tap is a sharp dash; holding keeps pushing. Fuel runs out in the
    // air and refills on the ground.
    public class Jet : PartModule
    {
        bool held;
        float nextDash;
        float flashUntil;
        GameObject flame;

        JetDefinition Tuning => (JetDefinition)Definition;
        Vector3 PushDirection => -Direction;
        bool Burning => held && Fuel > 0f && IsWorking;
        public float Fuel { get; private set; } = 1f;

        protected override void OnFitted()
        {
            flame = RobotBody.CreateBlock(transform, (Vector3)Definition.nozzleCube + new Vector3(0f, 0f, 0.8f),
                new Vector3(0.36f, 0.36f, 0.6f), Tuning.flameMaterial);
            flame.SetActive(false);
        }

        public override void OnButtonDown()
        {
            held = true;
            if (Fuel <= 0f || Time.time < nextDash) return;
            nextDash = Time.time + Tuning.dashCooldown / Boost(ModType.Recharge);
            if (!Robot.Motor.IsGrounded) Fuel = Mathf.Max(0f, Fuel - Tuning.dashFuel);

            // Jets on the same button stack: each dashes with its share of the stack's total.
            int count = Robot.PartsOn(Button).Count(p => p is Jet && p.IsWorking);
            float speed = Stacking.Total(Tuning.dashSpeed, count, Tuning.stackFalloff, Tuning.maxDashSpeed) / count * Boost(ModType.Power);
            Robot.Motor.Lunge(PushDirection * speed, Tuning.dashLock, Tuning.dashKeep);
            flashUntil = Time.time + 0.15f;
            Juice.Play(Sound.Dash);
        }

        public override void OnButtonUp() => held = false;

        void FixedUpdate()
        {
            if (Robot.Motor.IsGrounded) Fuel = 1f;
            if (!Burning || Robot.Motor.IsLunging) return;
            if (!Robot.Motor.IsGrounded) Fuel = Mathf.Max(0f, Fuel - Time.fixedDeltaTime / (Tuning.fuelTime * Boost(ModType.Recharge)));

            int count = Robot.PartsOn(Button).Count(p => p is Jet && p.IsWorking);
            float push = Stacking.Total(Tuning.thrust, count, Tuning.stackFalloff, Tuning.maxThrust) / count * Boost(ModType.Power);
            if (Vector3.Dot(Robot.Motor.Velocity, PushDirection) < Tuning.maxSpeed)
                Robot.Motor.Thrust(PushDirection * push);
        }

        void Update()
        {
            if (flame) flame.SetActive(Time.time < flashUntil || Burning);
        }
    }
}
