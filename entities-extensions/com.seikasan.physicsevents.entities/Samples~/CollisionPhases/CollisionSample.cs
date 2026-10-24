using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace PhysicsEvents.Entities.Samples.CollisionPhases
{
    public struct CollisionSurface : IComponentData
    {
    }

    public struct CollisionPlayer : IComponentData
    {
    }

    public struct CollisionCounts : IComponentData
    {
        public int Enter;
        public int Stay;
        public int Exit;
    }

    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [BurstCompile]
    public partial struct CollisionSampleSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var counts = SystemAPI.GetComponentLookup<CollisionCounts>();
            foreach (var events in SystemAPI.Query<DynamicBuffer<CollisionEnter>>().WithAll<CollisionSurface>())
            {
                foreach (var contact in events)
                {
                    if (!counts.HasComponent(contact.Other))
                    {
                        continue;
                    }

                    var count = counts[contact.Other];
                    count.Enter++;
                    counts[contact.Other] = count;
                }
            }

            foreach (var events in SystemAPI.Query<DynamicBuffer<CollisionStay>>().WithAll<CollisionSurface>())
            {
                foreach (var contact in events)
                {
                    if (!counts.HasComponent(contact.Other))
                    {
                        continue;
                    }

                    var count = counts[contact.Other];
                    count.Stay++;
                    counts[contact.Other] = count;
                }
            }

            foreach (var events in SystemAPI.Query<DynamicBuffer<CollisionExit>>().WithAll<CollisionSurface>())
            {
                foreach (var contact in events)
                {
                    if (!counts.HasComponent(contact.Other))
                    {
                        continue;
                    }

                    var count = counts[contact.Other];
                    count.Exit++;
                    counts[contact.Other] = count;
                }
            }

            // 浅い接触を2秒間維持し、次の物理更新で離します。
            // この移動処理により、シーンや入力を設定せず3つの接触状態を確認できます。
            var position = state.WorldUnmanaged.Time.ElapsedTime < 2 ? new float3(
                0,
                1.9f,
                0) : new float3(
                    5,
                    0,
                    0);
            foreach (var (transform, velocity) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>>().WithAll<CollisionPlayer>())
            {
                transform.ValueRW = LocalTransform.FromPosition(position);
                velocity.ValueRW = default;
            }
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct CollisionSampleSetupSystem : ISystem
    {
        private BlobAssetReference<Collider> _surface;
        private BlobAssetReference<Collider> _player;
        public void OnCreate(ref SystemState state)
        {
            var geometry = new BoxGeometry
            {
                Center = float3.zero,
                Size = new float3(2),
                Orientation = quaternion.identity,
                BevelRadius = 0
            };
            var material = Material.Default;
            material.CollisionResponse = CollisionResponsePolicy.CollideRaiseCollisionEvents;
            _surface = BoxCollider.Create(
                geometry,
                CollisionFilter.Default,
                material);
            _player = BoxCollider.Create(geometry);
            var manager = state.EntityManager;
            var surface = manager.CreateEntity(
                typeof(CollisionSurface),
                typeof(PhysicsCollider),
                typeof(LocalTransform),
                typeof(PhysicsWorldIndex));
            manager.SetComponentData(surface, new PhysicsCollider { Value = _surface });
            manager.SetComponentData(surface, LocalTransform.Identity);
            manager.AddBuffer<CollisionEnter>(surface);
            manager.AddBuffer<CollisionStay>(surface);
            manager.AddBuffer<CollisionExit>(surface);
            manager.AddComponentData(surface, new PhysicsEventBufferCapacity
            {
                CollisionEnter = 4,
                CollisionStay = 4,
                CollisionExit = 4
            });
            manager.SetName(surface, "Collision sample surface");
            var player = manager.CreateEntity(
                typeof(CollisionPlayer),
                typeof(CollisionCounts),
                typeof(PhysicsCollider),
                typeof(LocalTransform),
                typeof(PhysicsWorldIndex),
                typeof(PhysicsMass),
                typeof(PhysicsVelocity),
                typeof(PhysicsGravityFactor));
            manager.SetComponentData(player, new PhysicsCollider { Value = _player });
            manager.SetComponentData(player, LocalTransform.FromPosition(new float3(
                0,
                1.9f,
                0)));
            manager.SetComponentData(player, PhysicsMass.CreateDynamic(_player.Value.MassProperties, 1));
            manager.SetComponentData(player, new PhysicsGravityFactor { Value = 0 });
            manager.SetName(player, "Collision sample player");
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }

        public void OnDestroy(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();
            if (_surface.IsCreated)
            {
                _surface.Dispose();
            }

            if (_player.IsCreated)
            {
                _player.Dispose();
            }
        }
    }
}
