using FMODUnity;
using GameObjectBridge.Entities.Fmod;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace GameObjectBridge.Entities.Samples
{
    public sealed class FmodBridgeSample : MonoBehaviour
    {
        [SerializeField]
        private EventReference _event;

        public EventReference Event
        {
            get => _event;
            set => _event = value;
        }
        World world;
        Entity entity;
        void Start()
        {
            if (Event.IsNull)
            {
                Debug.LogError("ロード済みのバンクから3D対応のFMODイベントを指定してください。", this);

                return;
            }

            world = World.DefaultGameObjectInjectionWorld;

            if (world == null ||
                !world.IsCreated)
            {
                Debug.LogError("初期化済みのEntities Worldが必要です。", this);

                return;
            }

            entity = world.EntityManager.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld));
            world.EntityManager.SetComponentData(entity, LocalTransform.Identity);
            world.EntityManager.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });
            var source = new GameObject("Entity FMOD source");
            var bridge = EntityGameObjectBridge.Bind(
                world,
                entity,
                source,
                destroyGameObjectOnUnbind: true);
            bridge.ConfigureFmod(Event);
            world.EntityManager.GetBuffer<FmodCommand>(entity).Add(FmodCommand.Play());
        }

        void Update()
        {
            if (world != null &&
                world.IsCreated &&
                world.EntityManager.Exists(entity))
            {
                world.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(new float3(
                    Mathf.Sin(Time.time) * 3,
                    0,
                    2)));
            }
        }

        void OnDestroy()
        {
            if (world != null &&
                world.IsCreated &&
                world.EntityManager.Exists(entity))
            {
                world.EntityManager.DestroyEntity(entity);
            }
        }
    }
}
