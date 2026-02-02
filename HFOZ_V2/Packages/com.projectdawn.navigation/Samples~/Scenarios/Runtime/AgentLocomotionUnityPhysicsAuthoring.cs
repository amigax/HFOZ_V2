#if MODULE_UNITY_PHYSICS
using Unity.Physics;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using ProjectDawn.Navigation.Hybrid;
using Unity.Burst;
using Unity.Transforms;

namespace ProjectDawn.Navigation.Sample.Scenarios
{
    [RequireComponent(typeof(AgentAuthoring))]
    [DisallowMultipleComponent]
    public class AgentLocomotionUnityPhysicsAuthoring : MonoBehaviour
    {
        [SerializeField]
        float Speed = 3.5f;

        [SerializeField]
        float Acceleration = 8;

        [SerializeField]
        float AngularSpeed = 120;

        [SerializeField]
        float StoppingDistance = 0;

        [SerializeField]
        bool AutoBreaking = true;

        Entity m_Entity;

        public AgentLocomotionUnityPhysics DefaultLocomotion => new()
        {
            Speed = Speed,
            Acceleration = Acceleration,
            AngularSpeed = math.radians(AngularSpeed),
            StoppingDistance = StoppingDistance,
            AutoBreaking = AutoBreaking,
        };

        void Awake()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            m_Entity = GetComponent<AgentAuthoring>().GetOrCreateEntity();
            world.EntityManager.AddComponentData(m_Entity, DefaultLocomotion);
            if (TryGetComponent(out AgentCylinderShapeAuthoring shape))
            {
                var collider = Unity.Physics.CapsuleCollider.Create(new CapsuleGeometry
                {
                    Radius = shape.DefaultShape.Radius,
                    Vertex0 = math.up() * shape.DefaultShape.Height * 0.5f,
                    Vertex1 = math.down() * shape.DefaultShape.Height * 0.5f,
                }, CollisionFilter.Default);

                world.EntityManager.AddComponentData(m_Entity, new PhysicsCollider
                {
                    Value = collider,
                });

                // Dynamic rigid body setup
                var mass = PhysicsMass.CreateDynamic(collider.Value.MassProperties, mass: 1f);
                // Freeze rotation on X and Z axes
                mass.InverseInertia = new float3(0f, 0.0f, 0f);
                world.EntityManager.AddComponentData(m_Entity, mass);

                // Add damping and zero initial velocity
                world.EntityManager.AddComponentData(m_Entity, new PhysicsVelocity());
                world.EntityManager.AddComponentData(m_Entity, new PhysicsDamping
                {
                    Linear = 0,
                    Angular = 0
                });

                world.EntityManager.AddSharedComponent(m_Entity, new PhysicsWorldIndex
                {
                    
                });
            }
        }

