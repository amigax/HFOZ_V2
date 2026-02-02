#if ENABLE_ASTAR_PATHFINDING_PROJECT
using Pathfinding;
using Pathfinding.ECS;
using System;
using Unity.Collections;
using Unity.Entities;
using static Unity.Entities.SystemAPI;

namespace ProjectDawn.Navigation.Astar
{
    public unsafe struct SetupManagedState : IComponentData
    {
        public GraphMask graphMask;
        public int traversableTags;
        public fixed float tagCostMultipliers[32];
        public AstarLinkTraversalMode LinkTraversalMode;
    }

    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public unsafe partial struct AstarSetupSystem : ISystem
    {
        void ISystem.OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (setup, entity) in Query<SetupManagedState>().WithEntityAccess())
            {
                var managedSettings = new ManagedSettings();
                managedSettings.pathfindingSettings.graphMask = setup.graphMask;
                managedSettings.pathfindingSettings.traversableTags = setup.traversableTags;
                managedSettings.pathfindingSettings.tagCostMultipliers = new Span<float>(setup.tagCostMultipliers, 32).ToArray();
                ecb.AddComponent(entity, managedSettings);

                var managedState = new ManagedState();
                managedState.pathTracer = new PathTracer(Allocator.Persistent);
                ecb.AddComponent(entity, managedState);

                if (setup.LinkTraversalMode != AstarLinkTraversalMode.None)
                {
                    ecb.AddComponent<LinkTraversal>(entity);
                    ecb.SetComponentEnabled<LinkTraversal>(entity, false);
                }
                if (setup.LinkTraversalMode == AstarLinkTraversalMode.Seeking)
                    ecb.AddComponent<LinkTraversalSeek>(entity);
                if (setup.LinkTraversalMode == AstarLinkTraversalMode.StateMachine)
                    ecb.AddComponent(entity, new AstarLinkTraversalStateMachine { });

                ecb.RemoveComponent<SetupManagedState>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
#endif
