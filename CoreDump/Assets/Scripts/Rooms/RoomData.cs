using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CoreDump
{
    public enum RoomSize { Small, Medium, Large }

    // Combat rooms are where the fights are. The workshop comes between them, with its free parts and
    // building, and the escape shaft is the climb at the end.
    public enum RoomType { Combat, Workshop, Shaft, Tutorial }

    // One piece of a room: what it is, the box of 1 m cells it fills, which way it faces, and its
    // chance to appear each time the room loads.
    [System.Serializable]
    public class RoomPiece
    {
        // A RoomPieces id.
        public string kind;
        // Its lowest corner cell, and its size in cells (meters), in the room's own axes.
        public Vector3Int min;
        public Vector3Int size = Vector3Int.one;
        // Quarter turns around up: which way a ramp rises, or a marker faces. 0 is +z.
        public int turn;
        // Which way the surface it was put on faces: up on a floor, down on a ceiling, sideways on a wall.
        public Vector3Int face = Vector3Int.up;
        // Percent chance to appear each time the room loads.
        public int chance = 100;

        // One past its highest corner cell.
        public Vector3Int Max => min + size;
        public Vector3 Center => (Vector3)min + (Vector3)size * 0.5f;
        // The middle of its bottom face: where the robot stands on an entry.
        public Vector3 Bottom => new(min.x + size.x * 0.5f, min.y, min.z + size.z * 0.5f);
        public Vector3 Facing => Quaternion.Euler(0f, turn * 90f, 0f) * Vector3.forward;

        public bool Overlaps(RoomPiece other) =>
            min.x < other.Max.x && other.min.x < Max.x &&
            min.y < other.Max.y && other.min.y < Max.y &&
            min.z < other.Max.z && other.min.z < Max.z;

        public RoomPiece Copy() => (RoomPiece)MemberwiseClone();
    }

    // A room as the room editor saves it: a list of pieces on a 1 m grid. Enemies are never part of
    // it; the run picks them each time.
    [System.Serializable]
    public class RoomData
    {
        public int version = 1;
        public string name = "Room";
        public RoomSize size;
        public RoomType type;
        public List<RoomPiece> pieces = new();

        public RoomData Copy() => new() { version = version, name = name, size = size, type = type, pieces = pieces.Select(p => p.Copy()).ToList() };

        public IEnumerable<RoomPiece> All(string kind) => pieces.Where(p => p.kind == kind);

        public string ToJson() => JsonUtility.ToJson(this, true);

        public static RoomData FromJson(string json)
        {
            var room = JsonUtility.FromJson<RoomData>(json);
            if (room != null) room.pieces ??= new List<RoomPiece>();
            return room;
        }

        // The same room flipped left to right, in place: boxes, ramps, facings and surfaces all flip.
        public RoomData Mirrored()
        {
            var flipped = Copy();
            if (flipped.pieces.Count == 0) return flipped;
            // Flip around the middle of the room, so it stays where it was.
            int sum = pieces.Min(p => p.min.x) + pieces.Max(p => p.Max.x);
            foreach (var p in flipped.pieces)
            {
                p.min.x = sum - p.Max.x;
                p.turn = (4 - p.turn) % 4;
                p.face.x = -p.face.x;
            }
            return flipped;
        }
    }
}
