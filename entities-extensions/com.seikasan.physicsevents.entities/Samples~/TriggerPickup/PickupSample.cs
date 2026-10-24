using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace PhysicsEvents.Entities.Samples
{
    public struct PickupZone : IComponentData
    {
    }

    public struct PlayerTag : IComponentData
    {
    }

    public struct PickupCount : IComponentData
    {
        public int Value;
    }

    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [BurstCompile]
    public partial struct PickupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var counts = SystemAPI.GetComponentLookup<PickupCount>();
            foreach (var (events, self) in SystemAPI.Query<DynamicBuffer<TriggerEnter>>().WithAll<PickupZone>().WithEntityAccess())
            {
                foreach (var contact in events)
                {
                    if (!SystemAPI.HasComponent<PlayerTag>(contact.Other))
                    {
                        continue;
                    }

                    var count = counts[contact.Other];
                    count.Value++;
                    counts[contact.Other] = count;
                    ecb.DestroyEntity(self);
                    break;
                }
            }

            // 列挙中のBufferを無効化しないよう、構造変更は列挙後に行います。
            ecb.Playback(state.EntityManager);
        }
    }

    /// <summary>
    /// 単体で動く物理デモを生成します。シーン参照やイベント用Authoringは不要です。
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PickupSampleSetupSystem : ISystem
    {
        private BlobAssetReference<Collider> _zoneCollider;
        private BlobAssetReference<Collider> _playerCollider;
        public void OnCreate(ref SystemState state)
        {
            var material = Material.Default;
            material.CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents;
            _zoneCollider = BoxCollider.Create(
                new BoxGeometry
                {
                    Center = float3.zero,
                    Size = new float3(2),
                    Orientation = quaternion.identity,
                    BevelRadius = 0
                },
                CollisionFilter.Default,
                material);
            _playerCollider = SphereCollider.Create(new SphereGeometry
            {
                Center = float3.zero,
                Radius = 0.5f
            });
            var manager = state.EntityManager;
            var zone = manager.CreateEntity(
                typeof(PickupZone),
                typeof(PhysicsCollider),
                typeof(LocalTransform),
                typeof(PhysicsWorldIndex));
            manager.SetComponentData(zone, new PhysicsCollider { Value = _zoneCollider });
            manager.SetComponentData(zone, LocalTransform.Identity);
            manager.AddBuffer<TriggerEnter>(zone);
            manager.AddComponentData(zone, new PhysicsEventBufferCapacity { TriggerEnter = 4 });
            manager.SetName(zone, "Pickup zone");
            var player = manager.CreateEntity(
                typeof(PlayerTag),
                typeof(PickupCount),
                typeof(PhysicsCollider),
                typeof(LocalTransform),
                typeof(PhysicsWorldIndex),
                typeof(PhysicsMass),
                typeof(PhysicsVelocity),
                typeof(PhysicsGravityFactor));
            manager.SetComponentData(player, new PhysicsCollider { Value = _playerCollider });
            manager.SetComponentData(player, LocalTransform.FromPosition(new float3(
                -4,
                0,
                0)));
            manager.SetComponentData(player, PhysicsMass.CreateKinematic(_playerCollider.Value.MassProperties));
            manager.SetComponentData(player, new PhysicsVelocity
            {
                Linear = new float3(
                    1,
                    0,
                    0)
            });
            manager.SetComponentData(player, new PhysicsGravityFactor { Value = 0 });
            manager.SetName(player, "Pickup sample player");
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }

        public void OnDestroy(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();
            if (_zoneCollider.IsCreated)
            {
                _zoneCollider.Dispose();
            }

            if (_playerCollider.IsCreated)
            {
                _playerCollider.Dispose();
            }
        }
    }
}
