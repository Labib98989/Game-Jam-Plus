using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace CoreDump
{
    // One combat room's fight: 3-4 waves, each bought from a budget that grows room by room, so later
    // rooms bring more and tougher enemies. Every wave runs on a timer: when it runs out the next wave
    // comes whether or not you've finished this one, and clearing a wave early brings the next one
    // early. Enemies come out of the room's spawn spots after a warning flash, picked from the spots
    // near the robot when there are any.
    public class Waves
    {
        // What each enemy costs from a wave's budget. Skitters come in packs, one point each.
        const int SpitterCost = 3, BomberCost = 4, StalkerCost = 5, HornetCost = 7, BruteCost = 8;
        // The flash before an enemy appears.
        const float Warning = 0.8f;
        // The pause before the next wave when the last one was cleared early.
        const float Breather = 2f;
        // Spawn spots closer than this to the robot are picked first.
        const float NearRange = 60f;

        readonly EnemyKit kit;
        readonly RobotBody robot;
        readonly int roomIndex;
        readonly float waveTime;
        readonly List<RoomPiece> floorSpots, groundSpots, perches, airSpots;
        readonly HashSet<RoomPiece> reserved = new();
        readonly List<(float at, System.Action spawn)> pending = new();
        readonly System.Random random;
        float nextWaveAt;

        // The waves that have come in so far, out of Count.
        public int Wave { get; private set; }
        public int Count { get; }
        public float TimeToNext => Mathf.Max(0f, nextWaveAt - Time.time);
        public bool AllIn => Wave >= Count && pending.Count == 0;
        public bool Clear => AllIn && Enemy.AliveCount == 0;

        public Waves(RoomData room, int roomIndex, EnemyKit kit, RobotBody robot, float waveTime, float firstDelay, System.Random random)
        {
            this.kit = kit;
            this.robot = robot;
            this.roomIndex = roomIndex;
            this.waveTime = waveTime;
            this.random = random;
            Count = roomIndex < 2 ? 3 : 4;
            nextWaveAt = Time.time + firstDelay;
            // Skitters pour out of vents, and Brutes and Stalkers come in on the ground; either
            // stands in for the other if a room lacks it.
            var vents = room.All(RoomPieces.Vent).ToList();
            var grounds = room.All(RoomPieces.Ground).ToList();
            floorSpots = vents.Count > 0 ? vents : grounds;
            groundSpots = grounds.Count > 0 ? grounds : vents;
            perches = room.All(RoomPieces.Perch).ToList();
            airSpots = room.All(RoomPieces.Air).ToList();
        }

        public void Tick()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i].at) continue;
                var spawn = pending[i].spawn;
                pending.RemoveAt(i);
                spawn();
            }
            if (Wave >= Count) return;
            if (Wave > 0 && pending.Count == 0 && Enemy.AliveCount == 0) nextWaveAt = Mathf.Min(nextWaveAt, Time.time + Breather);
            if (Time.time >= nextWaveAt) StartWave();
        }

        void StartWave()
        {
            Wave++;
            Juice.Play(Sound.WaveStart);
            nextWaveAt = Time.time + waveTime;
            int room = roomIndex + 1;
            // A crowded room gets a smaller wave, so enemies don't pile up without end.
            int budget = Mathf.Max(6, 10 + 5 * roomIndex + 3 * (Wave - 1) - Enemy.AliveCount / 2);
            var choices = new List<(float weight, int cost, System.Action queue)>();
            while (budget > 0)
            {
                choices.Clear();
                choices.Add((2.5f, 0, null));
                if (budget >= SpitterCost && FreePerch() != null) choices.Add((1.5f, SpitterCost, QueueSpitter));
                if (room >= 3 && budget >= BruteCost && groundSpots.Count > 0) choices.Add((1f, BruteCost, () => QueueAt(PickSpot(groundSpots).Bottom, p => Brute.Spawn(kit, robot, p))));
                if (room >= 3 && budget >= BomberCost) choices.Add((1.2f, BomberCost, () => QueueAt(AirSpot(), p => Bomber.Spawn(kit, robot, p))));
                if (room >= 4 && budget >= StalkerCost && groundSpots.Count > 0) choices.Add((1f, StalkerCost, () => QueueAt(PickSpot(groundSpots).Bottom, p => Stalker.Spawn(kit, robot, p))));
                if (room >= 5 && budget >= HornetCost) choices.Add((0.9f, HornetCost, () => QueueAt(AirSpot(), p => Hornet.Spawn(kit, robot, p))));

                var pick = Pick(choices);
                if (pick.queue == null)
                {
                    int pack = Mathf.Min(budget, random.Next(4, 9));
                    QueuePack(pack);
                    budget -= pack;
                }
                else
                {
                    pick.queue();
                    budget -= pick.cost;
                }
            }
        }

        (float weight, int cost, System.Action queue) Pick(List<(float weight, int cost, System.Action queue)> choices)
        {
            float roll = (float)random.NextDouble() * choices.Sum(c => c.weight);
            foreach (var choice in choices)
            {
                roll -= choice.weight;
                if (roll <= 0f) return choice;
            }
            return choices[^1];
        }

        void QueuePack(int size)
        {
            var spot = FloorSpot();
            Flash(spot);
            for (int i = 0; i < size; i++)
            {
                var at = spot + new Vector3((float)random.NextDouble() * 3f - 1.5f, 0f, (float)random.NextDouble() * 3f - 1.5f);
                pending.Add((Time.time + Warning + i * 0.12f, () => Skitter.Spawn(kit, robot, at)));
            }
        }

        void QueueAt(Vector3 at, System.Action<Vector3> spawn)
        {
            Flash(at);
            pending.Add((Time.time + Warning, () => spawn(at)));
        }

        void QueueSpitter()
        {
            var perch = FreePerch();
            reserved.Add(perch);
            Flash(perch.Center);
            pending.Add((Time.time + Warning, () =>
            {
                reserved.Remove(perch);
                Spitter.Spawn(kit, robot, perch, perches);
            }));
        }

        RoomPiece FreePerch()
        {
            var free = perches.Where(p => !Spitter.IsTaken(p) && !reserved.Contains(p)).ToList();
            return free.Count == 0 ? null : PickSpot(free);
        }

        // One of the spots near the robot, or failing that, one of the three closest.
        RoomPiece PickSpot(List<RoomPiece> spots)
        {
            var at = robot.transform.position;
            var near = spots.Where(s => (s.Center - at).sqrMagnitude < NearRange * NearRange).ToList();
            if (near.Count == 0) near = spots.OrderBy(s => (s.Center - at).sqrMagnitude).Take(3).ToList();
            return near[random.Next(near.Count)];
        }

        // A spawn spot on the floor, or failing that, somewhere walkable near the robot.
        Vector3 FloorSpot()
        {
            if (floorSpots.Count > 0) return PickSpot(floorSpots).Bottom;
            var guess = robot.transform.position + new Vector3((float)random.NextDouble() * 40f - 20f, 0f, (float)random.NextDouble() * 40f - 20f);
            return NavMesh.SamplePosition(guess, out var hit, 20f, NavMesh.AllAreas) ? hit.position : guess;
        }

        // An air spawn spot, or high above a floor spot if the room has none.
        Vector3 AirSpot() => airSpots.Count > 0 ? PickSpot(airSpots).Center : FloorSpot() + Vector3.up * 12f;

        void Flash(Vector3 at)
        {
            FxGrow.Spawn(PrimitiveType.Sphere, at + Vector3.up * 0.6f, Vector3.one * 3f, Warning, kit.spawnFx);
            Juice.Play(Sound.Spawn, at);
        }
    }
}
