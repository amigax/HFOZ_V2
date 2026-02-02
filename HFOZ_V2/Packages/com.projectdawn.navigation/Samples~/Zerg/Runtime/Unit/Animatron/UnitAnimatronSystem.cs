#if MODULE_ANIMATRON
using Unity.Entities;
using Unity.Mathematics;
using ProjectDawn.Animation;

namespace ProjectDawn.Navigation.Sample.Zerg
{
    [RequireMatchingQueriesForUpdate]
    public partial class UnitAnimatronSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (brain, unit, body, entity) in SystemAPI.Query<UnitBrain, UnitAnimatron, AgentBody>().WithEntityAccess())
            {
                if (!SystemAPI.HasComponent<Animatron>(unit.Animatron))
                    continue;

                ref var animatron = ref SystemAPI.GetComponentRW<Animatron>(unit.Animatron).ValueRW;
                ref var inertializer = ref SystemAPI.GetComponentRW<Inertializer>(unit.Animatron).ValueRW;

                if (brain.State == UnitBrainState.Attack && animatron.AnimationIndex != unit.Attack)
                {
                    inertializer.Intertialize(unit.Attack);
                    animatron.Speed = 1.0f;

                }
                else
                {
                    float speed = math.length(body.Velocity);
                    if (animatron.AnimationIndex != unit.Move && speed > 0.5f)
                    {
                        inertializer.Intertialize(unit.Move);
                        animatron.Speed = speed > 0.3f ? speed : 1f;
                    }
                    else if (animatron.AnimationIndex != unit.Idle && speed < 0.5f)
                    {
                        inertializer.Intertialize(unit.Idle);
                        animatron.Speed = 1.0f;
                    }
                }
            }
        }
    }
}
#endif
