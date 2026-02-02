using Unity.Entities;
using UnityEngine;
using ProjectDawn.Navigation.Hybrid;
using ProjectDawn.Entities;
using Unity.Collections;
using Unity.Mathematics;



#if ENABLE_ASTAR_PATHFINDING_PROJECT
using Pathfinding.ECS;
using Pathfinding;
#endif

namespace ProjectDawn.Navigation.Astar
{
    /// <summary>
    /// Agent uses NavMesh for pathfinding.
    /// </summary>
    [RequireComponent(typeof(AgentAuthoring))]
    [AddComponentMenu("Agents Navigation/Agent Astar Pathing")]
    [DisallowMultipleComponent]
    [HelpURL("https://lukaschod.github.io/agents-navigation-docs/manual/game-objects/pathing/astar.html")]
    public class AgentAstarPathingAuthoring : MonoBehaviour, INavMeshWallProvider
    {
#if ENABLE_ASTAR_PATHFINDING_PROJECT
        [SerializeField]
        AstarLinkTraversalMode m_LinkTraversalMode = AstarLinkTraversalMode.StateMachine;

        [SerializeField]
        AgentAstarPath m_Path = AgentAstarPath.Default;

        [SerializeField]
        ManagedSettings m_ManagedSettings = new()
        {
            pathfindingSettings = PathRequestSettings.Default,
        };

        ManagedState m_ManagedState;

        Entity m_Entity;

        /// <summary>
        /// Returns default component of <see cref="AgentAstarPath"/>.
        /// </summary>
        public AgentAstarPath DefaultPath => m_Path;

        /// <summary>
        /// Returns default component of <see cref="Pathfinding.ECS.MovementState"/>.
        /// </summary>
        public MovementState DefaultMovementState => new(transform.position);

        /// <summary>
        /// <see cref="Pathfinding.ECS.ManagedSettings"/> component of this <see cref="ManagedSettings"/> Entity.
        /// </summary>
        public ManagedSettings ManagedSettings => m_ManagedSettings;

        /// <summary>
        /// <see cref="Pathfinding.ECS.ManagedState"/> component of this <see cref="AgentAuthoring"/> Entity.
        /// </summary>
        public ManagedState ManagedState => m_ManagedState;

        /// <summary>
        /// <see cref="AgentAstarPath"/> component of this <see cref="AgentAuthoring"/> Entity.
        /// Accessing this property is potentially heavy operation as it will require wait for agent jobs to finish.
        /// </summary>
        public ref AgentAstarPath Path
        {
            get
            {
                if (World.DefaultGameObjectInjectionWorld == null)
                    return ref m_Path;

                return ref World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentDataRW<AgentAstarPath>(m_Entity).ValueRW;
            }
        }

        /// <summary>
        /// <see cref="Pathfinding.ECS.MovementState"/> component of this <see cref="AgentAuthoring"/> Entity.
        /// Accessing this property is potentially heavy operation as it will require wait for agent jobs to finish.
        /// </summary>
        public ref MovementState MovementState => ref World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentDataRW<MovementState>(m_Entity).ValueRW;

        void Awake()
        {
            m_ManagedState = new ManagedState
            {
                pathTracer = new PathTracer(Allocator.Persistent),
            };

            var world = World.DefaultGameObjectInjectionWorld;
            m_Entity = GetComponent<AgentAuthoring>().GetOrCreateEntity();
            world.EntityManager.AddComponentData(m_Entity, m_Path);
            world.EntityManager.AddComponentData(m_Entity, m_ManagedState);
            world.EntityManager.AddComponentData(m_Entity, m_ManagedSettings);
            world.EntityManager.AddComponentData(m_Entity, DefaultMovementState);

            if (m_LinkTraversalMode != AstarLinkTraversalMode.None)
            {
                world.EntityManager.AddComponent<LinkTraversal>(m_Entity);
                world.EntityManager.SetComponentEnabled<LinkTraversal>(m_Entity, false);
            }
            if (m_LinkTraversalMode == AstarLinkTraversalMode.Seeking)
                world.EntityManager.AddComponent<LinkTraversalSeek>(m_Entity);
            if (m_LinkTraversalMode == AstarLinkTraversalMode.StateMachine)
                world.EntityManager.AddComponentData(m_Entity, new AstarLinkTraversalStateMachine{});

            // Sync in case it was created as disabled
            if (!enabled)
                world.EntityManager.SetComponentEnabled<AgentAstarPath>(m_Entity, false);
        }

        void OnDestroy()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null)
            {
                world.EntityManager.RemoveComponent<AgentAstarPath>(m_Entity);
                world.EntityManager.RemoveComponent<ManagedState>(m_Entity);
                world.EntityManager.RemoveComponent<MovementState>(m_Entity);
                if (m_LinkTraversalMode != AstarLinkTraversalMode.None)
                    world.EntityManager.RemoveComponent<LinkTraversal>(m_Entity);
                if (m_LinkTraversalMode == AstarLinkTraversalMode.Seeking)
                    world.EntityManager.RemoveComponent<LinkTraversalSeek>(m_Entity);
                if (m_LinkTraversalMode == AstarLinkTraversalMode.StateMachine)
                    world.EntityManager.RemoveComponent<AstarLinkTraversalStateMachine>(m_Entity);
            }
            if (m_ManagedState != null)
            {
                m_ManagedState.Dispose();
                m_ManagedState = null;
            }
        }

        void OnEnable()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
                return;
            world.EntityManager.SetComponentEnabled<AgentAstarPath>(m_Entity, true);
        }

        void OnDisable()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
                return;
            world.EntityManager.SetComponentEnabled<AgentAstarPath>(m_Entity, false);
        }

        class AgentAstarPathingBaker : Baker<AgentAstarPathingAuthoring>
        {
            public override void Bake(AgentAstarPathingAuthoring authoring)
            {
                var state = new SetupManagedState
                {
                    graphMask = authoring.ManagedSettings.pathfindingSettings.graphMask,
                    traversableTags = authoring.ManagedSettings.pathfindingSettings.traversableTags,
                    LinkTraversalMode = authoring.m_LinkTraversalMode
                };

                var source = authoring.ManagedSettings.pathfindingSettings.tagCostMultipliers;
                int length = math.min(source.Length, 32);

                unsafe
                {
                    for (int i = 0; i < length; i++)
                        state.tagCostMultipliers[i] = source[i];
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, authoring.DefaultPath);
                AddComponent(entity, state);
                AddComponent(entity, authoring.DefaultMovementState);

            }
        }
#endif
    }
}
