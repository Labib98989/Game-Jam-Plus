using UnityEngine;

namespace CoreDump
{
    public interface IDamageable
    {
        void TakeHit(float damage, Vector3 direction);
    }
}
