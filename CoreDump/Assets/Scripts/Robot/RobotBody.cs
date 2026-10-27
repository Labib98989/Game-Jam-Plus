using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CoreDump
{
    // The robot's block body and the parts fitted to it, all on one grid of whole-number cells in
    // the robot's own space (each cell `cellSize` meters across). Every body and part cube has its
    // own box collider, so build mode can aim at it, shots can hit it and the robot can stand on its
    // parts. What a part can attach to is up to the parts around it: the body and connectors hold
    // anything, mods hold other mods, and the head holds vision mods. Mods of the same type that
    // touch form chains, which boost the parts they touch (see ModChain).
    // The body itself can be moved and turned in build mode, as long as every part still connects.
    // Each part's GameObject is scaled by the cell size, so inside a part, 1 unit is 1 cell.
    [RequireComponent(typeof(Rigidbody))]
    public class RobotBody : MonoBehaviour
    {
        [Tooltip("Width, height and depth of the body, in cells.")]
        public Vector3Int size = new(1, 4, 1);
        [Tooltip("How big one grid cell (one cube) is, in meters.")]
        public float cellSize = 0.5f;
        [Tooltip("How many cells past the body parts may reach. A safety limit, not the build zone.")]
        public int maxReach = 12;
        public Material bodyMaterial;
        [Tooltip("Nozzle marks and the head's visor.")]
        public Material detailMaterial;
        public PhysicsMaterial physicsMaterial;
        public AimRig aim;
        [Tooltip("What shots can hit. Leave out the robot's own layer.")]
        public LayerMask shotMask = ~0;

        const float CubeScale = 0.96f;

        // Where a part sits, without the part itself: what build mode moves around, saves and restores.
        public struct Mount
        {
            public PartDefinition definition;
            public Vector3Int anchor;
            public Quaternion orientation;
            public ButtonSlot button;

            public static Mount Of(PartModule part) =>
                new() { definition = part.Definition, anchor = part.Anchor, orientation = part.Orientation, button = part.Button };
        }

        readonly List<Vector3Int> bodyShape = new();
        readonly HashSet<Vector3Int> bodyCells = new();
        readonly Dictionary<Vector3Int, PartModule> partCells = new();
        readonly List<PartModule> parts = new();
        Vector3 gridOffset;
        Transform bodyVisual;
        RobotMotor motor;

        public IReadOnlyList<PartModule> Parts => parts;
        public IReadOnlyCollection<Vector3Int> BodyCells => bodyCells;
        public Vector3Int BodyAnchor { get; private set; }
        public Quaternion BodyOrientation { get; private set; } = Quaternion.identity;
        public Transform BodyVisual => bodyVisual;
        // The robot's lowest row of cells: the one touching the ground when it stands.
        public int BottomLayer { get; private set; }
        // How far the bottom of that row is above the robot's pivot, in meters (below it is negative).
        public float BottomHeight => BottomLayer * cellSize;
        public RobotMotor Motor => motor ? motor : motor = GetComponent<RobotMotor>();
        public Head Head => parts.OfType<Head>().FirstOrDefault();
        // Fires whenever a part is fitted or removed, or the body moves.
        public event Action Changed;

        // Where the crosshair points. Forward-facing weapons fire at it.
        public Vector3 AimPoint => aim ? aim.GetAimPoint() : transform.position + transform.forward * 50f;

        void Awake()
        {
            // The pivot sits at the center of the body's bottom. The offset is in cells.
            gridOffset = new Vector3(-size.x * 0.5f, 0f, -size.z * 0.5f);
            for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y++)
                    for (int z = 0; z < size.z; z++)
                        bodyShape.Add(new Vector3Int(x, y, z));
            SetBodyCells(Vector3Int.zero, Quaternion.identity);
            BottomLayer = AllCells().Min(c => c.y);
        }

        // A cell's center in grid units (1 per cell), before scaling by the cell size.
        Vector3 CellToGrid(Vector3Int cell) => new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f) + gridOffset;
        public Vector3 CellToLocal(Vector3Int cell) => CellToGrid(cell) * cellSize;
        public Vector3Int LocalToCell(Vector3 local) => Vector3Int.FloorToInt(local / cellSize - gridOffset);
        public Vector3 CellToWorld(Vector3Int cell) => transform.TransformPoint(CellToLocal(cell));

        public bool IsBody(Vector3Int cell) => bodyCells.Contains(cell);
        public bool IsFilled(Vector3Int cell) => bodyCells.Contains(cell) || partCells.ContainsKey(cell);
        public PartModule PartAt(Vector3Int cell) => partCells.TryGetValue(cell, out var part) ? part : null;
        public IEnumerable<PartModule> PartsOn(ButtonSlot button) => parts.Where(p => p.Button == button);

        // The cell a raycast hit on the robot landed in, and the grid direction of the face it hit.
        public bool TryGetCell(RaycastHit hit, out Vector3Int cell, out Vector3Int normal)
        {
            cell = LocalToCell(transform.InverseTransformPoint(hit.point - hit.normal * 0.05f));
            normal = GridMath.Axis(transform.InverseTransformDirection(hit.normal));
            return IsFilled(cell);
        }

        public IEnumerable<Vector3Int> Footprint(PartDefinition def, Vector3Int anchor, Quaternion orientation) =>
            def.cubes.Select(c => anchor + GridMath.Rotate(orientation, c));

        public IEnumerable<Vector3Int> BodyFootprint(Vector3Int anchor, Quaternion orientation) =>
            bodyShape.Select(c => anchor + GridMath.Rotate(orientation, c));

        // Why a part can't go here, or null if it can. Every cube must land in an empty cell, and the
        // part has to attach to something that can hold it.
        public string WhyCantPlace(PartDefinition def, Vector3Int anchor, Quaternion orientation) =>
            WhyCantPlace(new[] { new Mount { definition = def, anchor = anchor, orientation = orientation } });

        public bool CanPlace(PartDefinition def, Vector3Int anchor, Quaternion orientation) => WhyCantPlace(def, anchor, orientation) == null;

        // The same for a group of parts going on together, such as a connector and everything
        // hanging off it: every part has to be held on, by the robot or by the rest of the group.
        public string WhyCantPlace(IReadOnlyList<Mount> group)
        {
            var groupCells = new HashSet<Vector3Int>();
            foreach (var m in group)
                foreach (var cell in Footprint(m.definition, m.anchor, m.orientation))
                {
                    if (IsFilled(cell) || !groupCells.Add(cell)) return "It overlaps something";
                    if (!InReach(cell)) return "That's past the edge of the build zone";
                }

            var pieces = Pieces().Concat(group.Select(m => new Piece(m.definition, Footprint(m.definition, m.anchor, m.orientation)))).ToList();
            var held = Held(pieces, bodyCells);
            for (int i = 0; i < group.Count; i++)
                if (!held[parts.Count + i]) return group[i].definition.AttachRule;
            return null;
        }

        // Fits a group: whatever touches the robot first, then the rest once they can. Returns the
        // parts that couldn't go on.
        public List<Mount> PlaceGroup(IEnumerable<Mount> group)
        {
            var left = group.ToList();
            int index;
            while ((index = left.FindIndex(m => CanPlace(m.definition, m.anchor, m.orientation))) >= 0)
            {
                var m = left[index];
                Place(m.definition, m.anchor, m.orientation, m.button);
                left.RemoveAt(index);
            }
            return left;
        }

        public bool InReach(Vector3Int cell) =>
            cell.x >= -maxReach && cell.x < size.x + maxReach &&
            cell.y >= -maxReach && cell.y < size.y + maxReach &&
            cell.z >= -maxReach && cell.z < size.z + maxReach;

        public PartModule Place(PartDefinition def, Vector3Int anchor, Quaternion orientation, ButtonSlot? button = null)
        {
            orientation = GridMath.Snap(orientation);
            if (!CanPlace(def, anchor, orientation)) return null;

            var go = new GameObject(def.displayName) { layer = gameObject.layer };
            go.transform.SetParent(transform, false);
            go.transform.SetLocalPositionAndRotation(CellToLocal(anchor), orientation);
            go.transform.localScale = Vector3.one * cellSize;
            BuildPartVisual(go.transform, def, true, physicsMaterial);

            var module = def.AddModule(go);
            go.AddComponent<PartWear>();
            var footprint = Footprint(def, anchor, orientation).ToList();
            foreach (var cell in footprint) partCells[cell] = module;
            parts.Add(module);
            module.Bind(def, this, anchor, orientation, button ?? def.defaultButton, footprint);
            AfterChange();
            return module;
        }

        public void Remove(PartModule part)
        {
            if (!parts.Remove(part)) return;
            foreach (var cell in part.Cells) partCells.Remove(cell);
            Destroy(part.gameObject);
            AfterChange();
        }

        public void RemoveAll()
        {
            foreach (var part in parts.ToList()) Remove(part);
        }

        // The body may move anywhere its cubes fit, as long as every part is still held on.
        public bool CanMoveBody(Vector3Int anchor, Quaternion orientation)
        {
            var cells = new HashSet<Vector3Int>(BodyFootprint(anchor, GridMath.Snap(orientation)));
            if (cells.Any(c => partCells.ContainsKey(c) || !InReach(c))) return false;
            return Held(Pieces().ToList(), cells).All(h => h);
        }

        public bool MoveBody(Vector3Int anchor, Quaternion orientation)
        {
            orientation = GridMath.Snap(orientation);
            if (!CanMoveBody(anchor, orientation)) return false;
            SetBodyCells(anchor, orientation);
            AfterChange();
            return true;
        }

        void SetBodyCells(Vector3Int anchor, Quaternion orientation)
        {
            BodyAnchor = anchor;
            BodyOrientation = orientation;
            bodyCells.Clear();
            foreach (var cell in BodyFootprint(anchor, orientation)) bodyCells.Add(cell);

            if (bodyVisual) Destroy(bodyVisual.gameObject);
            bodyVisual = new GameObject("Body") { layer = gameObject.layer }.transform;
            bodyVisual.SetParent(transform, false);
            bodyVisual.localScale = Vector3.one * cellSize;
            foreach (var cell in bodyCells)
                CreateCube(bodyVisual, CellToGrid(cell), bodyMaterial, true, physicsMaterial).name = $"Cube {cell}";
        }

        IEnumerable<Vector3Int> AllCells() => bodyCells.Concat(partCells.Keys);

        // Keeps the robot standing on the floor: anything new that pokes out below its feet while it
        // stands lifts the whole robot, instead of ending up inside the floor and dragging it down.
        void AfterChange()
        {
            BottomLayer = AllCells().Min(c => c.y);
            if (Motor && Motor.IsGrounded)
            {
                float sunk = Motor.FloorHeight - (transform.position.y + BottomHeight);
                if (sunk > 0f)
                {
                    var lift = Vector3.up * (sunk + 0.01f);
                    GetComponent<Rigidbody>().position += lift;
                    transform.position += lift;
                }
            }
            RebuildChains();
            Changed?.Invoke();
        }

        // A part, placed or about to be, as far as holding goes: what it is and where its cubes are.
        readonly struct Piece
        {
            public readonly PartDefinition definition;
            public readonly List<Vector3Int> cells;

            public Piece(PartDefinition definition, IEnumerable<Vector3Int> cells)
            {
                this.definition = definition;
                this.cells = cells.ToList();
            }
        }

        // The robot's parts as pieces, in the same order as `parts`.
        IEnumerable<Piece> Pieces(PartModule ignoring = null) =>
            parts.Where(p => p != ignoring).Select(p => new Piece(p.Definition, p.Cells));

        // Which pieces are held on: a piece is held if it touches the body, or a held piece that
        // can hold it (connectors hold anything, mods hold mods, the head holds vision mods).
        static bool[] Held(IReadOnlyList<Piece> pieces, ICollection<Vector3Int> body)
        {
            var owner = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < pieces.Count; i++)
                foreach (var cell in pieces[i].cells) owner[cell] = i;
            var held = new bool[pieces.Count];
            var queue = new Queue<int>();
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i].cells.Any(c => GridMath.Directions.Any(d => body.Contains(c + d))))
                {
                    held[i] = true;
                    queue.Enqueue(i);
                }
            while (queue.Count > 0)
            {
                var holder = pieces[queue.Dequeue()];
                foreach (var cell in holder.cells)
                    foreach (var d in GridMath.Directions)
                        if (owner.TryGetValue(cell + d, out int j) && !held[j] && holder.definition.CanHold(pieces[j].definition))
                        {
                            held[j] = true;
                            queue.Enqueue(j);
                        }
            }
            return held;
        }

        // Parts that would no longer be held on, if `ignoring` were gone.
        public List<PartModule> FindDisconnected(PartModule ignoring = null)
        {
            var kept = parts.Where(p => p != ignoring).ToList();
            var held = Held(Pieces(ignoring).ToList(), bodyCells);
            return kept.Where((p, i) => !held[i]).ToList();
        }

        public bool HoldsUpOthers(PartModule part) => FindDisconnected(part).Count > 0;

        // A run of touching mods of one type, and the parts of its family that touch it. The longer
        // the chain, the higher its level: each extra mod adds a bit less, up to the mod's ceiling.
        public class ModChain
        {
            public ModDefinition definition;
            public readonly List<PartModule> mods = new();
            public readonly List<PartModule> boosted = new();
            public float level;

            public ModType Type => definition.type;
            // What the chain gives each part it boosts: a share, degrees or meters, depending on the type.
            public float Amount => definition.amountPerLevel * level;
        }

        readonly List<ModChain> chains = new();
        readonly Dictionary<(PartModule, ModType), ModChain> strongest = new();

        public IReadOnlyList<ModChain> Chains => chains;

        // The strongest chain of a type boosting a part, or null. Separate chains don't add up.
        public ModChain ChainBoosting(PartModule part, ModType type) => strongest.TryGetValue((part, type), out var chain) ? chain : null;
        public ModChain ChainOf(PartModule mod) => chains.FirstOrDefault(c => c.mods.Contains(mod));

        // The parts sharing a face with this one.
        public IEnumerable<PartModule> Neighbours(PartModule part) =>
            part.Cells.SelectMany(c => GridMath.Directions.Select(d => PartAt(c + d))).Where(p => p && p != part).Distinct();

        void RebuildChains()
        {
            chains.Clear();
            strongest.Clear();
            var seen = new HashSet<PartModule>();
            foreach (var start in parts)
            {
                if (start.Definition is not ModDefinition mod || !seen.Add(start)) continue;
                var chain = new ModChain { definition = mod };
                var queue = new Queue<PartModule>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var link = queue.Dequeue();
                    chain.mods.Add(link);
                    foreach (var next in Neighbours(link))
                        if (next.Definition is ModDefinition other && other.type == mod.type && seen.Add(next)) queue.Enqueue(next);
                }
                chain.level = Stacking.Total(1f, chain.mods.Count, mod.chainFalloff, mod.maxLevel);
                foreach (var part in chain.mods.SelectMany(Neighbours).Distinct())
                    if ((part.Definition.TakesMods & mod.Family) != 0) chain.boosted.Add(part);
                chains.Add(chain);
                foreach (var part in chain.boosted)
                    if (!strongest.TryGetValue((part, mod.type), out var best) || best.level < chain.level)
                        strongest[(part, mod.type)] = chain;
            }
        }

        // The space the whole robot (body and parts) takes up, in its own space.
        public Bounds LocalBounds
        {
            get
            {
                var cube = Vector3.one * cellSize;
                var bounds = new Bounds(CellToLocal(bodyCells.First()), cube);
                foreach (var c in AllCells()) bounds.Encapsulate(new Bounds(CellToLocal(c), cube));
                return bounds;
            }
        }

        // A part's cubes (or its model) under `parent`, with its nozzle mark or visor. `parent` should
        // be scaled by the cell size, since the cubes are 1 unit apart.
        public void BuildPartVisual(Transform parent, PartDefinition def, bool withColliders, PhysicsMaterial physics)
        {
            foreach (var c in def.cubes)
            {
                var cube = CreateCube(parent, c, def.material, withColliders, physics);
                if (def.modelPrefab) cube.GetComponent<Renderer>().enabled = false;
            }
            if (def.modelPrefab)
            {
                Instantiate(def.modelPrefab, parent, false);
                return;
            }

            var nozzle = (Vector3)def.nozzleCube;
            if (def.marking == PartMarking.Nozzle)
                CreateBlock(parent, nozzle + new Vector3(0f, 0f, 0.48f), new Vector3(0.42f, 0.42f, 0.08f), detailMaterial).name = "Working End";
            else if (def.marking == PartMarking.Visor)
                CreateBlock(parent, nozzle + new Vector3(0f, 0.1f, 0.48f), new Vector3(0.8f, 0.22f, 0.08f), detailMaterial).name = "Visor";
        }

        // One grid cube, drawn slightly small so the grid reads, with a collider that fills the whole cell.
        public static GameObject CreateCube(Transform parent, Vector3 localPosition, Material material, bool withCollider, PhysicsMaterial physics = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * CubeScale;
            if (material) go.GetComponent<Renderer>().sharedMaterial = material;
            var box = go.GetComponent<BoxCollider>();
            if (withCollider)
            {
                box.size = Vector3.one / CubeScale;
                box.sharedMaterial = physics;
            }
            else DestroyImmediate(box);
            return go;
        }

        public static GameObject CreateBlock(Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            if (material) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, size.y * 0.5f, 0f) * cellSize, (Vector3)size * cellSize);
        }
    }
}
