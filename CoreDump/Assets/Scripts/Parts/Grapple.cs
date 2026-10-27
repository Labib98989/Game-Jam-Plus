using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoreDump
{
    // Hooks the surface nearest the crosshair, within a wide cone, and pulls you there while you
    // hold the button. A marker shows where it would hook before you fire. Aimed at a knocked-off
    // part, it reels the part back to you instead. Does no damage.
    public class Grapple : PartModule
    {
        bool hooked;
        // Hooked onto a knocked-off part rather than a surface.
        bool catching;
        Vector3 hookPoint;
        PartChunk caught;
        Transform marker;
        LineRenderer rope;

        GrappleDefinition Tuning => (GrappleDefinition)Definition;
        Vector3 RobotCenter => Robot.transform.TransformPoint(Robot.LocalBounds.center);

        protected override void OnFitted()
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            marker.name = "Grapple Marker";
            Destroy(marker.GetComponent<Collider>());
            marker.localScale = Vector3.one * 0.5f;
            var markerRenderer = marker.GetComponent<Renderer>();
            markerRenderer.sharedMaterial = Tuning.markerMaterial;
            markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
            marker.gameObject.SetActive(false);

            rope = gameObject.AddComponent<LineRenderer>();
            rope.positionCount = 2;
            rope.startWidth = rope.endWidth = 0.08f;
            rope.sharedMaterial = Tuning.ropeMaterial;
            rope.shadowCastingMode = ShadowCastingMode.Off;
            rope.enabled = false;
        }

        void OnDestroy()
        {
            if (marker) Destroy(marker.gameObject);
        }

        public override void OnButtonDown()
        {
            hooked = FindHook(out hookPoint, out caught);
            catching = caught;
            Juice.Play(Sound.GrappleFire);
            if (hooked) Juice.Play(Sound.GrappleHook, hookPoint);
        }

        public override void OnButtonUp() => hooked = false;

        void FixedUpdate()
        {
            if (!hooked) return;

            if (catching)
            {
                // A caught part that snapped back on is gone, and the hook ends with it.
                if (!caught)
                {
                    hooked = false;
                    return;
                }
                caught.Body.linearVelocity = (RobotCenter - caught.Center).normalized * Tuning.catchSpeed;
                hookPoint = caught.Center;
                return;
            }

            var toHook = hookPoint - RobotCenter;
            if (toHook.magnitude < Tuning.releaseDistance)
            {
                hooked = false;
                return;
            }
            // Grapples on the same button stack: each pulls with its share of the stack's total.
            int count = Robot.PartsOn(Button).Count(p => p is Grapple);
            float pull = Stacking.Total(Tuning.pull, count, Tuning.stackFalloff, Tuning.maxPull) / count * Boost(ModType.Power);
            var direction = toHook.normalized;
            if (Vector3.Dot(Robot.Motor.Velocity, direction) < Tuning.maxSpeed) Robot.Motor.Thrust(direction * pull);
        }

        void LateUpdate()
        {
            rope.enabled = hooked;
            if (hooked)
            {
                rope.SetPosition(0, Nozzle);
                rope.SetPosition(1, hookPoint);
                marker.position = hookPoint;
                marker.gameObject.SetActive(true);
                return;
            }

            // Show where it would hook, while you're actually playing.
            var point = Vector3.zero;
            bool show = Robot.aim && Robot.aim.HasControl && FindHook(out point, out _);
            if (show) marker.position = point;
            marker.gameObject.SetActive(show);
        }

        // The knocked-off part or surface nearest the crosshair within the cone, if any.
        bool FindHook(out Vector3 point, out PartChunk chunk)
        {
            point = default;
            chunk = null;
            var cam = Camera.main;
            if (!cam) return false;
            var origin = cam.transform.position;
            var forward = cam.transform.forward;

            // Knocked-off parts come first: catching them is the point.
            float best = Tuning.coneAngle;
            foreach (var c in PartChunk.Live)
            {
                if (!c) continue;
                var to = c.Center - origin;
                float angle = Vector3.Angle(forward, to);
                if (angle > best || to.magnitude > Tuning.range) continue;
                best = angle;
                chunk = c;
                point = c.Center;
            }
            if (chunk) return true;

            // Straight at the crosshair first, then rings of rays out to the edge of the cone.
            if (Physics.Raycast(origin, forward, out var hit, Tuning.range, Robot.shotMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }
            var right = cam.transform.right;
            for (int ring = 1; ring <= 3; ring++)
            {
                float angle = Tuning.coneAngle * ring / 3f;
                for (int i = 0; i < 12; i++)
                {
                    var axis = Quaternion.AngleAxis(i * 30f, forward) * right;
                    var direction = Quaternion.AngleAxis(angle, axis) * forward;
                    if (Physics.Raycast(origin, direction, out hit, Tuning.range, Robot.shotMask, QueryTriggerInteraction.Ignore))
                    {
                        point = hit.point;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
