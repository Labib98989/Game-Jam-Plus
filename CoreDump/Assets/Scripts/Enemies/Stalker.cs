using UnityEngine;

namespace CoreDump
{
    // A fast loner that leaps between levels. Its pounce reaches a long way, up onto ledges too, and
    // it crouches first, so you can see it coming. A pounce that misses leaves it stuck for a moment:
    // that's the time to hit it.
    public class Stalker : Leaper
    {
        protected override Sound LeapSound => Sound.Screech;

        public static Stalker Spawn(EnemyKit kit, RobotBody robot, Vector3 position)
        {
            const float scale = 1.6f;
            bool onMesh = OnWalkingMap(ref position);
            var go = NewBody("Stalker", kit, position, new Vector3(0f, 1f, 0f) * scale, new Vector3(1.3f, 2f, 1.9f) * scale);
            var t = Model(go, scale);
            Box(t, new Vector3(0f, 1.1f, 0f), new Vector3(0.6f, 0.6f, 1.2f), kit.skin);
            Box(t, new Vector3(0f, 1.35f, 0.75f), new Vector3(0.45f, 0.4f, 0.45f), kit.skin);
            Box(t, new Vector3(-0.12f, 1.42f, 0.98f), new Vector3(0.12f, 0.08f, 0.06f), kit.eye);
            Box(t, new Vector3(0.12f, 1.42f, 0.98f), new Vector3(0.12f, 0.08f, 0.06f), kit.eye);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(t, new Vector3(side * 0.4f, 0.55f, 0.4f), new Vector3(0.12f, 1.1f, 0.12f), kit.skin);
                Box(t, new Vector3(side * 0.4f, 0.55f, -0.4f), new Vector3(0.12f, 1.1f, 0.12f), kit.skin);
                Box(t, new Vector3(side * 0.35f, 1.1f, 1f), new Vector3(0.08f, 0.08f, 0.9f), kit.armor);
            }

            var stalker = go.AddComponent<Stalker>();
            stalker.maxHealth = 14f;
            stalker.damage = 2;
            stalker.speed = 18f;
            stalker.leapRange = 20f;
            stalker.leapTime = 0.7f;
            stalker.windUp = 0.45f;
            stalker.leapCooldown = 1.4f;
            stalker.biteReach = 2.2f;
            stalker.leapDown = 10f;
            stalker.stuckOnMiss = 1.2f;
            stalker.Init(kit, robot);
            stalker.Begin(onMesh);
            return stalker;
        }
    }
}
