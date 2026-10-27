using UnityEngine;

namespace CoreDump
{
    // Whole-number grid helpers. A part's orientation is one of the 24 rotations that map grid
    // axes onto grid axes; Snap keeps a rotation exactly on that set.
    public static class GridMath
    {
        // The way every part points in its own grid.
        public static readonly Vector3Int Forward = new(0, 0, 1);

        public static readonly Vector3Int[] Directions =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new(0, 0, 1), new(0, 0, -1),
        };

        public static Vector3Int Rotate(Quaternion rotation, Vector3Int v) => Vector3Int.RoundToInt(rotation * (Vector3)v);

        public static Quaternion Snap(Quaternion rotation) =>
            Quaternion.LookRotation(Axis(rotation * Vector3.forward), Axis(rotation * Vector3.up));

        // The grid axis closest to a direction.
        public static Vector3Int Axis(Vector3 v)
        {
            float x = Mathf.Abs(v.x), y = Mathf.Abs(v.y), z = Mathf.Abs(v.z);
            if (x >= y && x >= z) return new Vector3Int(v.x > 0f ? 1 : -1, 0, 0);
            if (y >= z) return new Vector3Int(0, v.y > 0f ? 1 : -1, 0);
            return new Vector3Int(0, 0, v.z > 0f ? 1 : -1);
        }
    }
}
