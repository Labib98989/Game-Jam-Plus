using UnityEngine;

namespace CoreDump
{
    // A room's way out: a lit doorway, red while it's locked and green once it's open. Walk into it
    // to leave.
    public class ExitDoor : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color Locked = new(1f, 0.25f, 0.2f);
        static readonly Color Unlocked = new(0.3f, 1f, 0.45f);

        Renderer panel;
        MaterialPropertyBlock block;

        public bool IsOpen { get; private set; }

        // Stands on the exit marker's floor, facing the way the marker faces (into the room).
        public static ExitDoor Create(Transform parent, RoomPiece marker, Material frame, Material glow)
        {
            var go = new GameObject("Exit");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(marker.Bottom, Quaternion.LookRotation(marker.Facing));
            RobotBody.CreateBlock(go.transform, new Vector3(-1.75f, 2.25f, 0f), new Vector3(0.5f, 4.5f, 0.6f), frame);
            RobotBody.CreateBlock(go.transform, new Vector3(1.75f, 2.25f, 0f), new Vector3(0.5f, 4.5f, 0.6f), frame);
            RobotBody.CreateBlock(go.transform, new Vector3(0f, 4.25f, 0f), new Vector3(4f, 0.5f, 0.6f), frame);
            var door = go.AddComponent<ExitDoor>();
            door.panel = RobotBody.CreateBlock(go.transform, new Vector3(0f, 2f, -0.1f), new Vector3(3f, 4f, 0.15f), glow).GetComponent<Renderer>();
            door.panel.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            door.block = new MaterialPropertyBlock();
            door.SetOpen(false);
            return door;
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            block.SetColor(BaseColor, open ? Unlocked : Locked);
            panel.SetPropertyBlock(block);
        }

        // Whether a point is in the doorway, or just in front of it.
        public bool Contains(Vector3 point)
        {
            var local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) < 2f && local.y > -1.5f && local.y < 5f && local.z > -1f && local.z < 2.5f;
        }

        public Vector3 Middle => transform.position + Vector3.up * 2f;
    }
}
