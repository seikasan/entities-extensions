using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace GameObjectBridge.Entities.Samples
{
    /// <summary>
    /// 空のGameObjectに追加すると、Entityが動かすCubeと、Entityを動かすSphereを生成します。
    /// </summary>
    public sealed class TransformBridgeSample : MonoBehaviour
    {
        World world;
        Entity cube, sphere;
        GameObject input;
        void Start()
        {
            world = World.DefaultGameObjectInjectionWorld;

            if (world == null ||
                !world.IsCreated)
            {
                Debug.LogError("初期化済みのEntities Worldが必要です。", this);

                return;
            }

            cube = Create(world, new float3(
                -2,
                0,
                0));
            sphere = Create(world, new float3(
                2,
                0,
                0));
            var output = GameObject.CreatePrimitive(PrimitiveType.Cube);
            output.name = "Entity drives this cube";
            input = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            input.name = "This sphere drives its Entity";
            EntityGameObjectBridge.Bind(
                world,
                cube,
                output,
                destroyGameObjectOnUnbind: true);
            EntityGameObjectBridge.Bind(
                world,
                sphere,
                input,
                TransformSyncDirection.GameObjectToEntity,
                destroyGameObjectOnUnbind: true);
            world.EntityManager.AddComponent<BridgeSampleMotion>(cube);
        }

        public static Entity Create(World world, float3 position)
        {
            var entity = world.EntityManager.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld));
            world.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(position));
            world.EntityManager.SetComponentData(entity, new LocalToWorld { Value = float4x4.Translate(position) });

            return entity;
        }

        void Update()
        {
            if (input != null)
            {
                input.transform.position = new Vector3(
                    2,
                    Mathf.Sin(Time.time),
                    0);
            }
        }

        void OnDestroy()
        {
            if (world == null ||
                !world.IsCreated)
            {
                return;
            }

            if (world.EntityManager.Exists(cube))
            {
                world.EntityManager.DestroyEntity(cube);
            }

            if (world.EntityManager.Exists(sphere))
            {
                world.EntityManager.DestroyEntity(sphere);
            }
        }
    }

    public struct BridgeSampleMotion : IComponentData
    {
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct BridgeSampleMotionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float time = (float)state.WorldUnmanaged.Time.ElapsedTime;
            foreach (var value in SystemAPI.Query<RefRW<LocalTransform>>().WithAll<BridgeSampleMotion>())
            {
                value.ValueRW.Position = new float3(
                    -2,
                    math.sin(time),
                    0);
            }
        }
    }
}
