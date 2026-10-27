using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CoreDump
{
    public enum PieceFolder { Blocks, Ramps, Pads, Props, Markers }
    public enum PieceShape { Box, Ramp, Pad, Lamp, Marker }
    public enum KitSlot { Floor, Wall, Ramp, Prop, Lamp, Pad }

    // A kind of room piece, as it sits in the room editor's folders.
    public sealed class PieceKind
    {
        public string id, displayName, description;
        public PieceFolder folder;
        public PieceShape shape;
        public KitSlot slot;
        // Its size when dropped in one go, in meters. Drawn as a rectangle, a block or ramp builds
        // out of the surface by size.y instead.
        public Vector3Int size = Vector3Int.one;
        public Color color;
        // Markers: the tag shown on it in the editor, and whether enemies come in there.
        public string tag;
        public bool spawn;
        // Pads: how fast they throw the robot up, and the way they face.
        public float launchUp, launchForward;
        // Ceilings let the sun through, so a closed room isn't left in the dark.
        public bool castsShadow = true;

        public bool IsMarker => shape == PieceShape.Marker;
        public bool Draws => folder is PieceFolder.Blocks or PieceFolder.Ramps;
        public bool Resizable => shape is PieceShape.Box or PieceShape.Ramp;
    }

    // Every kind of room piece. Room files refer to them by id, so ids never change.
    public static class RoomPieces
    {
        public const string Entry = "entry";
        public const string Exit = "exit";
        public const string Vent = "vent";
        public const string Ground = "ground";
        public const string Perch = "perch";
        public const string Air = "air";
        public const string Rescue = "rescue";
        public const string Shop = "shop";
        public const string Pod = "pod";

        public static readonly PieceKind[] All =
        {
            new() { id = "floor", displayName = "Floor", folder = PieceFolder.Blocks, shape = PieceShape.Box, slot = KitSlot.Floor,
                size = new Vector3Int(4, 1, 4), color = new Color(0.52f, 0.54f, 0.58f),
                description = "Floors, ledges and catwalks. Drawn on a surface, it builds 1 m out of it." },
            new() { id = "wall", displayName = "Wall", folder = PieceFolder.Blocks, shape = PieceShape.Box, slot = KitSlot.Wall,
                size = new Vector3Int(1, 6, 4), color = new Color(0.42f, 0.47f, 0.55f),
                description = "Walls to run along. Drawn on a surface, it builds 6 m out of it, so a line drawn on the floor makes a wall." },
            new() { id = "pillar", displayName = "Pillar", folder = PieceFolder.Blocks, shape = PieceShape.Box, slot = KitSlot.Wall,
                size = new Vector3Int(2, 10, 2), color = new Color(0.36f, 0.41f, 0.5f),
                description = "Tall pillars to grapple onto and climb. Drawn on a surface, it builds 10 m out of it." },
            new() { id = "ceiling", displayName = "Ceiling", folder = PieceFolder.Blocks, shape = PieceShape.Box, slot = KitSlot.Wall,
                size = new Vector3Int(8, 1, 8), color = new Color(0.3f, 0.33f, 0.4f), castsShadow = false,
                description = "Closes the room in, so nothing flies out. Drawn on a surface, it builds 1 m out of it. It casts no shadow, so the sun still lights the room." },
            new() { id = "ramp", displayName = "Ramp", folder = PieceFolder.Ramps, shape = PieceShape.Ramp, slot = KitSlot.Ramp,
                size = new Vector3Int(4, 2, 6), color = new Color(0.95f, 0.55f, 0.18f),
                description = "Rises toward the way it faces (Q/E turn it). Drawn on a floor, it rises 2 m across the rectangle. Floors only." },
            new() { id = "jumppad", displayName = "Jump Pad", folder = PieceFolder.Pads, shape = PieceShape.Pad, slot = KitSlot.Pad,
                size = new Vector3Int(3, 1, 3), color = new Color(0.3f, 0.95f, 1f), launchUp = 34f,
                description = "Throws the robot straight up, about 18 m. Put it on a floor, next to something worth getting on top of." },
            new() { id = "launchpad", displayName = "Launch Pad", folder = PieceFolder.Pads, shape = PieceShape.Pad, slot = KitSlot.Pad,
                size = new Vector3Int(3, 1, 3), color = new Color(0.45f, 1f, 0.6f), launchUp = 20f, launchForward = 26f,
                description = "Throws the robot up and away the way it faces (Q/E turn it), about 30 m across. For fast routes across a room." },
            new() { id = "crate", displayName = "Crate", folder = PieceFolder.Props, shape = PieceShape.Box, slot = KitSlot.Prop,
                size = new Vector3Int(2, 2, 2), color = new Color(0.7f, 0.48f, 0.25f),
                description = "Cover, and something to jump onto." },
            new() { id = "cover", displayName = "Cover", folder = PieceFolder.Props, shape = PieceShape.Box, slot = KitSlot.Prop,
                size = new Vector3Int(4, 2, 1), color = new Color(0.62f, 0.5f, 0.36f),
                description = "A low wall to hide behind." },
            new() { id = "lamp", displayName = "Lamp", folder = PieceFolder.Props, shape = PieceShape.Lamp, slot = KitSlot.Lamp,
                color = new Color(1f, 0.92f, 0.7f),
                description = "A light. Rooms have no baked lighting, so put a few in every room." },
            new() { id = Entry, displayName = "Entry", folder = PieceFolder.Markers, shape = PieceShape.Marker, tag = "ENTRY",
                color = new Color(0.3f, 1f, 0.45f),
                description = "Where the robot comes in, facing the way it points (Q/E turn it). One per room." },
            new() { id = Exit, displayName = "Exit", folder = PieceFolder.Markers, shape = PieceShape.Marker, tag = "EXIT",
                color = new Color(0.4f, 0.65f, 1f),
                description = "The way out. It unlocks when the last wave is dead." },
            new() { id = Vent, displayName = "Skitter Vent", folder = PieceFolder.Markers, shape = PieceShape.Marker, tag = "VENT", spawn = true,
                color = new Color(0.75f, 0.45f, 1f),
                description = "Skitters pour out of these." },
            new() { id = Ground, displayName = "Ground Spawn", folder = PieceFolder.Markers, shape = PieceShape.Marker, tag = "GROUND", spawn = true,
                color = new Color(1f, 0.38f, 0.32f),
                description = "Brutes and Stalkers come in here. Put them on the floor." },
            new() { id = Perch, displayName = "Perch", folder = PieceFolder.Markers, shape = PieceShape.Marker, tag = "PERCH", spawn = true,
                color = new Color(1f, 0.85f, 0.25f),
                description = "Spitters and Stalkers land here. Put them on ledges, walls and ceilings." },
            new() { id = Air, displayName = "Air Spawn", folder = PieceFolder.Markers, shape = PieceShape.Marker, tag = "AIR", spawn = true,
                color = new Color(0.4f, 0.95f, 1f),
                description = "Bombers and Hornets fly in from here. Put them high up." },
            new() { id = Rescue, displayName = "Rescue Door", folder = PieceFolder.Markers, shape = PieceShape.Marker, tag = "RESCUE",
                color = new Color(0.92f, 0.92f, 0.92f),
                description = "Where a rescue door can go. A run puts doors in 3 of its rooms." },
            new() { id = Shop, displayName = "Shop Stand", folder = PieceFolder.Markers, shape = PieceShape.Marker, tag = "SHOP",
                color = new Color(1f, 0.62f, 0.2f),
                description = "Workshop rooms: a stand holding one free part. Walk into it to take the part." },
            new() { id = Pod, displayName = "Escape Pod", folder = PieceFolder.Markers, shape = PieceShape.Marker, tag = "POD",
                color = new Color(1f, 0.45f, 0.75f),
                description = "The escape shaft: where the pod waits. Reaching it wins the run." },
        };

        static readonly Dictionary<string, PieceKind> ById = All.ToDictionary(k => k.id);

        // Null for an id no piece has (a piece renamed since the room was saved, say).
        public static PieceKind Get(string id) => id != null && ById.TryGetValue(id, out var kind) ? kind : null;
    }
}
