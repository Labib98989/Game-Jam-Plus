using System.Linq;
using UnityEngine;

namespace CoreDump
{
    // A stand in the workshop holding one free part, turning slowly above it. Walk into the stand
    // to take the part; it goes into your inventory, ready to build on.
    public class ShopStand : MonoBehaviour
    {
        Transform display;

        public PartDefinition Part { get; private set; }
        public bool Taken => !Part;

        public static ShopStand Create(Transform parent, Vector3 floorPoint, PartDefinition part, RobotBody robot, Material standMaterial)
        {
            var go = new GameObject($"Shop Stand: {part.displayName}");
            go.transform.SetParent(parent, false);
            go.transform.position = floorPoint;
            var plinth = RobotBody.CreateBlock(go.transform, new Vector3(0f, 0.4f, 0f), new Vector3(1.4f, 0.8f, 1.4f), standMaterial);
            plinth.AddComponent<BoxCollider>();

            // The part itself, at the robot's scale, centered over the stand.
            var display = new GameObject(part.displayName).transform;
            display.SetParent(go.transform, false);
            display.localPosition = new Vector3(0f, 1.7f, 0f);
            display.localScale = Vector3.one * robot.cellSize;
            var holder = new GameObject("Cubes").transform;
            holder.SetParent(display, false);
            robot.BuildPartVisual(holder, part, false, null);
            var middle = part.cubes.Aggregate(Vector3.zero, (sum, c) => sum + (Vector3)c) / part.cubes.Length;
            holder.localPosition = -middle;

            var stand = go.AddComponent<ShopStand>();
            stand.Part = part;
            stand.display = display;
            return stand;
        }

        void Update()
        {
            if (display) display.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
        }

        public PartDefinition Take()
        {
            var part = Part;
            Part = null;
            if (display) Destroy(display.gameObject);
            return part;
        }

        public Vector3 LabelPoint => transform.position + Vector3.up * 2.6f;
    }
}
