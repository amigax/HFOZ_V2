using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using ProjectDawn.Navigation;
using static Unity.Entities.SystemAPI;

namespace ProjectDawn.Navigation.Sample.Zerg
{
    [RequireMatchingQueriesForUpdate]
    public partial class UnitAnimationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (brain, unit, body, entity) in SystemAPI.Query<UnitBrain, UnitAnimator, AgentBody>().WithEntityAccess())
            {
                if (!ManagedAPI.HasComponent<Animator>(entity))
                    continue;

                var animator = ManagedAPI.GetComponent<Animator>(entity);

                animator.SetBool(unit.AttackId, brain.State == UnitBrainState.Attack);

                float speed = math.length(body.Velocity);

                animator.SetFloat(unit.MoveSpeedId, speed);
                animator.speed = speed > 0.3f ? speed * unit.MoveSpeed : 1f;
            }
        }
    }
}
