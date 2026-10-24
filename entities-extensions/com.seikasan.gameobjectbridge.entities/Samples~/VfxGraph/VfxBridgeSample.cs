using GameObjectBridge.Entities.VfxGraph;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.VFX;

namespace GameObjectBridge.Entities.Samples
{
    public sealed class VfxBridgeSample : MonoBehaviour
    {
        [SerializeField]
        private VisualEffectAsset _graph;

        public VisualEffectAsset Graph
        {
            get => _graph;
            set => _graph = value;
        }
        World world;
        Entity entity;
        int floatId;
        void Start()
        {
            if (Graph == null)
            {
                Debug.LogError("Assets > Create > GameObject BridgeからVFX Sample Graphを作成し、Graphに割り当ててください。", this);

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
            var target = new GameObject("Entity VFX target");
            var effect = target.AddComponent<VisualEffect>();
            effect.visualEffectAsset = Graph;
            var bridge = EntityGameObjectBridge.Bind(
                world,
                entity,
                target,
                destroyGameObjectOnUnbind: true);
            bridge.ConfigureVisualEffect(effect);
            floatId = Shader.PropertyToID("BridgeFloat");
            world.EntityManager.GetBuffer<VfxCommand>(entity).Add(VfxCommand.Play());
        }

        void Update()
        {
            if (world == null ||
                !world.IsCreated ||
                !world.EntityManager.Exists(entity))
            {
                return;
            }

            world.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(new float3(
                Mathf.Sin(Time.time) * 2,
                1,
                0)));
            world.EntityManager.GetBuffer<VfxCommand>(entity).Add(VfxCommand.SetFloat(floatId, 10 + Mathf.Sin(Time.time) * 5));
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