        void OnDestroy()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null)
                world.EntityManager.RemoveComponent<TankLocomotion>(m_Entity);
        }
    }

    internal class AgentLocomotionUnityPhysicsBaker : Baker<AgentLocomotionUnityPhysicsAuthoring>
    {
        public override void Bake(AgentLocomotionUnityPhysicsAuthoring authoring)
        {
            AddComponent(GetEntity(TransformUsageFlags.Dynamic), authoring.DefaultLocomotion);
        }
    }

    public struct AgentLocomotionUnityPhysics : IComponentData
    {
        /// <summary>
        /// Maximum movement speed when moving to destination.
        /// </summary>
        public float Speed;
        /// <summary>
        /// The maximum acceleration of an agent as it follows a path, given in units / sec^2.
        /// </summary>
        public float Acceleration;
        /// <summary>
        /// Maximum turning speed in (rad/s) while following a path.
        /// </summary>
        public float AngularSpeed;
        /// <summary>
        /// Stop within this distance from the target position.
        /// </summary>
        public float StoppingDistance;
        /// <summary>
        /// Should the agent brake automatically to avoid overshooting the destination point?
        /// </summary>
        public bool AutoBreaking;
    }

    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(AgentSeekingSystemGroup))]
    public partial struct AgentUnityPhysicsSeekingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new AgentUnityPhysicsSeekingJob().ScheduleParallel();
        }

        [BurstCompile]
        partial struct AgentUnityPhysicsSeekingJob : IJobEntity
        {
            public void Execute(ref AgentBody body, ref PhysicsVelocity physicsVelocity, in AgentLocomotionUnityPhysics locomotion, in LocalTransform transform)
            {
                if (body.IsStopped)
                    return;

                float3 towards = body.Destination - transform.Position;
                float distance = math.length(towards);
                float3 desiredDirection = distance > math.EPSILON ? towards / distance : float3.zero;
                body.Force = desiredDirection;
                body.RemainingDistance = distance;
                body.Velocity = physicsVelocity.Linear;
            }
        }
    }

    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(AgentLocomotionSystemGroup))]
    public partial struct AgentUnityPhysicsLocomotionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new AgentUnityPhysicsLocomotionJob
            {
                DeltaTime = state.WorldUnmanaged.Time.DeltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct AgentUnityPhysicsLocomotionJob : IJobEntity
        {
            public float DeltaTime;

            public void Execute(ref LocalTransform transform, ref PhysicsVelocity physicsVelocity, ref AgentBody body, in AgentLocomotionUnityPhysics locomotion, in AgentShape shape)
            {
                if (body.IsStopped)
                    return;

                // Check, if we reached the destination
                float remainingDistance = body.RemainingDistance;
                if (remainingDistance <= locomotion.StoppingDistance + 1e-3f)
                {
                    body.Velocity = 0;
                    physicsVelocity.Linear = 0;
                    body.IsStopped = true;
                    return;
                }

                float maxSpeed = locomotion.Speed;

                // Start breaking if close to destination
                if (locomotion.AutoBreaking)
                {
                    float breakDistance = shape.Radius * 2 + locomotion.StoppingDistance;
                    if (remainingDistance <= breakDistance)
                    {
                        maxSpeed = math.lerp(locomotion.Speed * 0.25f, locomotion.Speed, remainingDistance / breakDistance);
                    }
                }

                // Force force to be maximum of unit length, but can be less
                float forceLength = math.length(body.Force);
                if (forceLength > 1)
                    body.Force = body.Force / forceLength;

                // Update rotation
                if (shape.Type == ShapeType.Circle)
                {
                    float angle = math.atan2(body.Velocity.x, body.Velocity.y);
                    transform.Rotation = math.slerp(transform.Rotation, quaternion.RotateZ(-angle), DeltaTime * locomotion.AngularSpeed);
                }
                else if (shape.Type == ShapeType.Cylinder)
                {
                    float angle = math.atan2(body.Velocity.x, body.Velocity.z);
                    transform.Rotation = math.slerp(transform.Rotation, quaternion.RotateY(angle), DeltaTime * locomotion.AngularSpeed);
                }

                // Tank should only move, if facing direction and movement direction is within certain degrees
                float3 direction = math.normalizesafe(body.Velocity);
                float3 facing = math.mul(transform.Rotation, new float3(1, 0, 0));
                if (math.dot(direction, facing) > math.radians(10))
                {
                    maxSpeed = 0;
                }

                // Interpolate velocity
                body.Velocity = math.lerp(body.Velocity, body.Force * maxSpeed, DeltaTime * locomotion.Acceleration);
                physicsVelocity.Linear = body.Velocity;

                float speed = math.length(body.Velocity);

                // Early out if steps is going to be very small
                if (speed < 1e-3f)
                    return;

                // Avoid over-stepping the destination
                if (speed * DeltaTime > remainingDistance)
                {
                    transform.Position += (body.Velocity / speed) * remainingDistance;
                    return;
                }
            }
        }
    }
}
#endif
