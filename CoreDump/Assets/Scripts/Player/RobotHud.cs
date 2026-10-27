using System.Collections.Generic;
using UnityEngine;

namespace CoreDump
{
    // The in-game HUD: crosshair, health bar, and arrows pointing to knocked-off parts, plus a hit
    // marker on the crosshair and red screen edges when the robot is hurt or low. When the head is
    // knocked off, the HUD breaks loose: every piece bounces around the screen like a loose part
    // until the head is back on, then flies back into place. Aiming still uses the screen center.
    [RequireComponent(typeof(RobotBody))]
    public class RobotHud : MonoBehaviour
    {
        [Tooltip("Pull on loose HUD pieces, in pixels per second squared.")]
        public float gravity = 1400f;
        [Tooltip("How much speed a loose piece keeps when it bounces off a screen edge.")]
        public float bounce = 0.8f;
        [Tooltip("How quickly pieces fly back into place once the head is back on.")]
        public float returnSpeed = 10f;

        class Piece
        {
            public Vector2 offset, velocity;
            public float angle, spin;
        }

        const float ArrowSize = 28f;
        const float EdgeMargin = 36f;
        // At this much hull or less, the screen's edges pulse red.
        const int LowHull = 3;

        readonly Piece crosshair = new(), healthBar = new();
        readonly Dictionary<PartChunk, Piece> arrows = new();
        readonly List<PartChunk> gone = new();
        RobotBody robot;
        RobotHealth health;
        BuildMode build;
        bool loose;
        Texture2D arrowTexture, vignette;

        bool Building => build && build.IsOpen;
        bool HeadLost => !robot.Head && !Building;

        void Awake()
        {
            robot = GetComponent<RobotBody>();
            health = GetComponent<RobotHealth>();
            build = GetComponent<BuildMode>();
        }

        void Update()
        {
            if (HeadLost && !loose)
            {
                loose = true;
                Kick(crosshair);
                Kick(healthBar);
                foreach (var piece in arrows.Values) Kick(piece);
            }
            else if (!HeadLost) loose = false;

            float dt = Time.unscaledDeltaTime;
            Step(crosshair, CrosshairRect, dt);
            Step(healthBar, HealthRect, dt);

            gone.Clear();
            foreach (var chunk in arrows.Keys)
                if (!chunk) gone.Add(chunk);
            foreach (var chunk in gone) arrows.Remove(chunk);
            foreach (var chunk in PartChunk.Live)
            {
                if (!chunk) continue;
                if (!arrows.TryGetValue(chunk, out var piece))
                {
                    arrows[chunk] = piece = new Piece();
                    if (loose) Kick(piece);
                }
                if (TryArrow(chunk, out var rect, out _)) Step(piece, rect, dt);
            }
        }

        void Kick(Piece piece)
        {
            piece.velocity = new Vector2(Random.Range(-900f, 900f), Random.Range(-1100f, -500f));
            piece.spin = Random.Range(-360f, 360f);
        }

        void Step(Piece piece, Rect home, float dt)
        {
            if (!loose)
            {
                float t = 1f - Mathf.Exp(-returnSpeed * dt);
                piece.offset = Vector2.Lerp(piece.offset, Vector2.zero, t);
                piece.angle = Mathf.LerpAngle(piece.angle, 0f, t);
                piece.velocity = Vector2.zero;
                return;
            }

            piece.velocity.y += gravity * dt;
            piece.offset += piece.velocity * dt;
            piece.angle += piece.spin * dt;
            var r = new Rect(home.position + piece.offset, home.size);
            if (r.xMin < 0f)
            {
                piece.offset.x -= r.xMin;
                piece.velocity.x = Mathf.Abs(piece.velocity.x) * bounce;
            }
            else if (r.xMax > Screen.width)
            {
                piece.offset.x -= r.xMax - Screen.width;
                piece.velocity.x = -Mathf.Abs(piece.velocity.x) * bounce;
            }
            if (r.yMin < 0f)
            {
                piece.offset.y -= r.yMin;
                piece.velocity.y = Mathf.Abs(piece.velocity.y) * bounce;
            }
            else if (r.yMax > Screen.height)
            {
                piece.offset.y -= r.yMax - Screen.height;
                piece.velocity.y = -Mathf.Abs(piece.velocity.y) * bounce;
                // Never let a piece settle: a slow bounce off the bottom gets a fresh kick.
                if (piece.velocity.y > -400f) Kick(piece);
            }
        }

