using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoreDump
{
    // Turns room data into objects: a room to play in, or the room editor's clickable pieces.
    public static class RoomBuilder
    {
        public const float LampRange = 22f;
        // Pads sit on a layer raycasts skip, so they never block a shot, the grapple or the walking map.
        public const int PadLayer = 2;
        public const float LampIntensity = 3f;
        static readonly Color LampColor = new(1f, 0.9f, 0.75f);

        // Builds a room to play in: rolls the "sometimes" pieces, leaves the markers out (the run
        // reads them from the data), and merges the visuals so the room draws fast.
        public static Transform Build(RoomData room, RoomKit kit, Transform parent, System.Random random)
        {
            var root = new GameObject($"Room: {room.name}");
            root.transform.SetParent(parent, false);
            foreach (var piece in room.pieces)
            {
                var kind = RoomPieces.Get(piece.kind);
                if (kind == null || kind.IsMarker) continue;
                if (piece.chance < 100 && random.Next(100) >= piece.chance) continue;
                CreatePiece(piece, kind, kit, root.transform, inEditor: false);
            }
            var owned = root.AddComponent<OwnedMeshes>();
            owned.meshes.AddRange(root.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh));
            StaticBatchingUtility.Combine(root);
            // Merging can hand the pieces a new, combined mesh; that one goes with the room too.
            owned.meshes.AddRange(root.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).Distinct().Where(m => !owned.meshes.Contains(m)).ToList());
            return root.transform;
        }

        // One piece's object, centered on the piece. In the editor every piece can be clicked, and
        // markers, and pieces that only sometimes appear, show see-through.
        public static GameObject CreatePiece(RoomPiece piece, PieceKind kind, RoomKit kit, Transform parent, bool inEditor)
        {
            var go = new GameObject(kind.displayName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = piece.Center;
            var size = (Vector3)piece.size;
            bool seeThrough = inEditor && (kind.IsMarker || piece.chance < 100);
            var material = seeThrough ? kit.preview : kit.For(kind.slot);

            switch (kind.shape)
            {
                case PieceShape.Box:
                    AddMesh(go, BoxMesh(size, piece.Center), material, seeThrough || !kind.castsShadow);
                    go.AddComponent<BoxCollider>().size = size;
                    break;

                case PieceShape.Pad:
                    // A glowing plate on the floor of its cell; stepping into the cell launches you.
                    // Arrows stream along the path it throws you (PadGuide), in the editor too.
                    go.layer = PadLayer;
                    Child(go, "Plate", new Vector3(0f, -size.y * 0.5f + 0.1f, 0f), new Vector3(size.x * 0.9f, 0.2f, size.z * 0.9f), material);
                    if (kind.launchForward > 0f)
                    {
                        // A flat arrowhead on the plate, pointing the way it throws you.
                        var arrow = new GameObject("Arrow");
                        arrow.transform.SetParent(go.transform, false);
                        arrow.transform.SetLocalPositionAndRotation(new Vector3(0f, -size.y * 0.5f + 0.3f, 0f), Quaternion.LookRotation(piece.Facing));
                        AddMesh(arrow, ChevronMesh(Mathf.Min(size.x, size.z) * 0.75f), material, true);
                    }
                    Trigger(go, size);
                    var launch = Vector3.up * kind.launchUp + piece.Facing * kind.launchForward;
                    if (!inEditor) go.AddComponent<JumpPad>().launch = launch;
                    var guide = go.AddComponent<PadGuide>();
                    guide.launch = launch;
                    guide.material = material;
                    guide.surface = -size.y * 0.5f + 0.2f;
                    break;

                case PieceShape.Ramp:
                    var ramp = RampMesh(piece);
                    AddMesh(go, ramp, material, seeThrough);
                    var collider = go.AddComponent<MeshCollider>();
                    collider.sharedMesh = ramp;
                    collider.convex = true;
                    break;

                case PieceShape.Lamp:
                    AddMesh(go, BoxMesh(Vector3.one * 0.4f, piece.Center), material, seeThrough);
                    var light = go.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.range = LampRange;
                    light.intensity = LampIntensity;
                    light.color = LampColor;
                    light.shadows = LightShadows.None;
                    if (inEditor) Trigger(go, size);
                    break;

                case PieceShape.Marker:
                    AddMesh(go, BoxMesh(size * 0.9f, piece.Center), material, true);
                    Trigger(go, size);
                    // A thin plate on the side it sits against, and for the entry, a nose the way the robot will face.
                    var plate = new Vector3(piece.face.x != 0 ? 0.08f : 0.9f, piece.face.y != 0 ? 0.08f : 0.9f, piece.face.z != 0 ? 0.08f : 0.9f);
                    Child(go, "Surface", -(Vector3)piece.face * 0.5f, Vector3.Scale(plate, size), material);
                    if (piece.kind == RoomPieces.Entry) Child(go, "Facing", piece.Facing * 0.65f, Vector3.one * 0.3f, material);
                    break;
            }
            return go;
        }

        static void AddMesh(GameObject go, Mesh mesh, Material material, bool seeThrough)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (seeThrough) renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Trigger(GameObject go, Vector3 size)
        {
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
        }

        static void Child(GameObject parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = localPosition;
            AddMesh(child, BoxMesh(size, Vector3.zero), material, true);
        }

        // A box centered on its object. `origin` is where the object sits, for the UVs.
        public static Mesh BoxMesh(Vector3 size, Vector3 origin)
        {
            var b = new MeshBuilder(origin);
            AddBox(b, Vector3.zero, Quaternion.identity, size);
            return b.Finish("Room Box");
        }

        // An arrowhead `size` meters long, pointing along +z: two bars in a V, flat in the x-z plane.
        public static Mesh ChevronMesh(float size)
        {
            var b = new MeshBuilder(Vector3.zero);
            float bar = size * 0.24f, length = size * 0.85f;
            var tip = new Vector3(0f, 0f, size * 0.3f);
            for (int side = -1; side <= 1; side += 2)
            {
                var back = new Vector3(side, 0f, -1f).normalized;
                AddBox(b, tip + back * (length * 0.5f - bar * 0.5f), Quaternion.LookRotation(back), new Vector3(bar, bar * 0.5f, length));
            }
            return b.Finish("Chevron");
        }

        static void AddBox(MeshBuilder b, Vector3 center, Quaternion turn, Vector3 size)
        {
            var h = size * 0.5f;
            Vector3 C(float x, float y, float z) => center + turn * new Vector3(x * h.x, y * h.y, z * h.z);
            b.Quad(C(-1, 1, -1), C(-1, 1, 1), C(1, 1, 1), C(1, 1, -1), turn * Vector3.up);
            b.Quad(C(-1, -1, -1), C(1, -1, -1), C(1, -1, 1), C(-1, -1, 1), turn * Vector3.down);
            b.Quad(C(1, -1, -1), C(1, 1, -1), C(1, 1, 1), C(1, -1, 1), turn * Vector3.right);
            b.Quad(C(-1, -1, -1), C(-1, -1, 1), C(-1, 1, 1), C(-1, 1, -1), turn * Vector3.left);
            b.Quad(C(-1, -1, 1), C(1, -1, 1), C(1, 1, 1), C(-1, 1, 1), turn * Vector3.forward);
            b.Quad(C(-1, -1, -1), C(-1, 1, -1), C(1, 1, -1), C(1, -1, -1), turn * Vector3.back);
        }

        // A wedge filling the piece's box, rising toward the way the piece faces.
        public static Mesh RampMesh(RoomPiece piece)
        {
            // Built rising toward +z, then turned. A quarter turn swaps which world axis is its width.
            bool sideways = piece.turn % 2 == 1;
            float w = 0.5f * (sideways ? piece.size.z : piece.size.x);
            float l = 0.5f * (sideways ? piece.size.x : piece.size.z);
            float h = 0.5f * piece.size.y;
            var turn = Quaternion.Euler(0f, piece.turn * 90f, 0f);
            Vector3 P(float x, float y, float z) => turn * new Vector3(x, y, z);
            var b = new MeshBuilder(piece.Center);
            b.Quad(P(-w, -h, -l), P(w, -h, -l), P(w, -h, l), P(-w, -h, l), turn * Vector3.down);
            b.Quad(P(-w, -h, l), P(w, -h, l), P(w, h, l), P(-w, h, l), turn * Vector3.forward);
            b.Quad(P(-w, -h, -l), P(-w, h, l), P(w, h, l), P(w, -h, -l), turn * new Vector3(0f, l, -h).normalized);
            b.Triangle(P(-w, -h, -l), P(-w, -h, l), P(-w, h, l), turn * Vector3.left);
            b.Triangle(P(w, -h, -l), P(w, h, l), P(w, -h, l), turn * Vector3.right);
            return b.Finish("Room Ramp");
        }

        // Builds a mesh face by face, with flat normals, turning each face to face out. UVs are in
        // world meters, so a grid texture lines up across pieces and never stretches.
        sealed class MeshBuilder
        {
            readonly Vector3 origin;
            readonly List<Vector3> vertices = new();
            readonly List<Vector3> normals = new();
            readonly List<Vector2> uvs = new();
            readonly List<int> triangles = new();

            public MeshBuilder(Vector3 origin) => this.origin = origin;

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int i = Add(a, normal);
                Add(b, normal);
                Add(c, normal);
                Add(d, normal);
                if (FacesOut(a, b, c, normal)) triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                else triangles.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
            {
                int i = Add(a, normal);
                Add(b, normal);
                Add(c, normal);
                triangles.AddRange(FacesOut(a, b, c, normal) ? new[] { i, i + 1, i + 2 } : new[] { i, i + 2, i + 1 });
            }

            // Unity draws a triangle's front where its corners run clockwise, which is where this cross product points.
            static bool FacesOut(Vector3 a, Vector3 b, Vector3 c, Vector3 normal) => Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0f;

            int Add(Vector3 position, Vector3 normal)
            {
                vertices.Add(position);
                normals.Add(normal);
                var world = origin + position;
                float x = Mathf.Abs(normal.x), y = Mathf.Abs(normal.y), z = Mathf.Abs(normal.z);
                uvs.Add(y >= x && y >= z ? new Vector2(world.x, world.z) : x >= z ? new Vector2(world.z, world.y) : new Vector2(world.x, world.y));
                return vertices.Count - 1;
            }

            public Mesh Finish(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
