using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using static Unity.Entities.SystemAPI;
using UnityEngine;
using Unity.Jobs;
using ProjectDawn.Navigation.LowLevel.Unsafe;

namespace ProjectDawn.Navigation.Editor
{
    [DisableAutoCreation]
    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(AgentPathingSystemGroup))]
    [UpdateAfter(typeof(NavMeshBoundarySystem))]
    public partial struct AgentSpatilaPartitioningGizmosSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gizmos = GetSingletonRW<GizmosSystem.Singleton>();
            var spatial = GetSingletonRW<AgentSpatialPartitioningSystem.Singleton>();
            state.Dependency = new Job
            {
                Gizmos = gizmos.ValueRW.CreateCommandBuffer(),
                Spatial = spatial.ValueRW,
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        unsafe partial struct Job : IJob
        {
            public GizmosCommandBuffer Gizmos;
            public AgentSpatialPartitioningSystem.Singleton Spatial;

            public void Execute()
            {
#if AGENTS_NAVIGATION_AABB_TREE
                if (Spatial.Tree->IsEmpty)
                    return;

                var handles = new UnsafeStack<UnsafeBHVTree<AABB, int>.Handle>(1, Allocator.Temp);
                handles.Push(Spatial.Tree->Root);

                while (handles.TryPop(out var handle))
                {
                    var node = Spatial.Tree->GetNode(handle);

                    if (node.IsLeaf)
                    {
                        Gizmos.DrawWireBox((node.Volume.Min + node.Volume.Max) * 0.5f, (node.Volume.Max - node.Volume.Min), Color.green);
                    }
                    else
                    {
                        Gizmos.DrawWireBox((node.Volume.Min + node.Volume.Max) * 0.5f, (node.Volume.Max - node.Volume.Min), Color.white);

                        handles.Push(node.LeftChild);
                        handles.Push(node.RightChild);
                    }
                }
#endif
            }
        }
    }
}
