using ProjectDawn.ContinuumCrowds;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Scenes;
using Unity.Transforms;
using static Unity.Entities.SystemAPI;

namespace ProjectDawn.Navigation
{
    public class CrowdWorlds : IComponentData, System.IDisposable
    {
        public List<CrowdWorld> Worlds = new();
        public void Dispose()
        {
            foreach (var world in Worlds)
                world.Dispose();
            Worlds.Clear();
        }
    }

    public class CrowdFlows : IComponentData, System.IDisposable
    {
        public List<CrowdFlow> Flows = new();
        public void Dispose()
        {
            foreach (var flow in Flows)
                flow.Dispose();
            Flows.Clear();
        }
    }

    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SceneSystemGroup))]
    public partial struct CrowdSurfaceSystem : ISystem
    {
        void ISystem.OnCreate(ref Unity.Entities.SystemState state)
        {
            state.EntityManager.CreateSingleton(new CrowdWorlds());
            state.EntityManager.CreateSingleton(new CrowdFlows());
            state.RequireForUpdate<CrowdWorlds>();
            state.RequireForUpdate<CrowdFlows>();
        }

        void ISystem.OnUpdate(ref Unity.Entities.SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var worlds = ManagedAPI.GetSingleton<CrowdWorlds>();
            foreach (var (surface, transform, entity) in
                Query<CrowdSurface, LocalTransform>().WithNone<CrowdSurfaceWorld>().WithEntityAccess())
            {
                float3 scale = new float3(surface.Size.xy / new float2(surface.Width, surface.Height), 1);

                var world = new CrowdWorld(surface.Width, surface.Height, NonUniformTransform.FromPositionRotationScale(transform.Position, transform.Rotation, scale), Allocator.Persistent);
                ecb.AddComponent(entity, new CrowdSurfaceWorld { World = world });
                worlds.Worlds.Add(world);

                var data = state.EntityManager.HasComponent<CrowdSurfaceData>(entity) ?
                    state.EntityManager.GetSharedComponentManaged<CrowdSurfaceData>(entity) :
                    default;
                if (data.Data != null)
                {
                    world.SetHeightField(data.Data.HeightField);
                    world.SetObstacleField(data.Data.ObstacleField);
                    world.RecalculateHeightGradientField();
                }
            }
            foreach (var (world, entity) in
                Query<CrowdSurfaceWorld>().WithNone<CrowdSurface>().WithEntityAccess())
            {
                world.World.Dispose();
                worlds.Worlds.Remove(world.World);
                ecb.RemoveComponent<CrowdSurfaceWorld>(entity);
            }

            var flows = ManagedAPI.GetSingleton<CrowdFlows>();
            foreach (var (group, entity) in
                Query<CrowdGroup>().WithNone<CrowdGroupFlow>().WithEntityAccess())
            {
                if (!state.EntityManager.HasComponent<CrowdSurfaceWorld>(group.Surface))
                    continue;

                var surface = GetComponent<CrowdSurfaceWorld>(group.Surface);

                if (!surface.World.IsCreated)
                    throw new System.InvalidOperationException("CrowdGroup surface has to be created!");

                var layer = new CrowdFlow(surface.World.Width, surface.World.Height, surface.World.Transform, Allocator.Persistent);
                ecb.AddComponent(entity, new CrowdGroupFlow { Flow = layer });
                flows.Flows.Add(layer);
            }
            foreach (var (flow, entity) in
                Query<CrowdGroupFlow>().WithNone<CrowdGroup>().WithEntityAccess())
            {
                flow.Flow.Dispose();
                flows.Flows.Remove(flow.Flow);
                ecb.RemoveComponent<CrowdGroupFlow>(entity);
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
