using System.Collections.Generic;
using UnityEngine;

namespace CoreDump
{
    // Fast, and in packs. It runs at the robot along the floor and leaps at it from close range,
    // biting whatever part is nearest. A knocked-off part left lying a few seconds gets grabbed by
    // the nearest Skitter, which runs off with it, slowed down, the part hovering over its back.
    // Touch the part to take it back, or kill the Skitter and the part drops.
    public class Skitter : Leaper
    {
        [Tooltip("How far away it notices a knocked-off part to steal.")]
        public float stealRange = 20f;
        [Tooltip("Its speed while carrying a part, as a share of its normal speed, so the robot can catch it.")]
        public float carrySpeed = 0.55f;

        // How high a stolen part floats over the Skitter's feet, in meters.
        const float CarryHeight = 2.6f;

        // Knocked-off parts a Skitter is already going for.
        static readonly HashSet<PartChunk> claimed = new();

        PartChunk chunk;

        public static Skitter Spawn(EnemyKit kit, RobotBody robot, Vector3 position)
        {
            const float scale = 1.7f;
            bool onMesh = OnWalkingMap(ref position);
            var go = NewBody("Skitter", kit, position, new Vector3(0f, 0.45f, 0f) * scale, new Vector3(1.3f, 0.9f, 1.5f) * scale);
            var t = Model(go, scale);
            Box(t, new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 0.4f, 1f), kit.skin);
            Box(t, new Vector3(0f, 0.5f, 0.55f), new Vector3(0.5f, 0.35f, 0.35f), kit.skin);
            Box(t, new Vector3(-0.14f, 0.58f, 0.73f), Vector3.one * 0.1f, kit.eye);
            Box(t, new Vector3(0.14f, 0.58f, 0.73f), Vector3.one * 0.1f, kit.eye);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 3; i++)
                    Box(t, new Vector3(side * 0.5f, 0.2f, -0.3f + i * 0.3f), new Vector3(0.12f, 0.4f, 0.1f), kit.skin);

            var skitter = go.AddComponent<Skitter>();
            skitter.maxHealth = 4f;
            skitter.Init(kit, robot);
            skitter.Begin(onMesh);
            return skitter;
        }

        protected override void Run()
        {
            if (TryClaim()) return;
            base.Run();
        }

        // A knocked-off part nearby that no other Skitter is after.
        bool TryClaim()
        {
            claimed.RemoveWhere(c => !c);
            foreach (var c in PartChunk.Live)
            {
                if (!c || c.HasHead || !c.Stealable || claimed.Contains(c)) continue;
                if ((c.Center - transform.position).sqrMagnitude > stealRange * stealRange) continue;
                chunk = c;
                claimed.Add(c);
                mode = Mode.Busy;
                nextPath = 0f;
                return true;
            }
            return false;
        }

        // Going for a part, or running off with it.
        protected override void Busy()
        {
            if (!chunk)
            {
                Release();
                mode = Mode.Run;
                return;
            }
            if (chunk.Carried)
            {
                if (chunk.Carrier == transform) Carry();
                else
                {
                    Release();
                    mode = Mode.Run;
                }
                return;
            }
            if ((chunk.Center - transform.position).sqrMagnitude < 3f * 3f)
            {
                chunk.PickUp(transform, CarryHeight);
                Juice.Play(Sound.Chitter, transform.position);
                if (agent) agent.speed = speed * carrySpeed;
                nextPath = 0f;
                return;
            }
            if (OnMesh) RunTo(chunk.Center, 0.3f);
            else transform.position = Vector3.MoveTowards(transform.position, chunk.Center, speed * Time.deltaTime);
        }

        // Runs off with the part, away from the robot.
        void Carry()
        {
            if (!OnMesh || Time.time < nextPath) return;
            nextPath = Time.time + 0.8f;
            var away = transform.position - robot.transform.position;
            away.y = 0f;
            var goal = transform.position + away.normalized * 14f + Random.insideUnitSphere * 5f;
            if (UnityEngine.AI.NavMesh.SamplePosition(goal, out var nav, 8f, UnityEngine.AI.NavMesh.AllAreas)) agent.SetDestination(nav.position);
        }

        // Lets go of the part it was after, or carrying, and gets its speed back.
        void Release()
        {
            if (chunk) claimed.Remove(chunk);
            chunk = null;
            if (agent) agent.speed = speed;
        }

        protected override void Die()
        {
            if (chunk && chunk.Carried && chunk.Carrier == transform) chunk.Drop();
            Release();
            base.Die();
        }

        void OnDestroy() => Release();
    }
}
