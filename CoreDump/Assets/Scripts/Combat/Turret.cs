using UnityEngine;

namespace CoreDump
{
    // A stationary alien turret. It turns toward the robot and fires slow shots while it can see
    // it, each at a random cube of the robot, so parts fly off as well as the body getting hit.
    // Shoot it or slam into it to knock it out for a few seconds.
    [RequireComponent(typeof(TargetDummy))]
    public class Turret : MonoBehaviour
    {
        public Transform target;
        [Tooltip("The part that turns to aim. Shots come out along its forward direction.")]
        public Transform head;
        public float range = 18f;
        public float interval = 1.6f;
        public float shotSpeed = 14f;
        public float turnRate = 6f;
        public Material shotMaterial;
        public LayerMask sightMask = ~0;

        TargetDummy life;
        float nextShot;
        // The robot cube the next shot goes for, in the robot's own space.
        Vector3 aimLocal = Vector3.up;

        void Awake()
        {
            life = GetComponent<TargetDummy>();
            nextShot = Time.time + interval;
        }

        void Update()
        {
            if (!target || !head || !life.IsAlive) return;

            var aimAt = target.TransformPoint(aimLocal);
            head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(aimAt - head.position), turnRate * Time.deltaTime);
            if (Time.time < nextShot || (aimAt - head.position).magnitude > range) return;

            // Only fire when nothing is in the way.
            var muzzle = head.position + head.forward * 1.6f;
            var toTarget = aimAt - muzzle;
            if (!Physics.Raycast(muzzle, toTarget.normalized, out var hit, range + 2f, sightMask, QueryTriggerInteraction.Ignore)) return;
            if (!hit.rigidbody || hit.rigidbody.transform != target)
            {
                PickAimPoint();
                return;
            }

            nextShot = Time.time + interval;
            Projectile.Fire(muzzle, toTarget, shotSpeed, shotMaterial, sightMask);
            PickAimPoint();
        }

        void PickAimPoint()
        {
            var cubes = target.GetComponentsInChildren<BoxCollider>();
            if (cubes.Length > 0) aimLocal = target.InverseTransformPoint(cubes[Random.Range(0, cubes.Length)].bounds.center);
        }
    }
}
