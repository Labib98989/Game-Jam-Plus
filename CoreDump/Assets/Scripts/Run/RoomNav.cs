using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CoreDump
{
    // The walking map for ground enemies, baked from a room's colliders when the room loads. Baking
    // freezes the game for a moment, which the fade between rooms hides.
    public static class RoomNav
    {
        static NavMeshDataInstance current;

        public static void Bake(Transform room)
        {
            Clear();
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(room, ~(1 << RoomBuilder.PadLayer), NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var colliders = room.GetComponentsInChildren<Collider>();
            if (sources.Count == 0 || colliders.Length == 0) return;

            // Sized for Skitters and Stalkers. Brutes are bigger and just brush past corners.
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.8f;
            settings.agentHeight = 2f;
            settings.agentClimb = 0.6f;
            settings.agentSlope = 40f;
            // Coarser than the default, so big rooms bake quickly.
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.3f;
            var bounds = colliders[0].bounds;
            foreach (var c in colliders) bounds.Encapsulate(c.bounds);
            bounds.Expand(2f);
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data) current = NavMesh.AddNavMeshData(data);
        }

        public static void Clear()
        {
            if (current.valid) NavMesh.RemoveNavMeshData(current);
            current = default;
        }
    }
}
