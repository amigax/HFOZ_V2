using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;
using static Unity.Entities.SystemAPI;

namespace ProjectDawn.Navigation.Sample.Zerg
{
    [RequireMatchingQueriesForUpdate]
    public partial class UnitDeathSystem : SystemBase
    {
        static List<GameObject> GameObjectsToDestroy = new();
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            foreach (var (e, entity) in SystemAPI.Query<UnitDead>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);

                if (ManagedAPI.HasComponent<Transform>(entity))
                {
                    var behaviour = ManagedAPI.GetComponent<Transform>(entity);
                    GameObjectsToDestroy.Add(behaviour.gameObject);
                }
            }

            if (GameObjectsToDestroy.Count > 0)
            {
                foreach (var gameObject in GameObjectsToDestroy)
                {
                    GameObject.Destroy(gameObject);
                }
                GameObjectsToDestroy.Clear();
            }
        }
    }
}
