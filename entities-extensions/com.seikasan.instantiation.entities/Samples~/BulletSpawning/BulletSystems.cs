using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Instantiation.Entities.Samples
{
    public struct BulletData : IComponentData
    {
        public float Damage;
        public float Speed;
        public float RemainingLifetime;
    }

    public struct BulletSpawnRequest : IComponentData
    {
        public Entity Prefab;
    }

    [BurstCompile]
    public partial struct BulletSpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<BulletSpawnRequest>();
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (request, requestEntity) in SystemAPI.Query<RefRO<BulletSpawnRequest>>().WithEntityAccess())
            {
                for (int i = 0; i < 3; i++)
                {
                    ecb.Instantiate(
                        request.ValueRO.Prefab,
                        LocalTransform.FromPosition(new float3(
                            i * 2,
                            0,
                            0)),
                        new BulletData
                        {
                            Damage = 10,
                            Speed = 20,
                            RemainingLifetime = 5
                        });
                }

                ecb.DestroyEntity(requestEntity);
            }

            // 列挙を完了してから構造変更をPlaybackします。
            ecb.Playback(state.EntityManager);
        }
    }

    [UpdateAfter(typeof(BulletSpawnSystem))]
    [BurstCompile]
    public partial struct BulletMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            float deltaTime = state.WorldUnmanaged.Time.DeltaTime;
            foreach (var (bullet, transform, entity) in SystemAPI.Query<RefRW<BulletData>, RefRW<LocalTransform>>().WithEntityAccess())
            {
                transform.ValueRW.Position.z += bullet.ValueRO.Speed * deltaTime;
                bullet.ValueRW.RemainingLifetime -= deltaTime;
                if (bullet.ValueRO.RemainingLifetime <= 0)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
