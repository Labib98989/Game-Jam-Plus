using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CoreDump
{
    // Parts knocked off the robot, as one physics object that keeps its shape. Get close to it with
    // the robot and every part snaps back into its old spot, as long as that spot is still free.
    // Leave it lying too long and a Skitter grabs it and runs, the part hovering over it; touch the
    // part to take it back, or kill the Skitter to make it drop. A lost head blinks, beeps, and glows
    // with a huge aura that shows through walls, so you can find it from anywhere.
    public class PartChunk : MonoBehaviour
    {
        [Tooltip("Snaps back on when one of its cubes is this close to one of the robot's, center to center, in meters.")]
        public float snapDistance = 5f;
        [Tooltip("How long it lies loose before a Skitter can grab it, so the robot gets the first chance.")]
        public float stealDelay = 3f;

        // The head's aura: its radius in meters, and how close the camera gets before it has faded out.
        const float AuraRadius = 6f;
        const float AuraFadeFrom = 30f, AuraFadeTo = 9f;
        const float BeepEvery = 0.8f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly List<PartChunk> live = new();

        readonly List<RobotBody.Mount> entries = new();
        readonly List<Renderer> headRenderers = new();
        RobotBody owner;
        float pickupTime, stealTime, nextBeep;
        // The middle of its cubes, in its own space, and how high over its carrier that middle floats.
        Vector3 localMiddle;
        float hoverHeight;
        MaterialPropertyBlock blink, glow;
        Renderer aura;
        Color auraColor;

        public static IReadOnlyList<PartChunk> Live => live;
        public Rigidbody Body { get; private set; }
        public bool HasHead => entries.Any(e => e.definition is HeadDefinition);
        public Vector3 Center => transform.TransformPoint(localMiddle);
        // Being carried off by a Skitter, floating over it.
        public bool Carried { get; private set; }
        public Transform Carrier { get; private set; }
        // Lying loose long enough for a Skitter to go for it.
        public bool Stealable => !Carried && Time.time >= stealTime;

        // Carried off: it stops being a physics object and floats `height` meters over the carrier's
        // feet, bobbing and turning, until it's dropped or the robot touches it.
        public void PickUp(Transform carrier, float height)
        {
            Carried = true;
            Carrier = carrier;
            hoverHeight = height;
            Body.isKinematic = true;
            // Interpolation would keep pulling it back to where its body last was.
            Body.interpolation = RigidbodyInterpolation.None;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Hover();
        }

        public void Drop()
        {
            if (!Carried) return;
            Carried = false;
            Carrier = null;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = true;
            Body.isKinematic = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.linearVelocity = Vector3.up * 4f;
            pickupTime = Time.time + 0.5f;
            stealTime = Time.time + stealDelay;
        }

        void Hover()
        {
            var turn = Quaternion.Euler(0f, Time.time * 90f, 0f);
            var middle = Carrier.position + Vector3.up * (hoverHeight + 0.25f * Mathf.Sin(Time.time * 4f));
            transform.SetPositionAndRotation(middle - turn * localMiddle, turn);
        }

        // Takes the parts off the robot and throws them, as one chunk, with `launch` on top of the
        // robot's own speed. A chunk with the head wears `auraMaterial` as its aura.
        public static PartChunk Spawn(RobotBody robot, IEnumerable<PartModule> parts, Vector3 launch, Material auraMaterial = null)
        {
            var go = new GameObject("Knocked-off Parts");
            go.transform.SetPositionAndRotation(robot.transform.position, robot.transform.rotation);
            var chunk = go.AddComponent<PartChunk>();
            chunk.owner = robot;
            chunk.pickupTime = Time.time + 1f;
            chunk.stealTime = Time.time + chunk.stealDelay;

            foreach (var part in parts.ToList())
            {
                chunk.entries.Add(RobotBody.Mount.Of(part));
                var holder = new GameObject(part.Definition.displayName).transform;
                holder.SetParent(go.transform, false);
                holder.SetLocalPositionAndRotation(robot.CellToLocal(part.Anchor), part.Orientation);
                holder.localScale = Vector3.one * robot.cellSize;
                robot.BuildPartVisual(holder, part.Definition, true, null);
                if (part is Head)
                {
                    chunk.headRenderers.AddRange(holder.GetComponentsInChildren<Renderer>());
                    if (auraMaterial) chunk.AddAura(holder, auraMaterial);
                }
                robot.Remove(part);
            }
            // A bit smaller than a cell, so the chunk doesn't start out touching the robot.
            var boxes = go.GetComponentsInChildren<BoxCollider>();
            foreach (var box in boxes) box.size = Vector3.one;
            if (boxes.Length > 0) chunk.localMiddle = boxes.Aggregate(Vector3.zero, (sum, b) => sum + go.transform.InverseTransformPoint(b.transform.position)) / boxes.Length;

            chunk.Body = go.AddComponent<Rigidbody>();
            chunk.Body.mass = 0.5f;
            chunk.Body.interpolation = RigidbodyInterpolation.Interpolate;
            chunk.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            chunk.Body.linearVelocity = robot.Motor.Velocity + launch;
            chunk.Body.angularVelocity = Random.insideUnitSphere * 6f;
            live.Add(chunk);
            return chunk;
        }

        public static void ClearAll()
        {
            foreach (var chunk in live)
                if (chunk) Destroy(chunk.gameObject);
            live.Clear();
        }

        void OnDestroy() => live.Remove(this);

        // A big glowing sphere around the head, drawn over walls.
        void AddAura(Transform head, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Aura";
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(head, false);
            go.transform.localScale = Vector3.one * (AuraRadius * 2f / head.lossyScale.x);
            aura = go.GetComponent<Renderer>();
            aura.sharedMaterial = material;
            aura.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            auraColor = material.GetColor(BaseColor);
        }

        void Update()
        {
            if (headRenderers.Count > 0) Blink();
            if (aura) Glow();
            if (HasHead && Time.time >= nextBeep)
            {
                nextBeep = Time.time + BeepEvery;
                Juice.Play(Sound.HeadBeep, Center);
            }
            if (Carried)
            {
                // A carrier that vanished without dropping it (the room was cleared) lets go anyway.
                if (Carrier) Hover();
                else Drop();
            }
            else if (transform.position.y < -30f)
            {
                // Never lose the head down a pit: drop it back next to the robot.
                if (HasHead && owner)
                {
                    Body.position = owner.transform.position + Vector3.up * 4f;
                    Body.linearVelocity = Vector3.zero;
                }
                else Destroy(gameObject);
                return;
            }
            if (Time.time >= pickupTime && owner && IsNearOwner()) TrySnapBack();
        }

        void Blink()
        {
            blink ??= new MaterialPropertyBlock();
            bool lit = Mathf.Repeat(Time.time, 0.4f) < 0.2f;
            foreach (var r in headRenderers)
            {
                if (lit) blink.SetColor(BaseColor, new Color(1f, 0.35f, 0.25f));
                else blink.Clear();
                r.SetPropertyBlock(blink);
            }
        }

        // Pulses with the blink, and fades out as the camera gets close, so it never floods the screen.
        void Glow()
        {
            glow ??= new MaterialPropertyBlock();
            var cam = Camera.main;
            float distance = cam ? Vector3.Distance(cam.transform.position, aura.transform.position) : AuraFadeFrom;
            float strength = Mathf.InverseLerp(AuraFadeTo, AuraFadeFrom, distance) * (0.7f + 0.3f * Mathf.Cos(Time.time * Mathf.PI * 5f));
            glow.SetColor(BaseColor, auraColor * strength);
            aura.SetPropertyBlock(glow);
        }

        bool IsNearOwner()
        {
            var mine = GetComponentsInChildren<BoxCollider>(true);
            var theirs = owner.GetComponentsInChildren<BoxCollider>();
            float limit = snapDistance * snapDistance;
            foreach (var a in mine)
                foreach (var b in theirs)
                    if ((a.transform.position - b.transform.position).sqrMagnitude < limit) return true;
            return false;
        }

        void TrySnapBack()
        {
            // Every old spot has to be free; then whatever something on the robot can hold goes back on.
            foreach (var e in entries)
                foreach (var cell in owner.Footprint(e.definition, e.anchor, e.orientation))
                    if (owner.IsFilled(cell)) return;

            var left = owner.PlaceGroup(entries);
            if (left.Count == entries.Count) return;
            Juice.Play(Sound.PartBack);
            Juice.Flash(Center, 1.6f);
            entries.Clear();
            entries.AddRange(left);
            if (entries.Count == 0) Destroy(gameObject);
            else if (aura && !HasHead) Destroy(aura.gameObject);
        }
    }
}
