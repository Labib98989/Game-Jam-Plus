using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Mount = CoreDump.RobotBody.Mount;

namespace CoreDump
{
    // Building right in the room, the prototype's stand-in for the build screen. Tab pauses the
    // action and frees the mouse.
    // - The panel on the left is your inventory, sorted into folders. Drag a part from it onto the
    //   robot: it snaps to the surface under the mouse, pointing out of it.
    // - Click a part, or the body, to select it. Anything that would fall off without it (what hangs
    //   off a connector, say) comes along.
    //   WASD and R/F move the selection a cell at a time, Q/E spin it, T/G tip it and Z/C roll it,
    //   all relative to the camera. Drag it to move it further.
    // - The only rules: a part can't overlap anything, and it has to attach to the body or a
    //   connector. A part that fits counts as placed right away. One that doesn't stays where you
    //   put it as a red hologram you can keep moving, and nothing else can be selected until it fits
    //   or goes: Esc puts it back where it last fit, Ctrl+Z undoes, I sends it to the inventory and
    //   Delete to the bin (or drag it onto either). A part that fits but can't work there (a blocked
    //   barrel, a Leg off the ground) still goes on, with a yellow warning.
    // - The head and the body never leave the robot.
    // - Right-drag orbits the camera and the scroll wheel zooms. Tab closes; a red hologram goes
    //   back where it last fit first.
    // Pointing at or selecting a mod lights up its chain and the parts it boosts, and labels them.
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(RobotBody), typeof(PartInventory))]
    public class BuildMode : MonoBehaviour
    {
        public AimRig aim;
        [Tooltip("Takes over from the player camera while building. Positioned by this script.")]
        public CinemachineCamera buildCamera;
        public Material previewMaterial;
        public LayerMask robotMask;
        [Tooltip("Degrees the camera turns per pixel of right-drag.")]
        public float orbitSensitivity = 0.3f;
        [Tooltip("Whether Tab opens building here. A run only allows it in the workshop.")]
        public bool allowed = true;

        public bool IsOpen { get; private set; }

        // What's selected: some parts (one, or a connector with everything hanging off it), or the body.
        class Selection
        {
            public bool isBody;
            // Where the parts are, or where their hologram is while they don't fit.
            public List<Mount> mounts = new();
            // Turns go around this cell: the first cube of the part you clicked.
            public Vector3Int pivot;
            // Whether the parts are on the robot. False while they're a hologram.
            public bool placed = true;
            // Where they last fit, for Esc. Null for a new part that hasn't fit anywhere yet.
            public List<Mount> lastFit;
            // Where the body is going. It only moves once it fits there; until then a red hologram shows the spot.
            public Vector3Int bodyAnchor;
            public Quaternion bodyOrientation;
            // An undo step has already been saved for this selection.
            public bool saved;
        }

        class Snapshot
        {
            public Vector3Int bodyAnchor;
            public Quaternion bodyOrientation;
            public List<Mount> mounts;
            public List<PartInventory.Stock> stock;
        }

        // A line in the inventory panel: a folder heading, or a part in it.
        struct PanelRow
        {
            public Rect rect;
            public PartCategory folder;
            public PartDefinition part;
            public int count;
        }

        enum Drag { None, PendingPart, PendingSelection, NewPart, Selection }

        const float PanelWidth = 230f;
        const float RowHeight = 22f;
        const float DetailsWidth = 280f;
        // How far the mouse has to move with the button down before a click becomes a drag, squared, in pixels.
        const float DragThreshold = 36f;
        const string BodyWontMove = "The body has to fit, and every part has to stay connected to it";
        const string Locked = "Fix the red part first: move it until it fits, or press Esc to put it back";
        const string EdgeOfZone = "That's the edge of the build zone";

        static readonly PartCategory[] Folders =
        {
            PartCategory.Structure, PartCategory.Movement, PartCategory.Weapons,
            PartCategory.MovementMods, PartCategory.OffenseMods, PartCategory.VisionMods,
        };
        static readonly ButtonSlot[] Buttons = { ButtonSlot.LeftMouse, ButtonSlot.RightMouse, ButtonSlot.Space, ButtonSlot.Shift };
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color Fits = new(0.3f, 1f, 0.45f, 0.45f);
        static readonly Color WontWork = new(1f, 0.85f, 0.2f, 0.45f);
        static readonly Color DoesNotFit = new(1f, 0.2f, 0.15f, 0.55f);
        static readonly Color Mark = new(0.05f, 0.05f, 0.05f, 0.8f);
        static readonly Color GroundColor = new(0.3f, 1f, 0.6f, 0.14f);
        static readonly Color OkText = new(0.5f, 1f, 0.6f);
        static readonly Color WarningText = new(1f, 0.85f, 0.25f);
        static readonly Color ErrorText = new(1f, 0.45f, 0.4f);

        RobotBody robot;
        PartInventory inventory;
        Camera cam;
        Selection selection;
        readonly List<Snapshot> undo = new();

        Drag drag;
        Vector2 pressedAt;
        // A new part coming out of the inventory, and its turn on top of pointing out of the surface.
        PartDefinition dragPart;
        Quaternion dragTurn = Quaternion.identity;
        // Where a dragged selection was, for when it's dropped on nothing.
        List<Mount> dragFrom;
        Vector3Int dragFromPivot;
        bool dragFromPlaced;

        // What's under the mouse.
        bool overPanel, overBin, overDetails;
        int hoveredRow = -1;
        bool hasSurface;
        Vector3Int surfaceCell, surfaceNormal;
        PartModule hoveredPart;
        bool hoveredBody, hoveredSelection;

        float yaw, pitch = 22f, zoom = 1f;
        Vector2 rightPressedAt;
        bool orbiting, rightClicked;

        readonly HashSet<PartCategory> closedFolders = new();
        readonly List<PanelRow> rows = new();
        readonly List<(Rect rect, Action action)> detailButtons = new();
        Rect panelRect, binRect, detailsRect;

        Transform previewRoot;
        Renderer ground;
        readonly List<Renderer> preview = new();
        int previewCount;
        // What the hologram on show would do if dropped: why it wouldn't fit, or why it wouldn't work.
        string previewBlocked, previewProblem;
        MaterialPropertyBlock colorBlock, markBlock, highlightBlock, groundBlock;
        readonly Dictionary<Renderer, float> tints = new(), tinted = new();
        // Mod chains being pointed at or selected: lit up, with labels on the parts they boost.
        readonly List<RobotBody.ModChain> focusChains = new();
        GUIStyle tagStyle, crossStyle, titleStyle, folderStyle, wrapStyle, smallStyle, countStyle, boostStyle;
        string message;
        float messageUntil;

        bool BodyFits => selection.bodyAnchor == robot.BodyAnchor && selection.bodyOrientation == robot.BodyOrientation;
        bool SelectionFits => selection == null || (selection.isBody ? BodyFits : selection.placed);
        bool Carrying => drag == Drag.NewPart || (drag == Drag.Selection && !selection.isBody);

        void Awake()
        {
            robot = GetComponent<RobotBody>();
            inventory = GetComponent<PartInventory>();
            colorBlock = new MaterialPropertyBlock();
            markBlock = new MaterialPropertyBlock();
            markBlock.SetColor(BaseColor, Mark);
            highlightBlock = new MaterialPropertyBlock();
            groundBlock = new MaterialPropertyBlock();
            groundBlock.SetColor(BaseColor, GroundColor);
            previewRoot = new GameObject("Build Preview").transform;

            ground = RobotBody.CreateBlock(previewRoot, Vector3.zero, Vector3.one, previewMaterial).GetComponent<Renderer>();
            ground.name = "Build Ground";
            ground.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ground.SetPropertyBlock(groundBlock);
            ground.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if (IsOpen) Time.timeScale = 1f;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            if (kb.tabKey.wasPressedThisFrame)
            {
                if (IsOpen) Close();
                else if (allowed) Open();
            }
            if (!IsOpen) return;
            if (!cam) cam = Camera.main;

            OrbitAndZoom(mouse);
            Point(mouse.position.ReadValue());
            HandleMouse(mouse);
            HandleKeys(kb);
            // A right-click without dragging lets go of the selection.
            if (rightClicked && drag == Drag.None) Deselect();
            rightClicked = false;
            Show();
        }

        void LateUpdate()
        {
            if (!IsOpen || !buildCamera) return;
            var bounds = robot.LocalBounds;
            var focus = robot.transform.TransformPoint(bounds.center);
            float distance = (bounds.extents.magnitude * 2.4f + 2f) * zoom;
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            buildCamera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }

        void Open()
        {
            IsOpen = true;
            Time.timeScale = 0f;
            Juice.Play(Sound.Click);
            if (aim) aim.Suspended = true;
            yaw = (aim ? aim.Yaw : transform.eulerAngles.y) + 150f;
            pitch = 22f;
            zoom = 1f;
            selection = null;
            drag = Drag.None;
            if (buildCamera) buildCamera.Priority = 20;
            LateUpdate();
        }

        void Close()
        {
            // Whatever is in hand, or doesn't fit, goes back first.
            if (drag == Drag.NewPart)
            {
                inventory.Add(dragPart);
                DropLastSave();
            }
            else if (drag == Drag.Selection) DropOnNothing();
            drag = Drag.None;
            Deselect();
            IsOpen = false;
            Time.timeScale = 1f;
            Juice.Play(Sound.Click);
            previewCount = 0;
            HideUnusedPreview();
            ApplyTints();
            ground.gameObject.SetActive(false);
            if (buildCamera) buildCamera.Priority = 0;
            if (aim)
            {
                aim.Suspended = false;
                aim.SetLocked(true);
            }
        }

        // Right-drag orbits; a right-click without dragging is noted. The wheel zooms.
        void OrbitAndZoom(Mouse mouse)
        {
            if (mouse.rightButton.wasPressedThisFrame)
            {
                rightPressedAt = mouse.position.ReadValue();
                orbiting = false;
            }
            if (mouse.rightButton.isPressed)
            {
                if (!orbiting && (mouse.position.ReadValue() - rightPressedAt).sqrMagnitude > DragThreshold) orbiting = true;
                if (orbiting) Orbit(mouse.delta.ReadValue());
            }
            if (mouse.rightButton.wasReleasedThisFrame && !orbiting) rightClicked = true;
            if (mouse.middleButton.isPressed) Orbit(mouse.delta.ReadValue());
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f) zoom = Mathf.Clamp(zoom * (scroll > 0f ? 0.88f : 1.14f), 0.4f, 2.5f);
        }

        void Orbit(Vector2 delta)
        {
            yaw += delta.x * orbitSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * orbitSensitivity, -20f, 80f);
        }

        // Works out what's under the mouse: a panel, the selection's hologram, or a cell on the robot.
        void Point(Vector2 mousePosition)
        {
            var gui = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
            overPanel = panelRect.Contains(gui);
            overBin = binRect.Contains(gui);
            overDetails = selection != null && detailsRect.Contains(gui);
            hoveredRow = overPanel ? rows.FindIndex(r => r.rect.Contains(gui)) : -1;
            hasSurface = hoveredBody = hoveredSelection = false;
            hoveredPart = null;
            if (overPanel || overBin || overDetails || !cam) return;

            var ray = cam.ScreenPointToRay(mousePosition);
            // While dragging, look past what's being dragged to the robot behind it.
            float hologram = (drag is Drag.NewPart or Drag.Selection) ? float.PositiveInfinity : HologramDistance(ray);
            bool skipBody = drag == Drag.Selection && selection.isBody;
            foreach (var hit in Physics.RaycastAll(ray, 300f, robotMask, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (hit.distance > hologram) break;
                if (!hit.collider.transform.IsChildOf(robot.transform) || !robot.TryGetCell(hit, out var cell, out var normal)) continue;
                if (skipBody && robot.IsBody(cell)) continue;
                hasSurface = true;
                surfaceCell = cell;
                surfaceNormal = normal;
                hoveredPart = robot.PartAt(cell);
                hoveredBody = robot.IsBody(cell);
                break;
            }
            if (selection == null) return;
            hoveredSelection = (!hasSurface && hologram < float.PositiveInfinity) ||
                               (selection.isBody ? hoveredBody : hoveredPart && InSelection(hoveredPart));
        }

        // How far along the ray the selection's red hologram is, if it's showing.
        float HologramDistance(Ray ray)
        {
            var cells = HologramCells();
            if (cells == null) return float.PositiveInfinity;
            var local = new Ray(robot.transform.InverseTransformPoint(ray.origin), robot.transform.InverseTransformDirection(ray.direction));
            float best = float.PositiveInfinity;
            foreach (var cell in cells)
                if (new Bounds(robot.CellToLocal(cell), Vector3.one * robot.cellSize).IntersectRay(local, out float distance))
                    best = Mathf.Min(best, distance);
            return best;
        }

        IEnumerable<Vector3Int> HologramCells()
        {
            if (selection == null) return null;
            if (selection.isBody) return BodyFits ? null : robot.BodyFootprint(selection.bodyAnchor, selection.bodyOrientation);
            return selection.placed ? null : Cells(selection.mounts);
        }

        void HandleMouse(Mouse mouse)
        {
            var position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) Press(position);
            if (mouse.leftButton.isPressed && (drag is Drag.PendingPart or Drag.PendingSelection) && (position - pressedAt).sqrMagnitude > DragThreshold)
                StartDrag();
            if (drag is Drag.NewPart or Drag.Selection) Follow();
            if (mouse.leftButton.wasReleasedThisFrame) Release();
        }

        void Press(Vector2 position)
        {
            pressedAt = position;
            if (overDetails)
            {
                var gui = new Vector2(position.x, Screen.height - position.y);
                foreach (var (rect, action) in detailButtons)
                    if (rect.Contains(gui))
                    {
                        action();
                        break;
                    }
                return;
            }
            if (overPanel || overBin)
            {
                if (hoveredRow < 0) return;
                var row = rows[hoveredRow];
                if (!row.part)
                {
                    if (!closedFolders.Remove(row.folder)) closedFolders.Add(row.folder);
                    return;
                }
                if (!SelectionFits)
                {
                    Say(Locked);
                    return;
                }
                if (row.count == 0)
                {
                    Say($"No {row.part.displayName} left");
                    return;
                }
                dragPart = row.part;
                drag = Drag.PendingPart;
                return;
            }
            if (hoveredSelection)
            {
                drag = Drag.PendingSelection;
                return;
            }
            if (!SelectionFits)
            {
                Say(Locked);
                return;
            }
            if (hoveredPart) Select(hoveredPart);
            else if (hoveredBody) SelectBody();
            else
            {
                Deselect();
                return;
            }
            drag = Drag.PendingSelection;
        }

        void StartDrag()
        {
            if (drag == Drag.PendingPart)
            {
                Deselect();
                Save();
                inventory.Take(dragPart);
                dragTurn = Quaternion.identity;
                drag = Drag.NewPart;
                return;
            }
            // Let go of (or undone) while the button was still down.
            if (selection == null)
            {
                drag = Drag.None;
                return;
            }
            drag = Drag.Selection;
            if (selection.isBody) return;
            MarkChanged();
            dragFrom = selection.mounts.ToList();
            dragFromPivot = selection.pivot;
            dragFromPlaced = selection.placed;
            Lift();
        }

        // Moves what you're dragging to the surface under the mouse. Off the robot, it stays put.
        void Follow()
        {
            if (!hasSurface || drag == Drag.NewPart) return;
            var spot = surfaceCell + surfaceNormal;
            if (selection.isBody)
            {
                selection.bodyAnchor = spot;
                return;
            }
            var step = spot - selection.pivot;
            selection.mounts = selection.mounts.Select(m => Moved(m, step)).ToList();
            selection.pivot = spot;
        }

        // A new part dragged out of the inventory, at the surface under the mouse, pointing out of it.
        Mount DragMount => new()
        {
            definition = dragPart,
            anchor = surfaceCell + surfaceNormal,
            orientation = GridMath.Snap(SurfaceFrame(surfaceNormal) * dragTurn),
            button = dragPart.defaultButton,
        };

        static Quaternion SurfaceFrame(Vector3Int normal) =>
            Quaternion.LookRotation(normal, normal.y != 0 ? Vector3.forward : Vector3.up);

        void Release()
        {
            var was = drag;
            drag = Drag.None;
            switch (was)
            {
                case Drag.PendingPart:
                    Say("Drag it onto the robot");
                    break;

                case Drag.NewPart:
                    if (overBin) Say($"Threw away a {dragPart.displayName}");
                    else if (!hasSurface || !Cells(new[] { DragMount }).All(robot.InReach))
                    {
                        inventory.Add(dragPart);
                        DropLastSave();
                    }
                    else
                    {
                        var mount = DragMount;
                        selection = new Selection { mounts = { mount }, pivot = mount.anchor, saved = true };
                        Fit();
                    }
                    break;

                case Drag.Selection when selection.isBody:
                    if (overPanel || overBin) Say("The body never leaves the robot");
                    if (!hasSurface || !robot.BodyFootprint(selection.bodyAnchor, selection.bodyOrientation).All(robot.InReach))
                    {
                        selection.bodyAnchor = robot.BodyAnchor;
                        selection.bodyOrientation = robot.BodyOrientation;
                        break;
                    }
                    MarkChanged();
                    robot.MoveBody(selection.bodyAnchor, selection.bodyOrientation);
                    break;

                case Drag.Selection:
                    if (overBin) Bin();
                    else if (overPanel) Store();
                    else if (hasSurface && Cells(selection.mounts).All(robot.InReach)) Fit();
                    else DropOnNothing();
                    // The head can't go to the inventory or the bin, so it's still in hand.
                    if (selection != null && !selection.placed && (overBin || overPanel)) DropOnNothing();
                    break;
            }
        }

        // A dragged selection let go of away from the robot goes back where the drag started.
        void DropOnNothing()
        {
            if (selection.isBody)
            {
                selection.bodyAnchor = robot.BodyAnchor;
                selection.bodyOrientation = robot.BodyOrientation;
                return;
            }
            selection.mounts = dragFrom;
            selection.pivot = dragFromPivot;
            if (dragFromPlaced) Fit();
        }

        void HandleKeys(Keyboard kb)
        {
            if (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed)
            {
                if (kb.zKey.wasPressedThisFrame) Undo();
                return;
            }
            if (kb.escapeKey.wasPressedThisFrame) Deselect();
            if (drag == Drag.NewPart && kb.escapeKey.wasPressedThisFrame)
            {
                // Esc while dragging a new part puts it back.
                inventory.Add(dragPart);
                DropLastSave();
                drag = Drag.None;
                return;
            }
            if (drag == Drag.NewPart)
            {
                // A new part points out of the surface; these turn it from there.
                if (kb.qKey.wasPressedThisFrame) dragTurn = Quaternion.Euler(0f, 0f, 90f) * dragTurn;
                if (kb.eKey.wasPressedThisFrame) dragTurn = Quaternion.Euler(0f, 0f, -90f) * dragTurn;
                if (kb.tKey.wasPressedThisFrame) dragTurn = Quaternion.Euler(90f, 0f, 0f) * dragTurn;
                if (kb.gKey.wasPressedThisFrame) dragTurn = Quaternion.Euler(-90f, 0f, 0f) * dragTurn;
                if (kb.zKey.wasPressedThisFrame) dragTurn = Quaternion.Euler(0f, 90f, 0f) * dragTurn;
                if (kb.cKey.wasPressedThisFrame) dragTurn = Quaternion.Euler(0f, -90f, 0f) * dragTurn;
                return;
            }
            if (selection == null || !cam) return;
            if (drag == Drag.None)
            {
                if (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) Bin();
                if (kb.iKey.wasPressedThisFrame) Store();
                if (kb.bKey.wasPressedThisFrame) CycleButton();
                if (selection == null) return;
            }

            // Moves and turns follow the camera, snapped to the robot's grid.
            var forward = FlatAxis(cam.transform.forward);
            var right = FlatAxis(cam.transform.right);
            if (drag == Drag.None)
            {
                if (kb.wKey.wasPressedThisFrame) Nudge(forward);
                if (kb.sKey.wasPressedThisFrame) Nudge(-forward);
                if (kb.dKey.wasPressedThisFrame) Nudge(right);
                if (kb.aKey.wasPressedThisFrame) Nudge(-right);
                if (kb.rKey.wasPressedThisFrame) Nudge(Vector3Int.up);
                if (kb.fKey.wasPressedThisFrame) Nudge(Vector3Int.down);
            }
            if (kb.qKey.wasPressedThisFrame) Turn(Quaternion.AngleAxis(-90f, Vector3.up));
            if (kb.eKey.wasPressedThisFrame) Turn(Quaternion.AngleAxis(90f, Vector3.up));
            if (kb.tKey.wasPressedThisFrame) Turn(Quaternion.AngleAxis(90f, right));
            if (kb.gKey.wasPressedThisFrame) Turn(Quaternion.AngleAxis(-90f, right));
            if (kb.zKey.wasPressedThisFrame) Turn(Quaternion.AngleAxis(90f, forward));
            if (kb.cKey.wasPressedThisFrame) Turn(Quaternion.AngleAxis(-90f, forward));
        }

        // The grid axis, in the robot's space, closest to a camera direction flattened onto the floor.
        Vector3Int FlatAxis(Vector3 direction)
        {
            var local = robot.transform.InverseTransformDirection(direction);
            local.y = 0f;
            return GridMath.Axis(local.sqrMagnitude > 0.0001f ? local : Vector3.forward);
        }

        void Nudge(Vector3Int step)
        {
            if (selection.isBody) MoveBodyTo(selection.bodyAnchor + step, selection.bodyOrientation);
            else Change(selection.mounts.Select(m => Moved(m, step)).ToList(), selection.pivot + step);
        }

        // Turns the selection around its pivot. While it's being dragged it only turns in hand.
        void Turn(Quaternion turn)
        {
            if (selection.isBody)
            {
                var orientation = GridMath.Snap(turn * selection.bodyOrientation);
                if (drag == Drag.Selection) selection.bodyOrientation = orientation;
                else MoveBodyTo(selection.bodyAnchor, orientation);
                return;
            }
            var pivot = selection.pivot;
            var turned = selection.mounts.Select(m =>
            {
                m.anchor = pivot + GridMath.Rotate(turn, m.anchor - pivot);
                m.orientation = GridMath.Snap(turn * m.orientation);
                return m;
            }).ToList();
            if (drag == Drag.Selection) selection.mounts = turned;
            else Change(turned, pivot);
        }

        static Mount Moved(Mount m, Vector3Int step)
        {
            m.anchor += step;
            return m;
        }

        // Moves the selected parts to a new spot. They go on the robot if they fit there, and
        // become (or stay) a red hologram if they don't.
        void Change(List<Mount> moved, Vector3Int pivot)
        {
            if (!Cells(moved).All(robot.InReach))
            {
                Say(EdgeOfZone);
                return;
            }
            MarkChanged();
            Lift();
            selection.mounts = moved;
            selection.pivot = pivot;
            Fit();
        }

        // The body moves only where it fits; anywhere else, its red hologram shows where it's headed.
        void MoveBodyTo(Vector3Int anchor, Quaternion orientation)
        {
            if (!robot.BodyFootprint(anchor, orientation).All(robot.InReach))
            {
                Say(EdgeOfZone);
                return;
            }
            MarkChanged();
            selection.bodyAnchor = anchor;
            selection.bodyOrientation = orientation;
            robot.MoveBody(anchor, orientation);
        }

        // Takes the selected parts off the robot, so they can move freely.
        void Lift()
        {
            if (!selection.placed) return;
            foreach (var m in selection.mounts) robot.Remove(robot.PartAt(m.anchor));
            selection.placed = false;
        }

        // Puts the selected parts on the robot if they fit where they are.
        void Fit()
        {
            selection.placed = robot.WhyCantPlace(selection.mounts) == null;
            Juice.Play(selection.placed ? Sound.Place : Sound.Error);
            if (!selection.placed) return;
            robot.PlaceGroup(selection.mounts);
            selection.lastFit = selection.mounts.ToList();
        }

        IEnumerable<Vector3Int> Cells(IEnumerable<Mount> mounts) =>
            mounts.SelectMany(m => robot.Footprint(m.definition, m.anchor, m.orientation));

        // A part, plus everything that would fall off without it: what hangs off a connector, a mod
        // or the head.
        List<PartModule> Group(PartModule part)
        {
            var group = new List<PartModule> { part };
            group.AddRange(robot.FindDisconnected(part));
            return group;
        }

        void Select(PartModule part)
        {
            var mounts = Group(part).Select(Mount.Of).ToList();
            selection = new Selection { mounts = mounts, pivot = part.Anchor, lastFit = mounts.ToList() };
        }

        void SelectBody() =>
            selection = new Selection { isBody = true, bodyAnchor = robot.BodyAnchor, bodyOrientation = robot.BodyOrientation };

        bool InSelection(PartModule part) =>
            !selection.isBody && selection.placed && selection.mounts.Any(m => m.anchor == part.Anchor && m.definition == part.Definition);

        IEnumerable<PartModule> SelectedParts() =>
            selection == null || selection.isBody || !selection.placed
                ? Enumerable.Empty<PartModule>()
                : selection.mounts.Select(m => robot.PartAt(m.anchor)).Where(p => p);

        // Lets go of the selection. If it doesn't fit, it goes back where it last fit first, or back
        // to the inventory if it never fit anywhere. The body just stays where it is.
        void Deselect()
        {
            if (selection == null || drag is Drag.NewPart or Drag.Selection) return;
            if (!selection.isBody && !selection.placed)
            {
                if (selection.lastFit != null)
                {
                    selection.mounts = selection.lastFit;
                    Fit();
                }
                else
                {
                    foreach (var m in selection.mounts) inventory.Add(m.definition);
                    DropLastSave();
                }
            }
            selection = null;
        }

        bool Store() => TakeOff(true);
        bool Bin() => TakeOff(false);

        // Takes the selection off the robot, into the inventory or the bin.
        bool TakeOff(bool keep)
        {
            if (selection == null) return false;
            if (selection.isBody)
            {
                Say("The body never leaves the robot");
                return false;
            }
            if (selection.mounts.Any(m => m.definition is HeadDefinition))
            {
                Say(selection.mounts.Count > 1 ? "The head is on this, and it never leaves the robot" : "The head never leaves the robot");
                return false;
            }
            string name = SelectionName();
            MarkChanged();
            Lift();
            if (keep) foreach (var m in selection.mounts) inventory.Add(m.definition);
            Juice.Play(Sound.Remove);
            Say(keep ? $"{name}: back in your inventory" : $"{name}: thrown away");
            selection = null;
            return true;
        }

        string SelectionName()
        {
            if (selection.isBody) return "The body";
            var first = selection.mounts[0].definition.displayName;
            int more = selection.mounts.Count - 1;
            return more == 0 ? first : $"{first} + {more} part{(more > 1 ? "s" : "")}";
        }

        bool HasButton => selection != null && !selection.isBody && selection.mounts.Count == 1 &&
                          selection.mounts[0].definition.defaultButton != ButtonSlot.None;

        void CycleButton()
        {
            if (HasButton) SetButton(selection.mounts[0].button.Next() == ButtonSlot.None ? ButtonSlot.LeftMouse : selection.mounts[0].button.Next());
        }

        void SetButton(ButtonSlot slot)
        {
            if (!HasButton) return;
            MarkChanged();
            var m = selection.mounts[0];
            m.button = slot;
            selection.mounts[0] = m;
            if (selection.lastFit != null) selection.lastFit[0] = m;
            if (selection.placed) robot.PartAt(m.anchor).Button = slot;
        }

        // Saves an undo step the first time the selection changes anything, so a run of nudges
        // undoes in one go.
        void MarkChanged()
        {
            if (selection.saved) return;
            Save();
            selection.saved = true;
        }

        void Save()
        {
            undo.Add(new Snapshot
            {
                bodyAnchor = robot.BodyAnchor,
                bodyOrientation = robot.BodyOrientation,
                mounts = robot.Parts.Select(Mount.Of).ToList(),
                stock = inventory.Copy(),
            });
            if (undo.Count > 50) undo.RemoveAt(0);
        }

        void DropLastSave()
        {
            if (undo.Count > 0) undo.RemoveAt(undo.Count - 1);
        }

        void Undo()
        {
            if (drag is Drag.NewPart or Drag.Selection) return;
            if (undo.Count == 0)
            {
                Say("Nothing to undo");
                return;
            }
            selection = null;
            drag = Drag.None;
            var snapshot = undo[^1];
            undo.RemoveAt(undo.Count - 1);
            robot.RemoveAll();
            robot.MoveBody(snapshot.bodyAnchor, snapshot.bodyOrientation);
            robot.PlaceGroup(snapshot.mounts);
            inventory.Restore(snapshot.stock);
        }

        // This frame's holograms, highlights and ground plane.
        void Show()
        {
            previewCount = 0;
            previewBlocked = previewProblem = null;
            if (drag == Drag.NewPart && hasSurface) ShowGroup(new[] { DragMount });
            else if (selection != null && !selection.isBody && !selection.placed) ShowGroup(selection.mounts);
            else if (selection != null && selection.isBody && (drag == Drag.Selection || !BodyFits)) ShowBodyTarget();
            HideUnusedPreview();

            if (selection != null && selection.isBody) Tint(robot.BodyVisual.GetComponentsInChildren<Renderer>(), 1.7f);
            foreach (var part in SelectedParts()) Tint(part.GetComponentsInChildren<Renderer>(), 1.7f);
            if (drag == Drag.None)
            {
                if (hoveredPart && !(selection != null && InSelection(hoveredPart)))
                    foreach (var part in Group(hoveredPart)) Tint(part.GetComponentsInChildren<Renderer>(), 1.35f);
                else if (hoveredBody && !(selection != null && selection.isBody))
                    Tint(robot.BodyVisual.GetComponentsInChildren<Renderer>(), 1.35f);
            }

            focusChains.Clear();
            foreach (var part in SelectedParts())
                if (robot.ChainOf(part) is { } chain && !focusChains.Contains(chain)) focusChains.Add(chain);
            if (drag == Drag.None && hoveredPart && robot.ChainOf(hoveredPart) is { } pointed && !focusChains.Contains(pointed)) focusChains.Add(pointed);
            foreach (var chain in focusChains)
            {
                foreach (var mod in chain.mods) Tint(mod.GetComponentsInChildren<Renderer>(), 1.5f);
                foreach (var part in chain.boosted) Tint(part.GetComponentsInChildren<Renderer>(), 1.3f);
            }
            ApplyTints();
            ShowGround();
        }

        void ShowGroup(IReadOnlyList<Mount> mounts)
        {
            previewBlocked = robot.WhyCantPlace(mounts);
            if (previewBlocked == null) previewProblem = Problem(mounts);
            colorBlock.SetColor(BaseColor, previewBlocked != null ? DoesNotFit : previewProblem != null ? WontWork : Fits);
            foreach (var m in mounts)
            {
                var rotation = robot.transform.rotation * m.orientation;
                foreach (var cell in robot.Footprint(m.definition, m.anchor, m.orientation))
                    PreviewBlock(robot.CellToWorld(cell), rotation, Vector3.one * robot.cellSize, colorBlock);
                if (m.definition.marking == PartMarking.None) continue;
                var end = robot.CellToWorld(m.definition.WorkingEnd(m.anchor, m.orientation)) + rotation * new Vector3(0f, 0f, 0.5f * robot.cellSize);
                PreviewBlock(end, rotation, new Vector3(0.45f, 0.45f, 0.12f) * robot.cellSize, markBlock);
            }
        }

        // Why a group of parts would fit but not work where it is, or null.
        string Problem(IReadOnlyList<Mount> mounts)
        {
            var cells = Cells(mounts).ToList();
            int bottom = Mathf.Min(robot.BottomLayer, cells.Min(c => c.y));
            foreach (var m in mounts)
                if (m.definition.ProblemAt(robot, m.anchor, m.orientation, bottom, cells) is string problem)
                    return mounts.Count > 1 ? $"{m.definition.displayName}: {problem}" : problem;
            return null;
        }

        void ShowBodyTarget()
        {
            bool fits = robot.CanMoveBody(selection.bodyAnchor, selection.bodyOrientation);
            if (!fits) previewBlocked = BodyWontMove;
            colorBlock.SetColor(BaseColor, fits ? Fits : DoesNotFit);
            var rotation = robot.transform.rotation * selection.bodyOrientation;
            foreach (var cell in robot.BodyFootprint(selection.bodyAnchor, selection.bodyOrientation))
                PreviewBlock(robot.CellToWorld(cell), rotation, Vector3.one * robot.cellSize, colorBlock);
        }

        void PreviewBlock(Vector3 position, Quaternion rotation, Vector3 scale, MaterialPropertyBlock block)
        {
            if (previewCount == preview.Count)
            {
                var r = RobotBody.CreateBlock(previewRoot, Vector3.zero, Vector3.one, previewMaterial).GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                preview.Add(r);
            }
            var renderer = preview[previewCount++];
            renderer.gameObject.SetActive(true);
            renderer.transform.SetPositionAndRotation(position, rotation);
            renderer.transform.localScale = scale;
            renderer.SetPropertyBlock(block);
        }

        void HideUnusedPreview()
        {
            for (int i = previewCount; i < preview.Count; i++) preview[i].gameObject.SetActive(false);
        }

        // Brightens renderers this frame; the strongest request wins. ApplyTints sets them all at once.
        void Tint(IEnumerable<Renderer> renderers, float strength)
        {
            foreach (var r in renderers)
                if (r && (!tints.TryGetValue(r, out var current) || current < strength)) tints[r] = strength;
        }

        void ApplyTints()
        {
            foreach (var r in tinted.Keys)
                if (r && !tints.ContainsKey(r)) r.SetPropertyBlock(null);
            foreach (var (r, strength) in tints)
            {
                if (tinted.TryGetValue(r, out var old) && old == strength) continue;
                var material = r.sharedMaterial;
                if (!material || !material.HasColor(BaseColor)) continue;
                highlightBlock.SetColor(BaseColor, material.GetColor(BaseColor) * strength + Color.white * ((strength - 1f) * 0.25f));
                r.SetPropertyBlock(highlightBlock);
            }
            tinted.Clear();
            foreach (var (r, strength) in tints) tinted[r] = strength;
            tints.Clear();
        }

        // A see-through plane at the robot's feet: what Legs and Treads have to reach.
        void ShowGround()
        {
            var bounds = robot.LocalBounds;
            // Just above the bottom of the lowest cubes, so the floor doesn't hide it.
            var center = new Vector3(bounds.center.x, robot.BottomHeight + 0.02f, bounds.center.z);
            ground.gameObject.SetActive(true);
            ground.transform.SetPositionAndRotation(robot.transform.TransformPoint(center), robot.transform.rotation);
            ground.transform.localScale = new Vector3(bounds.size.x + 2f, 0.02f, bounds.size.z + 2f);
        }

        void Say(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 2.5f;
        }

        // How the selection is doing: fits and works, fits with a warning, or doesn't fit.
        (string text, Color color) Status()
        {
            if (selection.isBody)
                return previewBlocked != null ? ("Doesn't fit: " + previewBlocked, ErrorText) : ("Fits", OkText);
            if (!selection.placed)
                return previewBlocked != null ? ("Doesn't fit: " + previewBlocked, ErrorText)
                     : previewProblem != null ? ("Fits, but won't work: " + previewProblem, WarningText)
                     : ("Fits", OkText);
            var problem = SelectedParts().Select(p => p.Problem).FirstOrDefault(p => p != null);
            return problem != null ? ("Won't work: " + problem, WarningText) : ("Fits and works", OkText);
        }

        static Color ButtonColor(ButtonSlot slot) => slot switch
        {
            ButtonSlot.LeftMouse => new Color(0.95f, 0.35f, 0.3f),
            ButtonSlot.RightMouse => new Color(0.35f, 0.6f, 1f),
            ButtonSlot.Space => new Color(0.35f, 0.85f, 0.45f),
            ButtonSlot.Shift => new Color(1f, 0.8f, 0.25f),
            _ => Color.gray,
        };

        static string ButtonTag(ButtonSlot slot) => slot switch
        {
            ButtonSlot.LeftMouse => "LMB",
            ButtonSlot.RightMouse => "RMB",
            ButtonSlot.Space => "SPACE",
            ButtonSlot.Shift => "SHIFT",
            _ => "",
        };

        static string FolderName(PartCategory folder) => folder switch
        {
            PartCategory.Structure => "STRUCTURE",
            PartCategory.Movement => "MOVEMENT",
            PartCategory.Weapons => "WEAPONS",
            PartCategory.MovementMods => "MOVEMENT MODS",
            PartCategory.OffenseMods => "OFFENSE MODS",
            PartCategory.VisionMods => "VISION MODS",
            _ => folder.ToString().ToUpper(),
        };

        static Color FamilyColor(ModFamily family) => family switch
        {
            ModFamily.Movement => new Color(0.35f, 0.95f, 0.8f),
            ModFamily.Vision => new Color(0.8f, 0.6f, 1f),
            _ => new Color(1f, 0.55f, 0.4f),
        };

        // Which mod families a part takes, for the details panel.
        static string TakesText(ModFamily families)
        {
            var names = new List<string>();
            if ((families & ModFamily.Movement) != 0) names.Add("movement");
            if ((families & ModFamily.Offense) != 0) names.Add("offense");
            if ((families & ModFamily.Vision) != 0) names.Add("vision");
            return names.Count == 0 ? "" : $"  ·  takes {string.Join(" and ", names)} mods";
        }

        // What mods are doing for a part: a mod's chain and what it boosts, or the boosts a part gets.
        string ModSummary(PartModule part)
        {
            if (part.Definition is ModDefinition)
            {
                var chain = robot.ChainOf(part);
                if (chain == null) return null;
                string boosts = chain.boosted.Count == 0
                    ? "Not touching anything it can boost"
                    : "Boosting " + string.Join(", ", chain.boosted.Select(p => p.Definition.displayName));
                return $"Chain of {chain.mods.Count}: {ModDefinition.Describe(chain.Type, chain.Amount)}\n{boosts}";
            }
            // Separate chains of the same type don't add up: only the strongest counts.
            var boostsHere = robot.Chains.Where(c => c.boosted.Contains(part)).GroupBy(c => c.Type)
                .Select(g => g.OrderByDescending(c => c.level).First())
                .Select(c => $"{ModDefinition.Describe(c.Type, c.Amount)} (chain of {c.mods.Count})").ToList();
            return boostsHere.Count == 0 ? null : "Boosted: " + string.Join(", ", boostsHere);
        }

        void OnGUI()
        {
            if (!IsOpen) return;
            boostStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold };
            tagStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = Color.black } };
            crossStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 26, fontStyle = FontStyle.Bold, normal = { textColor = WarningText } };
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = true };
            folderStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.72f, 0.35f) } };
            wrapStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
            smallStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.75f, 0.75f, 0.78f) } };
            countStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };

            DrawPartMarks();
            DrawPanel();
            DrawDetails();
            DrawCursorLabel();
            GUI.Label(new Rect(PanelWidth + 30f, Screen.height - 28f, Screen.width - PanelWidth - 40f, 24f),
                "Drag parts from the inventory onto the robot    Click a part to select it    Right-drag orbit    Scroll zoom    Ctrl+Z undo    Tab done", smallStyle);
            if (Time.unscaledTime < messageUntil)
                GUI.Box(new Rect(Screen.width * 0.5f - 250f, Screen.height - 66f, 500f, 26f), message);
        }

        // Button tags on every part, and a yellow X on parts that can't work where they are.
        void DrawPartMarks()
        {
            if (!cam) return;
            foreach (var part in robot.Parts)
            {
                var center = Vector3.zero;
                foreach (var c in part.Cells) center += robot.CellToWorld(c);
                center /= part.Cells.Count;
                var screen = cam.WorldToScreenPoint(center);
                if (screen.z < 0f) continue;
                var at = new Vector2(screen.x, Screen.height - screen.y);

                if (part.Button != ButtonSlot.None)
                {
                    var rect = new Rect(at.x - 22f, at.y - 9f, 44f, 18f);
                    GUI.color = ButtonColor(part.Button);
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(rect, ButtonTag(part.Button), tagStyle);
                }
                if (!part.IsWorking)
                {
                    var end = cam.WorldToScreenPoint(robot.CellToWorld(part.Definition.HasWorkingEnd ? part.Definition.WorkingEnd(part.Anchor, part.Orientation) : part.Anchor));
                    if (end.z > 0f) GUI.Label(new Rect(end.x - 16f, Screen.height - end.y - 16f, 32f, 32f), "X", crossStyle);
                }
            }

            // What each lit-up chain gives the parts it boosts.
            foreach (var chain in focusChains)
                foreach (var part in chain.boosted)
                {
                    if (!part) continue;
                    var center = Vector3.zero;
                    foreach (var c in part.Cells) center += robot.CellToWorld(c);
                    var screen = cam.WorldToScreenPoint(center / part.Cells.Count);
                    if (screen.z < 0f) continue;
                    string text = ModDefinition.Describe(chain.Type, chain.Amount);
                    float width = boostStyle.CalcSize(new GUIContent(text)).x + 10f;
                    var rect = new Rect(screen.x - width * 0.5f, Screen.height - screen.y + 12f, width, 18f);
                    GUI.color = new Color(0f, 0f, 0f, 0.75f);
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    GUI.color = FamilyColor(chain.definition.Family);
                    GUI.Label(rect, text, boostStyle);
                    GUI.color = Color.white;
                }
        }

        // The inventory, folder by folder, and the bin below it.
        void DrawPanel()
        {
            rows.Clear();
            float y = 40f;
            foreach (var folder in Folders)
            {
                var stock = inventory.Items.Where(s => s.part && s.part.Category == folder && s.count != 0).ToList();
                if (stock.Count == 0) continue;
                rows.Add(new PanelRow { rect = new Rect(16f, y, PanelWidth - 12f, RowHeight - 2f), folder = folder });
                y += RowHeight;
                if (closedFolders.Contains(folder)) continue;
                foreach (var s in stock)
                {
                    rows.Add(new PanelRow { rect = new Rect(24f, y, PanelWidth - 20f, RowHeight - 2f), folder = folder, part = s.part, count = s.count });
                    y += RowHeight;
                }
            }
            panelRect = new Rect(10f, 10f, PanelWidth, y);
            binRect = new Rect(10f, panelRect.yMax + 8f, PanelWidth, 46f);

            bool dropHere = Carrying && overPanel;
            GUI.color = dropHere ? new Color(0.55f, 1f, 0.65f) : Color.white;
            GUI.Box(panelRect, GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(18f, 14f, PanelWidth - 16f, 22f), dropHere ? "Drop to put it in your inventory" : "INVENTORY", titleStyle);
            var mouse = Event.current.mousePosition;
            foreach (var row in rows)
            {
                if (!row.part)
                {
                    GUI.Label(row.rect, $"{(closedFolders.Contains(row.folder) ? "+" : "-")}  {FolderName(row.folder)}", folderStyle);
                    continue;
                }
                bool empty = row.count == 0;
                GUI.color = empty ? new Color(1f, 1f, 1f, 0.35f) : row.rect.Contains(mouse) && drag == Drag.None ? new Color(1f, 1f, 1f, 1f) : new Color(0.85f, 0.85f, 0.85f);
                GUI.Box(row.rect, GUIContent.none, GUI.skin.button);
                if (row.part.material)
                {
                    var swatch = row.part.material.GetColor(BaseColor);
                    GUI.color = empty ? new Color(swatch.r, swatch.g, swatch.b, 0.35f) : swatch;
                    GUI.DrawTexture(new Rect(row.rect.x + 6f, row.rect.y + 4f, 12f, 12f), Texture2D.whiteTexture);
                }
                GUI.color = empty ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
                GUI.Label(new Rect(row.rect.x + 24f, row.rect.y, row.rect.width - 60f, row.rect.height), row.part.displayName);
                GUI.Label(new Rect(row.rect.xMax - 44f, row.rect.y, 38f, row.rect.height), row.count < 0 ? "∞" : $"x{row.count}", countStyle);
                GUI.color = Color.white;
            }

            bool binIt = Carrying && overBin;
            GUI.color = binIt ? new Color(1f, 0.4f, 0.35f) : new Color(1f, 0.8f, 0.8f);
            GUI.Box(binRect, binIt ? "\nDrop to throw it away" : "BIN\nDrag parts here to throw them away");
            GUI.color = Color.white;
        }

        // What's selected, how it's doing, its button, and ways to take it off.
        void DrawDetails()
        {
            detailButtons.Clear();
            if (selection == null)
            {
                detailsRect = default;
                return;
            }
            float width = DetailsWidth - 20f;
            string title = SelectionName();
            string description = selection.isBody
                ? "Move it and turn it with the keys, or drag it. Every part has to stay connected to it."
                : selection.mounts[0].definition.description + (selection.mounts.Count > 1 ? "\nEverything hanging off it comes along." : "");
            var (statusText, statusColor) = Status();
            var firstPart = !selection.isBody && selection.placed ? robot.PartAt(selection.mounts[0].anchor) : null;
            string mods = firstPart ? ModSummary(firstPart) : null;
            bool removable = !selection.isBody && !selection.mounts.Any(m => m.definition is HeadDefinition);
            const string keys = "W A S D, R/F  move    Q/E  spin\nT/G  tip    Z/C  roll\nEsc  let go    Ctrl+Z  undo";

            float titleHeight = titleStyle.CalcHeight(new GUIContent(title), width);
            float descriptionHeight = wrapStyle.CalcHeight(new GUIContent(description), width);
            float statusHeight = wrapStyle.CalcHeight(new GUIContent(statusText), width);
            float modsHeight = mods != null ? wrapStyle.CalcHeight(new GUIContent(mods), width) + 6f : 0f;
            float keysHeight = smallStyle.CalcHeight(new GUIContent(keys), width);
            float height = 12f + titleHeight + 18f + descriptionHeight + 6f + statusHeight + 8f + modsHeight
                           + (HasButton ? 48f : 0f) + (removable ? 32f : 0f) + keysHeight + 10f;

            detailsRect = new Rect(Screen.width - DetailsWidth - 10f, 10f, DetailsWidth, height);
            GUI.Box(detailsRect, GUIContent.none);
            float x = detailsRect.x + 10f, y = detailsRect.y + 8f;
            GUI.Label(new Rect(x, y, width, titleHeight), title, titleStyle);
            y += titleHeight;
            var definition = selection.isBody ? null : selection.mounts[0].definition;
            string folder = definition ? FolderName(definition.Category) + TakesText(definition.TakesMods) : "Body";
            GUI.Label(new Rect(x, y, width, 18f), folder, smallStyle);
            y += 18f;
            GUI.Label(new Rect(x, y, width, descriptionHeight), description, wrapStyle);
            y += descriptionHeight + 6f;
            GUI.contentColor = statusColor;
            GUI.Label(new Rect(x, y, width, statusHeight), statusText, wrapStyle);
            GUI.contentColor = Color.white;
            y += statusHeight + 8f;
            if (mods != null)
            {
                GUI.contentColor = definition is ModDefinition mod ? FamilyColor(mod.Family) : new Color(0.75f, 0.9f, 1f);
                GUI.Label(new Rect(x, y, width, modsHeight - 6f), mods, wrapStyle);
                GUI.contentColor = Color.white;
                y += modsHeight;
            }

            if (HasButton)
            {
                GUI.Label(new Rect(x, y, width, 18f), "Fires on", smallStyle);
                y += 18f;
                var current = selection.mounts[0].button;
                float chip = (width - 3 * 6f) / 4f;
                for (int i = 0; i < Buttons.Length; i++)
                {
                    var slot = Buttons[i];
                    var rect = new Rect(x + i * (chip + 6f), y, chip, 22f);
                    var color = ButtonColor(slot);
                    GUI.color = slot == current ? color : new Color(color.r, color.g, color.b, 0.3f);
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(rect, ButtonTag(slot), tagStyle);
                    detailButtons.Add((rect, () => SetButton(slot)));
                }
                y += 30f;
            }

            if (removable)
            {
                float half = (width - 6f) / 2f;
                var store = new Rect(x, y, half, 24f);
                var bin = new Rect(x + half + 6f, y, half, 24f);
                GUI.Box(store, "To inventory (I)", GUI.skin.button);
                GUI.Box(bin, "Bin it (Del)", GUI.skin.button);
                detailButtons.Add((store, () => Store()));
                detailButtons.Add((bin, () => Bin()));
                y += 32f;
            }
            GUI.Label(new Rect(x, y, width, keysHeight), keys, smallStyle);
        }

        // Next to the mouse: what a drop would do, or what you're pointing at.
        void DrawCursorLabel()
        {
            string text = null;
            if (Carrying)
            {
                string name = drag == Drag.NewPart ? dragPart.displayName : SelectionName();
                string hint = overBin ? "Drop to throw it away"
                    : overPanel ? "Drop to put it in your inventory"
                    : !hasSurface ? "Drop it on the robot"
                    : previewBlocked != null ? $"Doesn't fit: {previewBlocked}\nDrop it here as a red hologram, then fix it with the keys"
                    : previewProblem != null ? $"Fits, but won't work: {previewProblem}"
                    : "Fits";
                text = $"{name}\n{hint}";
            }
            else if (drag == Drag.None && hoveredPart && !hoveredSelection)
            {
                var problem = hoveredPart.Problem;
                var mods = ModSummary(hoveredPart);
                text = $"{hoveredPart.Definition.displayName}" + (problem != null ? $"\nWon't work: {problem}" : "") +
                       (mods != null ? $"\n{mods}" : "") + "\nClick to select, drag to move";
            }
            else if (drag == Drag.None && hoveredBody && !hoveredSelection) text = "The body\nClick to select, drag to move";
            if (text == null) return;
            var mouse = Event.current.mousePosition;
            var size = GUI.skin.box.CalcSize(new GUIContent(text));
            GUI.Box(new Rect(mouse.x + 18f, mouse.y + 14f, size.x + 12f, size.y + 4f), text);
        }
    }
}
