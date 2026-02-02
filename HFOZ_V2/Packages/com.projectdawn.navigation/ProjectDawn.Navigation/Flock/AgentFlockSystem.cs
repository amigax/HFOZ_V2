using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

namespace ProjectDawn.Navigation
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(AgentForceSystemGroup))]
    [UpdateAfter(typeof(FlockGroupSystem))]
    public partial struct AgentFlockSystem : ISystem
    {
        void ISystem.OnUpdate(ref SystemState state)
        {
            new AgentFlockJob
            {
                BodyFromEntity = SystemAPI.GetComponentLookup<AgentBody>(isReadOnly: false),
                TranslationFromEntity = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true),
            }.Schedule();
        }

        partial struct AgentFlockJob : IJobEntity
        {
            public ComponentLookup<AgentBody> BodyFromEntity;
            [ReadOnly]
            public ComponentLookup<LocalTransform> TranslationFromEntity;
            public void Execute(in FlockGroup group, in DynamicBuffer<FlockEntity> flock)
            {
                for (int index = 0; index < flock.Length; ++index)
                {
                    var entity = flock[index].Value;
                    if (!BodyFromEntity.TryGetComponent(entity, out AgentBody flockBody))
                        continue;
                    if (!TranslationFromEntity.TryGetComponent(entity, out LocalTransform flockTransform))
                        continue;

                    float3 cohesionDirection = math.normalizesafe(group.AveragePositions - flockTransform.Position);
                    float3 alignmentDirection = group.AverageDirection;

                    float weight = 1 - (group.Cohesion + group.Alignment);
                    float3 direction = flockBody.Force * weight + cohesionDirection * group.Cohesion + alignmentDirection * group.Alignment;
                    flockBody.Force = math.normalizesafe(direction);
                    BodyFromEntity[entity] = flockBody;
                }
            }
        }
    }
}
