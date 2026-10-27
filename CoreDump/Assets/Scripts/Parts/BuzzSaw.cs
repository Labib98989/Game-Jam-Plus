using System.Collections.Generic;
using UnityEngine;

namespace CoreDump
{
    // A blade spinning across the +Z face of its 2x2 housing, hurting anything it touches.
    public class BuzzSaw : PartModule
    {
        static readonly Vector3 BladeCenter = new(0.5f, 0.5f, 0.56f);
        static readonly Vector3 BladeHalfSize = new(1.2f, 1.2f, 0.35f);

        Transform blade;
        readonly Dictionary<IDamageable, float> nextHit = new();

        BuzzSawDefinition Tuning => (BuzzSawDefinition)Definition;

        protected override void OnFitted()
        {
            blade = new GameObject("Blade").transform;
            blade.SetParent(transform, false);
            blade.localPosition = BladeCenter;
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(blade, false);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localScale = new Vector3(2.3f, 0.04f, 2.3f);
            disc.GetComponent<Renderer>().sharedMaterial = Tuning.bladeMaterial;
            for (int i = 0; i < 8; i++)
            {
                var angle = i * 45f * Mathf.Deg2Rad;
                var tooth = RobotBody.CreateBlock(blade, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 1.2f, new Vector3(0.22f, 0.22f, 0.08f), Tuning.bladeMaterial);
                tooth.transform.localRotation = Quaternion.Euler(0f, 0f, i * 45f + 45f);
            }
        }

        void Update()
        {
            if (blade) blade.Rotate(0f, 0f, Tuning.spinSpeed * Boost(ModType.FireRate) * Time.deltaTime, Space.Self);
        }

        void FixedUpdate()
        {
            var center = transform.TransformPoint(BladeCenter);
            foreach (var collider in Physics.OverlapBox(center, Vector3.Scale(BladeHalfSize, transform.lossyScale), transform.rotation, Robot.shotMask, QueryTriggerInteraction.Ignore))
            {
                var target = collider.GetComponentInParent<IDamageable>();
                if (target == null || (nextHit.TryGetValue(target, out var when) && Time.time < when)) continue;
                // Fire Rate mods make it hit more often, Damage mods make each hit harder.
                nextHit[target] = Time.time + Tuning.hitInterval / Boost(ModType.FireRate);
                target.TakeHit(Tuning.damagePerSecond * Tuning.hitInterval * Boost(ModType.Damage), transform.forward);
            }
        }
    }
}
