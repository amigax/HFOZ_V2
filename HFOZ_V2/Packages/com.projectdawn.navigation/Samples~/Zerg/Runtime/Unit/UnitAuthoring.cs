using Unity.Entities;
using UnityEngine;
using ProjectDawn.Navigation.Hybrid;
using Unity.Transforms;

#if MODULE_ANIMATRON
using ProjectDawn.Animation;
#endif

namespace ProjectDawn.Navigation.Sample.Zerg
{
    public class UnitAuthoring : MonoBehaviour
    {
        public PlayerId Owner;
        public Animator Animator;
#if MODULE_ANIMATRON
        public AnimatronAuthoring Animatron;
#endif
        public float MoveAnimationSpeed = 0.4f;
        public float Life = 100;

        Entity m_Entity;

        void Awake()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            m_Entity = GetComponent<AgentAuthoring>().GetOrCreateEntity();
            world.EntityManager.AddComponentData(m_Entity, new Unit
            {
                Owner = Owner,
            });
            world.EntityManager.AddComponentData(m_Entity, new UnitAnimator
            {
                AttackId = Animator.StringToHash("Attack"),
                MoveSpeed = MoveAnimationSpeed,
                MoveSpeedId = Animator.StringToHash("Speed"),
            });
            world.EntityManager.AddComponentData(m_Entity, new UnitBrain
            {
                State = UnitBrainState.Idle,
            });
            world.EntityManager.AddComponentData(m_Entity, new UnitLife
            {
                Life = Life,
                MaxLife = Life,
            });

            if (Animator)
                world.EntityManager.AddComponentObject(m_Entity, Animator);
#if MODULE_ANIMATRON
            if (Animatron)
            {
                world.EntityManager.AddComponentData(m_Entity, new UnitAnimatron
                {
                    Attack = Animatron.FindAnimationIndex("Attack"),
                    Move = Animatron.FindAnimationIndex("Move"),
                    Idle = Animatron.FindAnimationIndex("Idle"),
                    Animatron = Animatron.GetOrCreateEntity(),
                });
                //world.EntityManager.AddComponentData(Animatron.GetOrCreateEntity(), new Parent { Value = m_Entity });
            }
#endif
        }

        void OnDestroy()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null)
            {
                world.EntityManager.RemoveComponent<Unit>(m_Entity);
                world.EntityManager.RemoveComponent<UnitAnimator>(m_Entity);
                world.EntityManager.RemoveComponent<UnitBrain>(m_Entity);
                world.EntityManager.RemoveComponent<UnitLife>(m_Entity);
            }
        }
    }
}