        static Rect CrosshairRect => new(Screen.width * 0.5f - 12f, Screen.height * 0.5f - 12f, 24f, 24f);
        Rect HealthRect => new(16f, Screen.height - 54f, health ? health.maxHealth * 38f : 190f, 40f);

        // Where the arrow for a knocked-off part goes: just above it when it's on screen, otherwise
        // on the screen edge, pointing toward it. `angle` turns an up-pointing arrow to face it.
        bool TryArrow(PartChunk chunk, out Rect rect, out float angle)
        {
            rect = default;
            angle = 180f;
            var cam = Camera.main;
            if (!cam) return false;

            var screen = cam.WorldToScreenPoint(chunk.Center + Vector3.up * 1.2f);
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var point = new Vector2(screen.x, Screen.height - screen.y);
            bool behind = screen.z < 0f;
            if (behind) point = center - (point - center);

            bool onScreen = !behind &&
                point.x > EdgeMargin && point.x < Screen.width - EdgeMargin &&
                point.y > EdgeMargin && point.y < Screen.height - EdgeMargin;
            if (!onScreen)
            {
                var direction = (point - center).normalized;
                if (direction == Vector2.zero) direction = Vector2.down;
                float reach = Mathf.Min(
                    Mathf.Abs(direction.x) > 0.001f ? (center.x - EdgeMargin) / Mathf.Abs(direction.x) : float.MaxValue,
                    Mathf.Abs(direction.y) > 0.001f ? (center.y - EdgeMargin) / Mathf.Abs(direction.y) : float.MaxValue);
                point = center + direction * reach;
                angle = Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg;
            }
            rect = new Rect(point.x - ArrowSize * 0.5f, point.y - ArrowSize * 0.5f, ArrowSize, ArrowSize);
            return true;
        }

        void OnGUI()
        {
            if (!Building) DrawDamage();
            Draw(healthBar, HealthRect, DrawHealth);
            if (Building) return;
            if (robot.aim && !robot.aim.Suspended)
            {
                Draw(crosshair, CrosshairRect, DrawCrosshair);
                Draw(crosshair, CrosshairRect, DrawHitMarker);
            }
            DrawThermalMarks();

            if (!arrowTexture) arrowTexture = MakeArrowTexture();
            foreach (var chunk in PartChunk.Live)
            {
                if (!chunk || !arrows.TryGetValue(chunk, out var piece) || !TryArrow(chunk, out var rect, out float angle)) continue;
                bool blink = chunk.HasHead && Mathf.Repeat(Time.unscaledTime, 0.4f) < 0.2f;
                GUI.color = chunk.HasHead ? (blink ? Color.white : new Color(1f, 0.35f, 0.25f)) : new Color(1f, 0.6f, 0.15f);
                Draw(piece, rect, r =>
                {
                    var matrix = GUI.matrix;
                    GUIUtility.RotateAroundPivot(angle, r.center);
                    GUI.DrawTexture(r, arrowTexture);
                    GUI.matrix = matrix;
                });
                GUI.color = Color.white;
            }
        }

        // With Thermal Eye mods on the head, every enemy in range gets a mark, even through walls.
        void DrawThermalMarks()
        {
            var head = robot.Head;
            float range = head ? head.ModBonus(ModType.ThermalEye) : 0f;
            var cam = Camera.main;
            if (range <= 0f || !cam) return;
            GUI.color = new Color(1f, 0.45f, 0.2f, 0.9f);
            foreach (var target in Target.All)
            {
                if (!target.IsAlive || (target.AimPoint - head.transform.position).sqrMagnitude > range * range) continue;
                var screen = cam.WorldToScreenPoint(target.AimPoint);
                if (screen.z <= 0f) continue;
                var at = new Vector2(screen.x, Screen.height - screen.y);
                var matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, at);
                GUI.DrawTexture(new Rect(at.x - 7f, at.y - 7f, 14f, 14f), Texture2D.whiteTexture);
                GUI.matrix = matrix;
            }
            GUI.color = Color.white;
        }

