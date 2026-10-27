using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CoreDump
{
    // A burst lunge toward wherever the Ram points, and its tip hurts whatever it hits. Pointing
    // down in mid-air it's a butt slam whose landing knocks enemies away; pointing up it's a
    // rocket jump. One lunge per jump, reset on landing.
    public class Ram : PartModule
    {
        bool spentInAir;
        bool slamming;
        float readyTime;
        readonly HashSet<IDamageable> hitThisLunge = new();

        RamDefinition Tuning => (RamDefinition)Definition;

        protected override void OnFitted() => Robot.Motor.Landed += OnLanded;
        void OnDestroy()
        {
            if (Robot) Robot.Motor.Landed -= OnLanded;
        }

        public override void OnButtonDown()
        {
            bool grounded = Robot.Motor.IsGrounded;
            if (grounded ? Time.time < readyTime : spentInAir) return;
            readyTime = Time.time + Tuning.groundCooldown / Boost(ModType.Recharge);
            if (!grounded) spentInAir = true;

            // Rams on the same button stack: each lunges with its share of the stack's total.
            int count = Robot.PartsOn(Button).Count(p => p is Ram);
            float speed = Stacking.Total(Tuning.lungeSpeed, count, Tuning.stackFalloff, Tuning.maxLungeSpeed) / count * Boost(ModType.Power);
            Robot.Motor.Lunge(Direction * speed, Tuning.steerLock, Tuning.keep);
            Juice.Play(Sound.Ram);
            slamming = !grounded && Direction.y < -0.7f;
            hitThisLunge.Clear();
        }

        void FixedUpdate()
        {
            if (!Robot.Motor.IsLunging && !slamming) return;
            // The tip is the cube furthest along the way the Ram points.
            var tip = transform.TransformPoint(Definition.cubes.OrderByDescending(c => c.z).First());
            foreach (var hit in Physics.OverlapSphere(tip, Tuning.tipRadius, Robot.shotMask, QueryTriggerInteraction.Ignore))
            {
                var target = hit.GetComponentInParent<IDamageable>();
                if (target == null || !hitThisLunge.Add(target)) continue;
                target.TakeHit(Tuning.tipDamage * Boost(ModType.Damage), Direction);
            }
        }

        void OnLanded()
        {
            spentInAir = false;
            if (!slamming) return;
            slamming = false;

            // The shockwave starts at the robot's feet.
            var center = Robot.transform.TransformPoint(new Vector3(0f, Robot.BottomHeight, 0f));
            var hitTargets = new HashSet<IDamageable>();
            foreach (var hit in Physics.OverlapSphere(center, Tuning.slamRadius, Robot.shotMask, QueryTriggerInteraction.Ignore))
            {
                var target = hit.GetComponentInParent<IDamageable>();
                if (target != null && hitTargets.Add(target)) target.TakeHit(Tuning.slamDamage * Boost(ModType.Damage), (hit.transform.position - center).normalized);
                if (hit.attachedRigidbody) hit.attachedRigidbody.AddExplosionForce(Tuning.slamKnockback, center, Tuning.slamRadius, 1f, ForceMode.VelocityChange);
            }
            FxGrow.Spawn(PrimitiveType.Cylinder, center + Vector3.up * 0.05f, new Vector3(Tuning.slamRadius * 2f, 0.02f, Tuning.slamRadius * 2f), 0.2f, Tuning.shockwaveMaterial);
            Juice.Play(Sound.Slam, center);
            Juice.Dust(center, Tuning.slamRadius);
            Juice.Shake(0.5f);
        }
    }
}
