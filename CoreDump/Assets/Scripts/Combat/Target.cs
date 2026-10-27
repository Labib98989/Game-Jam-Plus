using System.Collections.Generic;
using UnityEngine;

namespace CoreDump
{
    // Anything the robot can shoot: enemies, and the test room's dummies. Every live one is listed in
    // All, so guns that aim themselves, homing rockets and the Thermal Eye can find them.
    public abstract class Target : MonoBehaviour, IDamageable
    {
        static readonly List<Target> all = new();

        protected Renderer[] renderers = System.Array.Empty<Renderer>();

        public static IReadOnlyList<Target> All => all;
        public abstract bool IsAlive { get; }

        // Where to aim: the middle of what you can see of it.
        public virtual Vector3 AimPoint
        {
            get
            {
                if (renderers.Length == 0 || !renderers[0]) return transform.position;
                var bounds = renderers[0].bounds;
                foreach (var r in renderers)
                    if (r) bounds.Encapsulate(r.bounds);
                return bounds.center;
            }
        }

        protected virtual void OnEnable() => all.Add(this);
        protected virtual void OnDisable() => all.Remove(this);

        public abstract void TakeHit(float damage, Vector3 direction);
    }
}