        // Draws a piece where it is now, turned by its spin.
        static void Draw(Piece piece, Rect home, System.Action<Rect> draw)
        {
            var rect = new Rect(home.position + piece.offset, home.size);
            var matrix = GUI.matrix;
            if (piece.angle != 0f) GUIUtility.RotateAroundPivot(piece.angle, rect.center);
            draw(rect);
            GUI.matrix = matrix;
        }

        static void DrawCrosshair(Rect r)
        {
            var c = r.center;
            GUI.DrawTexture(new Rect(c.x - 1f, c.y - 7f, 2f, 5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(c.x - 1f, c.y + 2f, 2f, 5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(c.x - 7f, c.y - 1f, 5f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(c.x + 2f, c.y - 1f, 5f, 2f), Texture2D.whiteTexture);
        }

        void DrawHealth(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y, 200f, 20f), "HULL");
            int max = health ? health.maxHealth : 5;
            int current = health ? health.Health : max;
            for (int i = 0; i < max; i++)
            {
                GUI.color = i < current ? new Color(1f, 0.6f, 0.15f) : new Color(0.2f, 0.2f, 0.22f, 0.8f);
                GUI.DrawTexture(new Rect(r.x + i * 38f, r.y + 22f, 34f, 14f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        // Red at the screen's edges: a flash when the robot is hurt, and a slow pulse while the hull is low.
        void DrawDamage()
        {
            if (Event.current.type != EventType.Repaint) return;
            float flash = Mathf.Clamp01(1f - (Time.unscaledTime - Juice.HurtAt) / 0.45f) * (0.35f + 0.5f * Juice.HurtStrength);
            bool low = health && !health.IsDead && health.Health <= LowHull;
            float pulse = low ? 0.18f + 0.14f * Mathf.Sin(Time.unscaledTime * 5f) : 0f;
            float strength = Mathf.Max(flash, pulse);
            if (strength <= 0.01f) return;
            if (!vignette) vignette = MakeVignette();
            GUI.color = new Color(0.9f, 0.05f, 0.02f, strength);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), vignette);
            GUI.color = Color.white;
        }

        // Four ticks around the crosshair when a shot lands; bigger and red for a kill.
        static void DrawHitMarker(Rect r)
        {
            float since = Time.unscaledTime - Juice.HitAt;
            if (since > 0.14f) return;
            bool kill = Juice.HitAt - Juice.KillAt < 0.001f;
            float gap = kill ? 9f : 7f, length = kill ? 9f : 6f;
            GUI.color = kill ? new Color(1f, 0.3f, 0.2f) : Color.white;
            var c = r.center;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, c);
            GUI.DrawTexture(new Rect(c.x - 1f, c.y - gap - length, 2f, length), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(c.x - 1f, c.y + gap, 2f, length), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(c.x - gap - length, c.y - 1f, length, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(c.x + gap, c.y - 1f, length, 2f), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = Color.white;
        }

        // Clear in the middle, fading in toward the edges.
        static Texture2D MakeVignette()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x, y) / (size - 1) * 2f - Vector2.one;
                    float edge = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) * 0.6f + p.magnitude * 0.4f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.05f, edge))));
                }
            texture.Apply();
            return texture;
        }

        // A white triangle pointing up.
        static Texture2D MakeArrowTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Texture rows run bottom to top, so the tip is at the top row.
                    float halfWidth = (size - 1 - y) * 0.5f;
                    bool inside = Mathf.Abs(x - (size - 1) * 0.5f) <= halfWidth;
                    texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            texture.Apply();
            return texture;
        }
    }
}
