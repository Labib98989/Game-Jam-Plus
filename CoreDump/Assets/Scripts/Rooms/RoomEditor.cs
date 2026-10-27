using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CoreDump
{
    // The room editor: a tool for the team that runs in Play mode and saves rooms into the project
    // (Assets/Resources/Rooms). It works like the build screen.
    // - The panel on the left holds the pieces in folders. Click a block or ramp, then drag across
    //   any surface to draw a rectangle of it; it builds out of the surface by its usual height.
    //   Click a prop or marker, then click to place one. Or drag any piece straight in. Right-click
    //   or Esc puts the brush down.
    // - Click a piece to select it, Shift-click to add more. W A S D and R/F move the selection a meter
    //   at a time, relative to the camera, and Q/E turn it. Shift with a move key grows a block that
    //   way, Alt with a move key shrinks it. Drag it to slide it along the floor.
    // - Pieces that overlap turn red. Delete bins the selection (or drag it onto the bin),
    //   Ctrl+C/Ctrl+V copy and paste, Ctrl+Z undoes.
    // - Hold the right mouse button to fly: move the mouse to look, WASD to move, Q/E down and up,
    //   Shift to go faster. The scroll wheel moves forward and back.
    // - Ctrl+S saves. Tab tests the room: the robot drops in at the entry. Tab again comes back.
    public class RoomEditor : MonoBehaviour
    {
        public RoomKit kit;
        [Tooltip("The editing camera. Positioned by this script.")]
        public CinemachineCamera editorCamera;
        public RobotBody robot;
        public AimRig aim;
        [Tooltip("Switched on only while testing a room: the robot, its camera and its landing shadow.")]
        public GameObject[] testObjects;
        public float flySpeed = 14f;
        [Tooltip("Degrees the camera turns per pixel while looking around.")]
        public float lookSensitivity = 0.2f;

        // Inside Assets, where the game can load them in any build.
        const string RoomsFolder = "Resources/Rooms";
        // How far a room can reach, in meters.
        static readonly Vector3Int AreaMin = new(-64, -16, -64);
        static readonly Vector3Int AreaMax = new(64, 48, 64);
        const int EnoughSpawns = 6;
        const float PanelWidth = 230f;
        const float RowHeight = 22f;
        const float SideWidth = 280f;
        // How far the mouse has to move with the button down before a click becomes a drag, squared, in pixels.
        const float DragThreshold = 36f;
        const string EdgeOfArea = "That's past the edge of the room area";

        static readonly PieceFolder[] Folders = { PieceFolder.Blocks, PieceFolder.Ramps, PieceFolder.Pads, PieceFolder.Props, PieceFolder.Markers };
        static readonly int[] Chances = { 100, 75, 50, 25 };
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color Fits = new(0.3f, 1f, 0.45f, 0.45f);
        static readonly Color DoesNotFit = new(1f, 0.2f, 0.15f, 0.55f);
        static readonly Color OverlapColor = new(1f, 0.35f, 0.3f);
        static readonly Color ChipColor = new(1f, 0.72f, 0.35f);
        static readonly Color OkText = new(0.5f, 1f, 0.6f);
        static readonly Color WarningText = new(1f, 0.85f, 0.25f);
        static readonly Color ErrorText = new(1f, 0.45f, 0.4f);

        class View
        {
            public GameObject go;
            public Renderer[] renderers;
        }

        // A line in the panel: a folder heading, or a piece in it.
        struct PanelRow
        {
            public Rect rect;
            public PieceFolder folder;
            public PieceKind kind;
        }

        enum Drag { None, PendingItem, NewPiece, Drawing, PendingSelection, Selection }

        RoomData room = new();
        // Unsaved changes.
        bool dirty;
        readonly List<RoomData> undo = new();
        readonly Dictionary<RoomPiece, View> views = new();
        readonly Dictionary<GameObject, RoomPiece> pieceOf = new();
        readonly HashSet<RoomPiece> overlapping = new();
        Transform viewRoot, previewRoot;
        Camera cam;
        StartingLoadout loadout;

        readonly List<RoomPiece> selection = new();
        // An undo step has already been saved for this selection, so a run of nudges undoes in one go.
        bool selectionSaved;
        List<RoomPiece> clipboard;
        // The piece picked up from the panel: drawn or placed with the left mouse until it's put down.
        PieceKind brush;
        int brushTurn;

        Drag drag;
        Vector2 pressedAt;
        // A piece dragged straight out of the panel, and its turn.
        PieceKind dragKind;
        int dragTurn;
        // The rectangle being drawn: where it started and ended, and which way the surface faces.
        Vector3Int drawStart, drawEnd, drawNormal;
        // Sliding the selection: where it was grabbed, how far it has moved, and where it started.
        Vector3 grabPoint;
        Vector3Int grabStep;
        List<Vector3Int> grabMins;

        // What's under the mouse.
        bool overPanel, overBin, overRoom, overDetails;
        bool OverUi => overPanel || overBin || overRoom || overDetails;
        int hoveredRow = -1;
        bool hasSurface;
        Vector3Int surfaceCell, surfaceNormal;
        RoomPiece hoveredPiece;
        Vector3 hoveredPoint;

        Vector3 cameraPosition = new(0f, 22f, -48f);
        float yaw, pitch = 28f;
        bool flying, looked, rightClicked;
        Vector2 rightPressedAt;

        bool testing, robotStarted;
        Transform testRoom;

        readonly HashSet<PieceFolder> closedFolders = new();
        readonly List<PanelRow> rows = new();
        readonly List<(Rect rect, System.Action action)> buttons = new();
        Rect panelRect, binRect, roomRect, detailsRect;
        List<string> savedRooms = new();
        // The name a save would replace, waiting for a second save to confirm.
        string replacing;
        float replacingUntil;
        bool dropFocus;

        readonly List<Renderer> preview = new();
        int previewCount;
        MaterialPropertyBlock block;
        readonly Dictionary<Renderer, Color> looks = new();
        GUIStyle labelStyle, chipStyle, titleStyle, folderStyle, wrapStyle, smallStyle, warnStyle, countStyle;
        string message;
        float messageUntil;

        Vector3Int Spot => surfaceCell + surfaceNormal;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            viewRoot = new GameObject("Room Pieces").transform;
            previewRoot = new GameObject("Room Editor Preview").transform;
            if (robot) loadout = robot.GetComponent<StartingLoadout>();

            // A faint 1 m grid on the ground, to draw the first floor on.
            var gridMaterial = new Material(kit.preview);
            gridMaterial.SetTexture("_BaseMap", GridTexture());
            gridMaterial.SetTextureScale("_BaseMap", new Vector2(AreaMax.x - AreaMin.x, AreaMax.z - AreaMin.z));
            gridMaterial.SetColor(BaseColor, new Color(1f, 1f, 1f, 0.3f));
            var grid = RobotBody.CreateBlock(previewRoot, new Vector3((AreaMin.x + AreaMax.x) * 0.5f, -0.005f, (AreaMin.z + AreaMax.z) * 0.5f),
                new Vector3(AreaMax.x - AreaMin.x, 0.02f, AreaMax.z - AreaMin.z), gridMaterial);
            grid.name = "Grid";
            grid.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            foreach (var go in testObjects)
                if (go) go.SetActive(false);
            if (aim) aim.Suspended = true;
            if (editorCamera) editorCamera.Priority = 30;
            RefreshRoomList();
            room.name = UnusedName();
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;
            if (testing)
            {
                if (kb.tabKey.wasPressedThisFrame) StopTest();
                return;
            }
            if (!cam) cam = Camera.main;

            // While the name is being typed, keys go to the name.
            bool typing = GUIUtility.keyboardControl != 0;
            Fly(mouse, kb, typing);
            Point(mouse.position.ReadValue());
            HandleMouse(mouse, kb);
            if (!typing && !flying) HandleKeys(kb);
            // A right-click without looking around puts the brush down, or lets go of the selection.
            if (rightClicked && drag == Drag.None)
            {
                if (brush != null) brush = null;
                else Deselect();
            }
            rightClicked = false;
            Show();
        }

        void LateUpdate()
        {
            if (testing || !editorCamera) return;
            editorCamera.transform.SetPositionAndRotation(cameraPosition, Quaternion.Euler(pitch, yaw, 0f));
        }

        // Hold the right mouse button to look around and fly; a right-click without moving is noted.
        void Fly(Mouse mouse, Keyboard kb, bool typing)
        {
            if (mouse.rightButton.wasPressedThisFrame)
            {
                rightPressedAt = mouse.position.ReadValue();
                looked = false;
            }
            flying = mouse.rightButton.isPressed;
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            if (flying)
            {
                if (!looked && (mouse.position.ReadValue() - rightPressedAt).sqrMagnitude > DragThreshold) looked = true;
                if (looked)
                {
                    var delta = mouse.delta.ReadValue();
                    yaw += delta.x * lookSensitivity;
                    pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -89f, 89f);
                }
                var move = Vector3.zero;
                if (!typing)
                {
                    if (kb.wKey.isPressed) move += Vector3.forward;
                    if (kb.sKey.isPressed) move += Vector3.back;
                    if (kb.dKey.isPressed) move += Vector3.right;
                    if (kb.aKey.isPressed) move += Vector3.left;
                    if (kb.eKey.isPressed) move += Vector3.up;
                    if (kb.qKey.isPressed) move += Vector3.down;
                }
                if (move != Vector3.zero)
                {
                    looked = true;
                    float speed = flySpeed * (kb.leftShiftKey.isPressed ? 3f : 1f);
                    cameraPosition += (rotation * new Vector3(move.x, 0f, move.z) + Vector3.up * move.y) * speed * Time.unscaledDeltaTime;
                }
            }
            if (mouse.rightButton.wasReleasedThisFrame && !looked) rightClicked = true;
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f && !OverUi) cameraPosition += rotation * Vector3.forward * (Mathf.Sign(scroll) * 3f);
        }

        // Works out what's under the mouse: a panel, a piece, and the surface a new piece would go on.
        void Point(Vector2 mousePosition)
        {
            var gui = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
            overPanel = panelRect.Contains(gui);
            overBin = binRect.Contains(gui);
            overRoom = roomRect.Contains(gui);
            overDetails = selection.Count > 0 && detailsRect.Contains(gui);
            hoveredRow = overPanel ? rows.FindIndex(r => r.rect.Contains(gui)) : -1;
            hasSurface = false;
            hoveredPiece = null;
            if (OverUi || !cam) return;

            var ray = cam.ScreenPointToRay(mousePosition);
            foreach (var hit in Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
            {
                if (!pieceOf.TryGetValue(hit.collider.gameObject, out var piece)) continue;
                // While sliding the selection, look past it.
                if (drag == Drag.Selection && selection.Contains(piece)) continue;
                if (hoveredPiece == null)
                {
                    hoveredPiece = piece;
                    hoveredPoint = hit.point;
                }
                // New pieces go through markers, pads and lamps, onto the surface behind them.
                if (Kind(piece).shape is PieceShape.Marker or PieceShape.Pad or PieceShape.Lamp) continue;
                hasSurface = true;
                surfaceNormal = GridMath.Axis(hit.normal);
                surfaceCell = Vector3Int.FloorToInt(hit.point - (Vector3)surfaceNormal * 0.01f);
                return;
            }
            // Nothing there: the ground grid.
            if (ray.direction.y < 0f && new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter))
            {
                var p = ray.GetPoint(enter);
                hasSurface = true;
                surfaceNormal = Vector3Int.up;
                surfaceCell = new Vector3Int(Mathf.FloorToInt(p.x), -1, Mathf.FloorToInt(p.z));
            }
        }

        void HandleMouse(Mouse mouse, Keyboard kb)
        {
            var position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) Press(position, kb);
            if (mouse.leftButton.isPressed && (drag is Drag.PendingItem or Drag.PendingSelection) && (position - pressedAt).sqrMagnitude > DragThreshold)
                StartDrag();
            if (drag == Drag.Drawing) Draw(position);
            if (drag == Drag.Selection) Slide(position);
            if (mouse.leftButton.wasReleasedThisFrame) Release();
        }

        void Press(Vector2 position, Keyboard kb)
        {
            pressedAt = position;
            // Clicking anywhere but the room box stops typing the name.
            if (!overRoom) dropFocus = true;
            if (overDetails || overRoom)
            {
                var gui = new Vector2(position.x, Screen.height - position.y);
                foreach (var (rect, action) in buttons)
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
                if (row.kind == null)
                {
                    if (!closedFolders.Remove(row.folder)) closedFolders.Add(row.folder);
                    return;
                }
                dragKind = row.kind;
                drag = Drag.PendingItem;
                return;
            }
            if (brush != null)
            {
                if (!hasSurface) return;
                if (brush.shape == PieceShape.Ramp && surfaceNormal != Vector3Int.up)
                {
                    Say("Ramps go on floors");
                    return;
                }
                if (brush.Draws)
                {
                    drawStart = drawEnd = Spot;
                    drawNormal = surfaceNormal;
                    drag = Drag.Drawing;
                }
                else Add(Placed(brush, Spot, surfaceNormal, brushTurn), select: false);
                return;
            }

            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (hoveredPiece == null)
            {
                if (!shift) Deselect();
                return;
            }
            if (shift)
            {
                if (!selection.Remove(hoveredPiece)) selection.Add(hoveredPiece);
                selectionSaved = false;
                return;
            }
            if (!selection.Contains(hoveredPiece)) Select(hoveredPiece);
            grabPoint = hoveredPoint;
            drag = Drag.PendingSelection;
        }

        void StartDrag()
        {
            if (drag == Drag.PendingItem)
            {
                brush = null;
                dragTurn = 0;
                drag = Drag.NewPiece;
                return;
            }
            if (selection.Count == 0)
            {
                drag = Drag.None;
                return;
            }
            MarkChanged();
            grabMins = selection.Select(p => p.min).ToList();
            grabStep = Vector3Int.zero;
            drag = Drag.Selection;
        }

        // Stretches the rectangle being drawn to the cell under the mouse, on the surface it started on.
        void Draw(Vector2 mousePosition)
        {
            var ray = cam.ScreenPointToRay(mousePosition);
            var plane = new Plane(drawNormal, (Vector3)drawStart + Vector3.one * 0.5f);
            if (!plane.Raycast(ray, out float enter)) return;
            var cell = Vector3Int.FloorToInt(ray.GetPoint(enter));
            int axis = Axis(drawNormal);
            cell[axis] = drawStart[axis];
            drawEnd = Vector3Int.Max(AreaMin, Vector3Int.Min(AreaMax - Vector3Int.one, cell));
        }

        // Slides the selection along the level it was grabbed at.
        void Slide(Vector2 mousePosition)
        {
            var ray = cam.ScreenPointToRay(mousePosition);
            if (!new Plane(Vector3.up, grabPoint).Raycast(ray, out float enter)) return;
            var p = ray.GetPoint(enter);
            var step = new Vector3Int(Mathf.RoundToInt(p.x - grabPoint.x), 0, Mathf.RoundToInt(p.z - grabPoint.z));
            if (step == grabStep) return;
            for (int i = 0; i < selection.Count; i++)
                if (!InArea(grabMins[i] + step, selection[i].size)) return;
            grabStep = step;
            for (int i = 0; i < selection.Count; i++) selection[i].min = grabMins[i] + step;
            Refresh(selection);
            dirty = true;
        }

        void Release()
        {
            var was = drag;
            drag = Drag.None;
            switch (was)
            {
                case Drag.PendingItem:
                    // A click on a piece in the panel picks it up as the brush, or puts it down again.
                    if (brush == dragKind) brush = null;
                    else
                    {
                        brush = dragKind;
                        brushTurn = 0;
                        Deselect();
                        Say(brush.Draws ? $"{brush.displayName}: drag across a surface to draw it" : $"{brush.displayName}: click to place one");
                    }
                    break;

                case Drag.NewPiece:
                    if (OverUi || !hasSurface) break;
                    if (dragKind.shape == PieceShape.Ramp && surfaceNormal != Vector3Int.up) Say("Ramps go on floors");
                    else Add(Placed(dragKind, Spot, surfaceNormal, dragTurn), select: true);
                    break;

                case Drag.Drawing:
                    if (brush != null) Add(Drawn(brush, drawStart, drawEnd, drawNormal, brushTurn), select: false);
                    break;

                case Drag.Selection:
                    if (overBin) Delete();
                    break;
            }
        }

        void HandleKeys(Keyboard kb)
        {
            if (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed)
            {
                if (kb.zKey.wasPressedThisFrame) Undo();
                if (kb.sKey.wasPressedThisFrame) SaveRoom();
                if (kb.cKey.wasPressedThisFrame) Copy();
                if (kb.vKey.wasPressedThisFrame) Paste();
                return;
            }
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (drag is Drag.NewPiece or Drag.Drawing) drag = Drag.None;
                else if (brush != null) brush = null;
                else Deselect();
                return;
            }
            if (drag == Drag.None && kb.tabKey.wasPressedThisFrame)
            {
                StartTest();
                return;
            }

            int quarter = (kb.eKey.wasPressedThisFrame ? 1 : 0) - (kb.qKey.wasPressedThisFrame ? 1 : 0);
            if (drag == Drag.NewPiece)
            {
                dragTurn = Wrap(dragTurn + quarter);
                return;
            }
            if (brush != null)
            {
                brushTurn = Wrap(brushTurn + quarter);
                return;
            }
            if (selection.Count == 0 || drag != Drag.None || !cam) return;
            if (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
            {
                Delete();
                return;
            }
            if (quarter != 0) Turn(quarter);

            // Moves follow the camera, snapped to the grid.
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            bool alt = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
            var forward = FlatAxis(cam.transform.forward);
            var right = FlatAxis(cam.transform.right);
            Move(kb.wKey, forward, shift, alt);
            Move(kb.sKey, -forward, shift, alt);
            Move(kb.dKey, right, shift, alt);
            Move(kb.aKey, -right, shift, alt);
            Move(kb.rKey, Vector3Int.up, shift, alt);
            Move(kb.fKey, Vector3Int.down, shift, alt);
        }

        void Move(KeyControl key, Vector3Int step, bool shift, bool alt)
        {
            if (!key.wasPressedThisFrame) return;
            if (shift) Resize(step, grow: true);
            else if (alt) Resize(step, grow: false);
            else Change(p =>
            {
                p.min += step;
                return p;
            });
        }

        static Vector3Int FlatAxis(Vector3 direction)
        {
            var flat = new Vector3(direction.x, 0f, direction.z);
            return GridMath.Axis(flat.sqrMagnitude > 0.0001f ? flat : Vector3.forward);
        }

        // Applies a change to a copy of every selected piece, and keeps it only if they all stay in the area.
        void Change(System.Func<RoomPiece, RoomPiece> change)
        {
            var changed = selection.Select(p => change(p.Copy())).ToList();
            if (!changed.All(p => InArea(p.min, p.size)))
            {
                Say(EdgeOfArea);
                return;
            }
            MarkChanged();
            for (int i = 0; i < selection.Count; i++)
            {
                var p = selection[i];
                p.min = changed[i].min;
                p.size = changed[i].size;
                p.turn = changed[i].turn;
                p.face = changed[i].face;
            }
            Refresh(selection);
            dirty = true;
        }

        // Grows the selected blocks a meter the way of `step`, or shrinks them from that side.
        void Resize(Vector3Int step, bool grow)
        {
            if (selection.Any(p => !Kind(p).Resizable))
            {
                Say("Only blocks and ramps change size");
                return;
            }
            int axis = Axis(step);
            if (!grow && selection.Any(p => p.size[axis] == 1))
            {
                Say("It can't get any thinner");
                return;
            }
            Change(p =>
            {
                int d = grow ? 1 : -1;
                var size = p.size;
                size[axis] += d;
                p.size = size;
                if (step[axis] < 0)
                {
                    var min = p.min;
                    min[axis] -= d;
                    p.min = min;
                }
                return p;
            });
        }

        // Turns the selection a quarter turn around its middle: +1 clockwise seen from above.
        void Turn(int quarter)
        {
            var (min, max) = Bounds(selection);
            int cx = Mathf.RoundToInt((min.x + max.x) * 0.5f);
            int cz = Mathf.RoundToInt((min.z + max.z) * 0.5f);
            Change(p => Turned(p, quarter, cx, cz));
        }

        static RoomPiece Turned(RoomPiece p, int quarter, int cx, int cz)
        {
            int x0 = p.min.x, x1 = p.Max.x, z0 = p.min.z, z1 = p.Max.z;
            p.min = quarter > 0
                ? new Vector3Int(cx + z0 - cz, p.min.y, cz - (x1 - cx))
                : new Vector3Int(cx - (z1 - cz), p.min.y, cz + x0 - cx);
            p.size = new Vector3Int(p.size.z, p.size.y, p.size.x);
            p.turn = Wrap(p.turn + quarter);
            p.face = quarter > 0 ? new Vector3Int(p.face.z, p.face.y, -p.face.x) : new Vector3Int(-p.face.z, p.face.y, p.face.x);
            return p;
        }

        // A piece dropped onto a surface: it sits against the surface, centered on the cell.
        static RoomPiece Placed(PieceKind kind, Vector3Int spot, Vector3Int normal, int turn)
        {
            var size = turn % 2 == 1 ? new Vector3Int(kind.size.z, kind.size.y, kind.size.x) : kind.size;
            var min = Vector3Int.zero;
            for (int a = 0; a < 3; a++)
                min[a] = normal[a] > 0 ? spot[a] : normal[a] < 0 ? spot[a] - size[a] + 1 : spot[a] - (size[a] - 1) / 2;
            return new RoomPiece { kind = kind.id, min = min, size = size, turn = turn, face = normal };
        }

        // A rectangle drawn on a surface, built out of it by the piece's usual height.
        static RoomPiece Drawn(PieceKind kind, Vector3Int start, Vector3Int end, Vector3Int normal, int turn)
        {
            var min = Vector3Int.Min(start, end);
            var size = Vector3Int.Max(start, end) - min + Vector3Int.one;
            int axis = Axis(normal);
            size[axis] = kind.size.y;
            if (normal[axis] < 0) min[axis] = start[axis] - kind.size.y + 1;
            return new RoomPiece { kind = kind.id, min = min, size = size, turn = turn, face = normal };
        }

        void Add(RoomPiece piece, bool select)
        {
            if (!InArea(piece.min, piece.size))
            {
                Say(EdgeOfArea);
                return;
            }
            Save();
            room.pieces.Add(piece);
            CreateView(piece);
            dirty = true;
            if (!select) return;
            Select(piece);
            selectionSaved = true;
        }

        void Delete()
        {
            if (selection.Count == 0) return;
            MarkChanged();
            foreach (var p in selection)
            {
                room.pieces.Remove(p);
                RemoveView(p);
            }
            Say($"Deleted {Count(selection.Count)}");
            selection.Clear();
            selectionSaved = false;
            dirty = true;
        }

        void Copy()
        {
            if (selection.Count == 0)
            {
                Say("Select some pieces to copy first");
                return;
            }
            clipboard = selection.Select(p => p.Copy()).ToList();
            Say($"Copied {Count(clipboard.Count)}");
        }

        // Pastes the copied pieces onto the surface under the mouse, centered on it.
        void Paste()
        {
            if (clipboard == null)
            {
                Say("Nothing copied yet: select pieces and press Ctrl+C");
                return;
            }
            var (min, max) = Bounds(clipboard);
            var offset = hasSurface && !OverUi
                ? new Vector3Int(Spot.x - (min.x + max.x) / 2, Spot.y - min.y, Spot.z - (min.z + max.z) / 2)
                : new Vector3Int(2, 0, 2);
            var pasted = clipboard.Select(p =>
            {
                var copy = p.Copy();
                copy.min += offset;
                return copy;
            }).ToList();
            if (!pasted.All(p => InArea(p.min, p.size)))
            {
                Say(EdgeOfArea);
                return;
            }
            Save();
            foreach (var p in pasted)
            {
                room.pieces.Add(p);
                CreateView(p);
            }
            selection.Clear();
            selection.AddRange(pasted);
            selectionSaved = true;
            dirty = true;
            Say($"Pasted {Count(pasted.Count)}");
        }

        void SetChance(int chance)
        {
            if (selection.All(p => p.chance == chance)) return;
            MarkChanged();
            foreach (var p in selection) p.chance = chance;
            Refresh(selection);
            dirty = true;
        }

        void Select(RoomPiece piece)
        {
            selection.Clear();
            selection.Add(piece);
            selectionSaved = false;
        }

        void Deselect()
        {
            if (drag == Drag.Selection) return;
            selection.Clear();
            selectionSaved = false;
        }

        void MarkChanged()
        {
            if (selectionSaved) return;
            Save();
            selectionSaved = true;
        }

        void Save()
        {
            undo.Add(room.Copy());
            if (undo.Count > 100) undo.RemoveAt(0);
        }

        void Undo()
        {
            if (drag != Drag.None) return;
            if (undo.Count == 0)
            {
                Say("Nothing to undo");
                return;
            }
            room = undo[^1];
            undo.RemoveAt(undo.Count - 1);
            selection.Clear();
            selectionSaved = false;
            RebuildViews();
            dirty = true;
        }

        // ---- The room's file ----

        string FolderPath => Path.Combine(Application.dataPath, RoomsFolder);

        void RefreshRoomList() =>
            savedRooms = Directory.Exists(FolderPath)
                ? Directory.GetFiles(FolderPath, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n).ToList()
                : new List<string>();

        string UnusedName()
        {
            for (int i = 1; ; i++)
                if (!savedRooms.Contains($"Room {i}")) return $"Room {i}";
        }

        static string Clean(string name) =>
            new string((name ?? "").Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').ToArray()).Trim();

        // Saves into the project. Saving under the name of another saved room asks first: save again to replace it.
        void SaveRoom()
        {
            string name = Clean(room.name);
            if (name.Length == 0)
            {
                Say("Give the room a name first");
                return;
            }
            bool replacesAnother = savedRooms.Contains(name) && !(name == loadedFrom);
            if (replacesAnother && !(replacing == name && Time.unscaledTime < replacingUntil))
            {
                replacing = name;
                replacingUntil = Time.unscaledTime + 4f;
                Say($"There's already a room called {name}. Save again to replace it");
                return;
            }
            replacing = null;
            room.name = name;
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(Path.Combine(FolderPath, name + ".json"), room.ToJson());
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.ImportAsset($"Assets/{RoomsFolder}/{name}.json");
#endif
            loadedFrom = name;
            dirty = false;
            RefreshRoomList();
            var warnings = Warnings();
            Say(warnings.Count == 0 ? $"Saved {name}" : $"Saved {name}, but: {string.Join(", ", warnings)}");
        }

        // The saved room this one was loaded from or last saved as.
        string loadedFrom;

        void LoadRoom(string name)
        {
            RoomData loaded;
            try
            {
                loaded = RoomData.FromJson(File.ReadAllText(Path.Combine(FolderPath, name + ".json")));
            }
            catch (System.Exception e)
            {
                Say($"Couldn't read {name}: {e.Message}");
                return;
            }
            if (loaded == null)
            {
                Say($"Couldn't read {name}");
                return;
            }
            Save();
            room = loaded;
            loadedFrom = name;
            brush = null;
            selection.Clear();
            selectionSaved = false;
            RebuildViews();
            dirty = false;
            FrameRoom();
            int unknown = room.pieces.Count(p => Kind(p) == null);
            Say(unknown == 0 ? $"Loaded {name}" : $"Loaded {name}, but {Count(unknown)} of a kind that doesn't exist any more are hidden");
        }

        void NewRoom()
        {
            Save();
            room = new RoomData { name = UnusedName() };
            loadedFrom = null;
            brush = null;
            selection.Clear();
            selectionSaved = false;
            RebuildViews();
            dirty = false;
            Say("New room. Ctrl+Z brings the last one back");
        }

        // Points the camera at the whole room.
        void FrameRoom()
        {
            if (room.pieces.Count == 0) return;
            var (min, max) = Bounds(room.pieces);
            var center = ((Vector3)min + (Vector3)max) * 0.5f;
            float reach = ((Vector3)(max - min)).magnitude;
            yaw = 0f;
            pitch = 35f;
            cameraPosition = center - Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * (reach * 0.75f + 10f);
        }

        List<string> Warnings()
        {
            var list = new List<string>();
            int entries = room.All(RoomPieces.Entry).Count();
            if (entries == 0) list.Add("no entry");
            else if (entries > 1) list.Add($"{entries} entries (a room has one)");
            if (room.type != RoomType.Shaft && !room.All(RoomPieces.Exit).Any()) list.Add("no exit");
            if (room.type == RoomType.Combat)
            {
                int spawns = room.pieces.Count(p => Kind(p)?.spawn == true);
                if (spawns < EnoughSpawns) list.Add($"only {spawns} spawn spots (aim for {EnoughSpawns} or more)");
            }
            if (room.type == RoomType.Workshop && !room.All(RoomPieces.Shop).Any()) list.Add("no shop stands");
            if (room.type == RoomType.Shaft && !room.All(RoomPieces.Pod).Any()) list.Add("no escape pod");
            if (overlapping.Count > 0) list.Add($"{Count(overlapping.Count)} overlap");
            return list;
        }

        // ---- Testing ----

        // Builds the room for real, as a run would, and drops the robot in at the entry.
        void StartTest()
        {
            var entry = room.All(RoomPieces.Entry).FirstOrDefault();
            if (entry == null)
            {
                Say("Put an Entry marker in the room first: the robot drops in there");
                return;
            }
            if (!robot || !aim) return;
            drag = Drag.None;
            brush = null;
            Deselect();
            dropFocus = true;
            testing = true;
            viewRoot.gameObject.SetActive(false);
            previewRoot.gameObject.SetActive(false);
            testRoom = RoomBuilder.Build(room, kit, null, new System.Random());

            float facing = entry.turn * 90f;
            var spawn = entry.Bottom + Vector3.up * 0.05f;
            robot.transform.SetPositionAndRotation(spawn, Quaternion.Euler(0f, facing, 0f));
            foreach (var go in testObjects)
                if (go) go.SetActive(true);
            // The first time, the robot fits its parts and stands at its spawn point as it starts.
            if (robotStarted && loadout) loadout.RespawnAt(spawn);
            robotStarted = true;
            aim.Face(facing);
            aim.Suspended = false;
            aim.SetLocked(true);
            if (editorCamera) editorCamera.Priority = 0;
        }

        void StopTest()
        {
            testing = false;
            aim.Suspended = true;
            PartChunk.ClearAll();
            foreach (var go in testObjects)
                if (go) go.SetActive(false);
            if (testRoom) Destroy(testRoom.gameObject);
            viewRoot.gameObject.SetActive(true);
            previewRoot.gameObject.SetActive(true);
            if (editorCamera) editorCamera.Priority = 30;
        }

        // ---- Pieces on show ----

        void CreateView(RoomPiece piece)
        {
            var kind = Kind(piece);
            if (kind == null) return;
            var go = RoomBuilder.CreatePiece(piece, kind, kit, viewRoot, inEditor: true);
            views[piece] = new View { go = go, renderers = go.GetComponentsInChildren<Renderer>() };
            pieceOf[go] = piece;
        }

        void RemoveView(RoomPiece piece)
        {
            if (!views.Remove(piece, out var view)) return;
            pieceOf.Remove(view.go);
            DestroyView(view);
        }

        void DestroyView(View view)
        {
            foreach (var r in view.renderers) looks.Remove(r);
            foreach (var filter in view.go.GetComponentsInChildren<MeshFilter>()) Destroy(filter.sharedMesh);
            Destroy(view.go);
        }

        // Pieces change shape when they move, since their textures line up with the world.
        void Refresh(IEnumerable<RoomPiece> pieces)
        {
            foreach (var p in pieces)
            {
                RemoveView(p);
                CreateView(p);
            }
        }

        void RebuildViews()
        {
            foreach (var view in views.Values) DestroyView(view);
            views.Clear();
            pieceOf.Clear();
            foreach (var p in room.pieces) CreateView(p);
        }

        // This frame's preview, and every piece's highlight: selected, pointed at, or red where pieces overlap.
        void Show()
        {
            previewCount = 0;
            FindOverlaps();
            if (drag == Drag.NewPiece)
            {
                if (hasSurface && !OverUi) ShowPreview(Placed(dragKind, Spot, surfaceNormal, dragTurn));
            }
            else if (drag == Drag.Drawing) ShowPreview(Drawn(brush, drawStart, drawEnd, drawNormal, brushTurn));
            else if (brush != null && drag == Drag.None && hasSurface && !OverUi)
                ShowPreview(brush.Draws ? Drawn(brush, Spot, Spot, surfaceNormal, brushTurn) : Placed(brush, Spot, surfaceNormal, brushTurn));
            for (int i = previewCount; i < preview.Count; i++) preview[i].gameObject.SetActive(false);

            foreach (var (piece, view) in views) Look(piece, view);
        }

        void FindOverlaps()
        {
            overlapping.Clear();
            var pieces = room.pieces;
            for (int i = 0; i < pieces.Count; i++)
                for (int j = i + 1; j < pieces.Count; j++)
                    if (pieces[i].Overlaps(pieces[j]))
                    {
                        overlapping.Add(pieces[i]);
                        overlapping.Add(pieces[j]);
                    }
        }

        // A hologram of a piece about to go in: green, or red if it would overlap or can't go there.
        void ShowPreview(RoomPiece piece)
        {
            var kind = Kind(piece);
            bool bad = !InArea(piece.min, piece.size) || (kind.shape == PieceShape.Ramp && piece.face != Vector3Int.up) ||
                       room.pieces.Any(p => p.Overlaps(piece));
            block.SetColor(BaseColor, bad ? DoesNotFit : Fits);
            PreviewBlock(piece.Center, piece.size);
            // Which way a ramp rises, or the entry faces.
            if (kind.shape == PieceShape.Ramp || piece.kind == RoomPieces.Entry)
            {
                var facing = piece.Facing;
                float reach = Mathf.Abs(Vector3.Dot(facing, piece.size)) * 0.5f;
                PreviewBlock(piece.Center + facing * reach + Vector3.up * (piece.size.y * 0.5f), Vector3.one * 0.4f);
            }
        }

        void PreviewBlock(Vector3 center, Vector3 size)
        {
            if (previewCount == preview.Count)
            {
                var r = RobotBody.CreateBlock(previewRoot, Vector3.zero, Vector3.one, kit.preview).GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                preview.Add(r);
            }
            var renderer = preview[previewCount++];
            renderer.gameObject.SetActive(true);
            // Just bigger than the piece, so its faces don't flicker against the pieces it touches.
            renderer.transform.SetPositionAndRotation(center, Quaternion.identity);
            renderer.transform.localScale = size + Vector3.one * 0.04f;
            renderer.SetPropertyBlock(block);
        }

        void Look(RoomPiece piece, View view)
        {
            var kind = Kind(piece);
            bool selected = selection.Contains(piece);
            bool hovered = piece == hoveredPiece && drag == Drag.None && brush == null;
            bool red = overlapping.Contains(piece);
            bool seeThrough = kind.IsMarker || piece.chance < 100;
            foreach (var r in view.renderers)
            {
                if (!r) continue;
                Color color;
                if (seeThrough)
                {
                    var c = red ? OverlapColor : kind.color;
                    color = new Color(c.r, c.g, c.b, selected ? 0.8f : hovered ? 0.6f : 0.4f);
                }
                else
                {
                    float strength = selected ? 1.6f : hovered ? 1.3f : 1f;
                    if (!red && strength == 1f)
                    {
                        if (looks.Remove(r)) r.SetPropertyBlock(null);
                        continue;
                    }
                    color = red ? OverlapColor * (selected ? 1.3f : 1f)
                        : r.sharedMaterial.GetColor(BaseColor) * strength + Color.white * ((strength - 1f) * 0.25f);
                }
                if (looks.TryGetValue(r, out var current) && current == color) continue;
                looks[r] = color;
                block.SetColor(BaseColor, color);
                r.SetPropertyBlock(block);
            }
        }

        // ---- Helpers ----

        static PieceKind Kind(RoomPiece piece) => RoomPieces.Get(piece.kind);

        static int Axis(Vector3Int direction) => direction.x != 0 ? 0 : direction.y != 0 ? 1 : 2;

        static int Wrap(int turn) => (turn % 4 + 4) % 4;

        static string Count(int n) => n == 1 ? "1 piece" : $"{n} pieces";

        static bool InArea(Vector3Int min, Vector3Int size)
        {
            var max = min + size;
            return min.x >= AreaMin.x && min.y >= AreaMin.y && min.z >= AreaMin.z && max.x <= AreaMax.x && max.y <= AreaMax.y && max.z <= AreaMax.z;
        }

        static (Vector3Int min, Vector3Int max) Bounds(IReadOnlyList<RoomPiece> pieces)
        {
            var min = pieces[0].min;
            var max = pieces[0].Max;
            foreach (var p in pieces)
            {
                min = Vector3Int.Min(min, p.min);
                max = Vector3Int.Max(max, p.Max);
            }
            return (min, max);
        }

        static Texture2D GridTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
            };
            var clear = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, x == 0 || y == 0 ? Color.white : clear);
            texture.Apply(true);
            return texture;
        }

        void Say(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 3f;
        }

        // ---- On screen ----

        void OnGUI()
        {
            labelStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold };
            chipStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = Color.black } };
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = true };
            folderStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.72f, 0.35f) } };
            wrapStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
            smallStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.75f, 0.75f, 0.78f) } };
            warnStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, normal = { textColor = WarningText } };
            countStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 11 };

            if (testing)
            {
                GUI.Box(new Rect(Screen.width - 400f, 10f, 390f, 26f), $"Testing {room.name}  (no enemies yet)    Tab: back to editing");
                return;
            }
            // Enter or Esc finishes typing the name.
            var e = Event.current;
            if (e.type == EventType.KeyDown && GUIUtility.keyboardControl != 0 && e.keyCode is KeyCode.Return or KeyCode.KeypadEnter or KeyCode.Escape)
            {
                GUIUtility.keyboardControl = 0;
                e.Use();
            }
            if (dropFocus && e.type == EventType.Layout)
            {
                GUIUtility.keyboardControl = 0;
                dropFocus = false;
            }

            buttons.Clear();
            DrawLabels();
            DrawPanel();
            DrawRoomBox();
            DrawDetails();
            DrawCursorLabel();
            GUI.Label(new Rect(PanelWidth + 30f, Screen.height - 28f, Screen.width - PanelWidth - 40f, 24f),
                "Click a piece in the panel, then draw or place it    Click to select    Hold right mouse to fly (WASD, Q/E)    Scroll to move    Ctrl+S save    Tab test",
                smallStyle);
            if (Time.unscaledTime < messageUntil)
            {
                float width = Mathf.Min(Screen.width - 40f, Mathf.Max(500f, GUI.skin.box.CalcSize(new GUIContent(message)).x + 20f));
                GUI.Box(new Rect(Screen.width * 0.5f - width * 0.5f, Screen.height - 66f, width, 26f), message);
            }
        }

        // Tags on markers, and the chance on pieces that only sometimes appear.
        void DrawLabels()
        {
            if (!cam) return;
            foreach (var piece in room.pieces)
            {
                var kind = Kind(piece);
                if (kind == null) continue;
                string text = kind.IsMarker ? kind.tag : null;
                if (piece.chance < 100) text = text == null ? $"{piece.chance}%" : $"{text} {piece.chance}%";
                if (text == null) continue;
                var screen = cam.WorldToScreenPoint(piece.Center);
                if (screen.z < 0f || screen.z > 150f) continue;
                float width = labelStyle.CalcSize(new GUIContent(text)).x + 10f;
                var rect = new Rect(screen.x - width * 0.5f, Screen.height - screen.y - 9f, width, 18f);
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = kind.IsMarker ? kind.color : WarningText;
                GUI.Label(rect, text, labelStyle);
                GUI.color = Color.white;
            }
        }

        // The pieces, folder by folder, and the bin below them.
        void DrawPanel()
        {
            rows.Clear();
            float y = 40f;
            foreach (var folder in Folders)
            {
                rows.Add(new PanelRow { rect = new Rect(16f, y, PanelWidth - 12f, RowHeight - 2f), folder = folder });
                y += RowHeight;
                if (closedFolders.Contains(folder)) continue;
                foreach (var kind in RoomPieces.All.Where(k => k.folder == folder))
                {
                    rows.Add(new PanelRow { rect = new Rect(24f, y, PanelWidth - 20f, RowHeight - 2f), folder = folder, kind = kind });
                    y += RowHeight;
                }
            }
            panelRect = new Rect(10f, 10f, PanelWidth, y);
            binRect = new Rect(10f, panelRect.yMax + 8f, PanelWidth, 46f);

            GUI.Box(panelRect, GUIContent.none);
            GUI.Label(new Rect(18f, 14f, PanelWidth - 16f, 22f), "ROOM PIECES", titleStyle);
            var mouse = Event.current.mousePosition;
            foreach (var row in rows)
            {
                if (row.kind == null)
                {
                    GUI.Label(row.rect, $"{(closedFolders.Contains(row.folder) ? "+" : "-")}  {row.folder.ToString().ToUpper()}", folderStyle);
                    continue;
                }
                bool armed = brush == row.kind;
                GUI.color = armed ? new Color(0.55f, 1f, 0.65f) : row.rect.Contains(mouse) && drag == Drag.None ? Color.white : new Color(0.85f, 0.85f, 0.85f);
                GUI.Box(row.rect, GUIContent.none, GUI.skin.button);
                GUI.color = row.kind.color;
                GUI.DrawTexture(new Rect(row.rect.x + 6f, row.rect.y + 4f, 12f, 12f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(row.rect.x + 24f, row.rect.y, row.rect.width - 30f, row.rect.height), row.kind.displayName);
                if (armed) GUI.Label(new Rect(row.rect.xMax - 66f, row.rect.y, 60f, row.rect.height), row.kind.Draws ? "drawing" : "placing", countStyle);
            }

            bool binIt = drag == Drag.Selection && overBin;
            GUI.color = binIt ? new Color(1f, 0.4f, 0.35f) : new Color(1f, 0.8f, 0.8f);
            GUI.Box(binRect, binIt ? "\nDrop to delete" : "BIN\nDrag pieces here to delete them");
            GUI.color = Color.white;
        }

        // The room's name and size, saving, testing, and the saved rooms.
        void DrawRoomBox()
        {
            var warnings = Warnings();
            float width = SideWidth - 20f;
            float height = 6f + 24f + 26f + 30f + 30f + 30f + 30f + 18f + savedRooms.Count * RowHeight + (warnings.Count > 0 ? 6f + warnings.Count * 18f : 0f) + 8f;
            roomRect = new Rect(Screen.width - SideWidth - 10f, 10f, SideWidth, height);
            GUI.Box(roomRect, GUIContent.none);
            float x = roomRect.x + 10f, y = roomRect.y + 6f;

            GUI.Label(new Rect(x, y, width, 22f), dirty ? "ROOM  (unsaved changes)" : "ROOM", titleStyle);
            y += 24f;
            GUI.Label(new Rect(x, y + 2f, 44f, 20f), "Name", smallStyle);
            string typed = GUI.TextField(new Rect(x + 46f, y, width - 46f, 20f), room.name ?? "", 40);
            if (typed != room.name)
            {
                room.name = typed;
                dirty = true;
            }
            y += 26f;

            float third = (width - 2 * 6f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var size = (RoomSize)i;
                Chip(new Rect(x + i * (third + 6f), y, third, 22f), size.ToString(), room.size == size, () =>
                {
                    if (room.size == size) return;
                    Save();
                    room.size = size;
                    dirty = true;
                });
            }
            y += 30f;
            int types = System.Enum.GetValues(typeof(RoomType)).Length;
            float share = (width - (types - 1) * 6f) / types;
            for (int i = 0; i < types; i++)
            {
                var type = (RoomType)i;
                Chip(new Rect(x + i * (share + 6f), y, share, 22f), type.ToString(), room.type == type, () =>
                {
                    if (room.type == type) return;
                    Save();
                    room.type = type;
                    dirty = true;
                });
            }
            y += 30f;

            float half = (width - 6f) / 2f;
            Button(new Rect(x, y, half, 24f), "Save (Ctrl+S)", SaveRoom);
            Button(new Rect(x + half + 6f, y, half, 24f), "Test (Tab)", StartTest);
            y += 30f;
            Button(new Rect(x, y, half, 24f), "New room", NewRoom);
            y += 30f;

            GUI.Label(new Rect(x, y, width, 18f), savedRooms.Count == 0 ? "No saved rooms yet" : "Saved rooms (click one to load it)", smallStyle);
            y += 18f;
            foreach (var saved in savedRooms)
            {
                string name = saved;
                Button(new Rect(x, y, width, RowHeight - 2f), name, () => LoadRoom(name), highlight: name == loadedFrom);
                y += RowHeight;
            }
            if (warnings.Count == 0) return;
            y += 6f;
            foreach (var warning in warnings)
            {
                GUI.Label(new Rect(x, y, width, 18f), "!  " + warning, warnStyle);
                y += 18f;
            }
        }

        // What's selected: what it is, its size, its chance to appear, and how to change it.
        void DrawDetails()
        {
            if (selection.Count == 0)
            {
                detailsRect = default;
                return;
            }
            float width = SideWidth - 20f;
            var first = selection[0];
            var kind = Kind(first);
            string title = selection.Count == 1 ? kind.displayName : $"{kind.displayName} + {selection.Count - 1} more";
            bool red = selection.Any(overlapping.Contains);
            string status = red ? "Overlaps another piece" : "Fits";
            string size = selection.Count == 1 ? $"{first.size.x} × {first.size.z} m, {first.size.y} m high" : null;
            const string keys = "W A S D, R/F  move    Q/E  turn\nShift + a move key  grow\nAlt + a move key  shrink\nCtrl+C, Ctrl+V  copy and paste\nDel  delete    Esc  let go    Ctrl+Z  undo";

            float titleHeight = titleStyle.CalcHeight(new GUIContent(title), width);
            float descriptionHeight = wrapStyle.CalcHeight(new GUIContent(kind.description), width);
            float keysHeight = smallStyle.CalcHeight(new GUIContent(keys), width);
            float height = 8f + titleHeight + 18f + descriptionHeight + 6f + 20f + (size != null ? 20f : 0f) + 18f + 30f + 32f + keysHeight + 10f;
            detailsRect = new Rect(Screen.width - SideWidth - 10f, roomRect.yMax + 8f, SideWidth, height);
            GUI.Box(detailsRect, GUIContent.none);
            float x = detailsRect.x + 10f, y = detailsRect.y + 8f;

            GUI.Label(new Rect(x, y, width, titleHeight), title, titleStyle);
            y += titleHeight;
            GUI.Label(new Rect(x, y, width, 18f), kind.folder.ToString().ToUpper(), smallStyle);
            y += 18f;
            GUI.Label(new Rect(x, y, width, descriptionHeight), kind.description, wrapStyle);
            y += descriptionHeight + 6f;
            GUI.contentColor = red ? ErrorText : OkText;
            GUI.Label(new Rect(x, y, width, 20f), status, wrapStyle);
            GUI.contentColor = Color.white;
            y += 20f;
            if (size != null)
            {
                GUI.Label(new Rect(x, y, width, 20f), size, wrapStyle);
                y += 20f;
            }

            GUI.Label(new Rect(x, y, width, 18f), "Chance to appear each time the room loads", smallStyle);
            y += 18f;
            float chip = (width - 3 * 6f) / 4f;
            for (int i = 0; i < Chances.Length; i++)
            {
                int chance = Chances[i];
                Chip(new Rect(x + i * (chip + 6f), y, chip, 22f), $"{chance}%", selection.All(p => p.chance == chance), () => SetChance(chance));
            }
            y += 30f;
            Button(new Rect(x, y, width, 24f), "Delete (Del)", Delete);
            y += 32f;
            GUI.Label(new Rect(x, y, width, keysHeight), keys, smallStyle);
        }

        // Next to the mouse: what a drop or a click would do, or what you're pointing at.
        void DrawCursorLabel()
        {
            string text = null;
            if (drag == Drag.NewPiece)
                text = $"{dragKind.displayName}\n" + (OverUi || !hasSurface ? "Drop it in the room" : "Q/E turn it");
            else if (drag == Drag.Drawing)
            {
                var p = Drawn(brush, drawStart, drawEnd, drawNormal, brushTurn);
                text = $"{p.size.x} × {p.size.z} m, {p.size.y} m high";
            }
            else if (brush != null && !OverUi)
                text = (brush.Draws ? $"{brush.displayName}: drag to draw" : $"{brush.displayName}: click to place") +
                       (brush.shape is PieceShape.Ramp or PieceShape.Marker or PieceShape.Pad ? "    Q/E turn" : "") + "\nRight-click or Esc: done";
            else if (drag == Drag.None && hoveredPiece != null && !OverUi)
                text = $"{Kind(hoveredPiece).displayName}\nClick to select, drag to slide";
            if (text == null) return;
            var mouse = Event.current.mousePosition;
            var size = GUI.skin.box.CalcSize(new GUIContent(text));
            GUI.Box(new Rect(mouse.x + 18f, mouse.y + 14f, size.x + 12f, size.y + 4f), text);
        }

        void Button(Rect rect, string text, System.Action action, bool highlight = false)
        {
            GUI.color = highlight ? new Color(0.55f, 1f, 0.65f) : Color.white;
            GUI.Box(rect, text, GUI.skin.button);
            GUI.color = Color.white;
            buttons.Add((rect, action));
        }

        void Chip(Rect rect, string text, bool on, System.Action action)
        {
            GUI.color = on ? ChipColor : new Color(ChipColor.r, ChipColor.g, ChipColor.b, 0.3f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, text, chipStyle);
            buttons.Add((rect, action));
        }
    }
}
