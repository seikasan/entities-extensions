using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Instantiation.Entities.Samples
{
    /// <summary>
    /// 空のGameObjectに追加すると、既定Worldにデータだけを持つ弾を3個生成します。
    /// </summary>
    public sealed class BulletSpawningSample : MonoBehaviour
    {
        World world;
        Entity prefab;
        Entity request;
        void Start()
        {
            world = World.DefaultGameObjectInjectionWorld;

            if (world == null ||
                !world.IsCreated)
            {
                Debug.LogError("Bullet sample requires an initialized Entities default World.", this);

                return;
            }

            var manager = world.EntityManager;
            prefab = manager.CreateEntity(
                typeof(Prefab),
                typeof(LocalTransform),
                typeof(BulletData));
            manager.SetComponentData(prefab, LocalTransform.Identity);
            request = manager.CreateEntity(typeof(BulletSpawnRequest));
            manager.SetComponentData(request, new BulletSpawnRequest { Prefab = prefab });
            Debug.Log(
                "Bullet sample: spawning 3 data-only bullets. Inspect BulletData in the Entities Hierarchy; bullets expire after 5 seconds.",
                this);
        }

        void OnDestroy()
        {
            if (world == null ||
                !world.IsCreated)
            {
                return;
            }

            var manager = world.EntityManager;
            if (manager.Exists(request))
            {
                manager.DestroyEntity(request);
            }

            if (manager.Exists(prefab))
            {
                manager.DestroyEntity(prefab);
            }
        }
    }
}
