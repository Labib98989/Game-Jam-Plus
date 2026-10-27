using UnityEngine;

namespace CoreDump
{
    // More of the same part on one button makes a stronger version, each extra one adding a bit
    // less than the last, up to a ceiling so silly stacks don't launch you into space.
    public static class Stacking
    {
        public static float Total(float one, int count, float falloff, float ceiling)
        {
            float total = 0f, step = one;
            for (int i = 0; i < count; i++)
            {
                total += step;
                step *= falloff;
            }
            return Mathf.Min(total, ceiling);
        }
    }
}
