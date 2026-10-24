using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Systems;

namespace PhysicsEvents.Entities
{
    /// <summary>
    /// 生成済みEntityのBufferを、毎固定更新の物理処理前に予約します。
    /// </summary>
    [UpdateInGroup(typeof(BeforePhysicsSystemGroup), OrderLast = true)]
    public partial class PhysicsEventCapacitySystem : SystemBase
    {
        private EntityQuery _triggerEnter;
        private EntityQuery _triggerStay;
        private EntityQuery _triggerExit;
        private EntityQuery _collisionEnter;
        private EntityQuery _collisionStay;
        private EntityQuery _collisionExit;
        protected override void OnCreate()
        {
            _triggerEnter = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<TriggerEnter>(),
                    ComponentType.ReadWrite<PhysicsEventBufferCapacity>()
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });
            _triggerEnter.SetChangedVersionFilter(ComponentType.ReadWrite<PhysicsEventBufferCapacity>());
            _triggerStay = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<TriggerStay>(),
                    ComponentType.ReadWrite<PhysicsEventBufferCapacity>()
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });
            _triggerStay.SetChangedVersionFilter(ComponentType.ReadWrite<PhysicsEventBufferCapacity>());
            _triggerExit = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<TriggerExit>(),
                    ComponentType.ReadWrite<PhysicsEventBufferCapacity>()
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });
            _triggerExit.SetChangedVersionFilter(ComponentType.ReadWrite<PhysicsEventBufferCapacity>());
            _collisionEnter = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<CollisionEnter>(),
                    ComponentType.ReadWrite<PhysicsEventBufferCapacity>()
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });
            _collisionEnter.SetChangedVersionFilter(ComponentType.ReadWrite<PhysicsEventBufferCapacity>());
            _collisionStay = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<CollisionStay>(),
                    ComponentType.ReadWrite<PhysicsEventBufferCapacity>()
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });
            _collisionStay.SetChangedVersionFilter(ComponentType.ReadWrite<PhysicsEventBufferCapacity>());
            _collisionExit = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<CollisionExit>(),
                    ComponentType.ReadWrite<PhysicsEventBufferCapacity>()
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });
            _collisionExit.SetChangedVersionFilter(ComponentType.ReadWrite<PhysicsEventBufferCapacity>());
        }

        protected override void OnUpdate()
        {
            // Source Generatorが読み取り権限を判定できるよう、定数式ではなくリテラルで指定します。
#if PHYSICS_EVENTS_PROFILING
            var settings = GetComponentTypeHandle<PhysicsEventBufferCapacity>(false);
#else
            var settings = GetComponentTypeHandle<PhysicsEventBufferCapacity>(true);
#endif
            if (!_triggerEnter.IsEmptyIgnoreFilter)
            {
                Dependency = new ReserveTriggerEnter
                {
                    Buffers = GetBufferTypeHandle<TriggerEnter>(),
                    Settings = settings
                }.Schedule(_triggerEnter, Dependency);
            }

            if (!_triggerStay.IsEmptyIgnoreFilter)
            {
                Dependency = new ReserveTriggerStay
                {
                    Buffers = GetBufferTypeHandle<TriggerStay>(),
                    Settings = settings
                }.Schedule(_triggerStay, Dependency);
            }

            if (!_triggerExit.IsEmptyIgnoreFilter)
            {
                Dependency = new ReserveTriggerExit
                {
                    Buffers = GetBufferTypeHandle<TriggerExit>(),
                    Settings = settings
                }.Schedule(_triggerExit, Dependency);
            }

            if (!_collisionEnter.IsEmptyIgnoreFilter)
            {
                Dependency = new ReserveCollisionEnter
                {
                    Buffers = GetBufferTypeHandle<CollisionEnter>(),
                    Settings = settings
                }.Schedule(_collisionEnter, Dependency);
            }

            if (!_collisionStay.IsEmptyIgnoreFilter)
            {
                Dependency = new ReserveCollisionStay
                {
                    Buffers = GetBufferTypeHandle<CollisionStay>(),
                    Settings = settings
                }.Schedule(_collisionStay, Dependency);
            }

            if (!_collisionExit.IsEmptyIgnoreFilter)
            {
                Dependency = new ReserveCollisionExit
                {
                    Buffers = GetBufferTypeHandle<CollisionExit>(),
                    Settings = settings
                }.Schedule(_collisionExit, Dependency);
            }
        }

        [BurstCompile]
        private struct ReserveTriggerEnter : IJobChunk
        {
            public BufferTypeHandle<TriggerEnter> Buffers;
#if !PHYSICS_EVENTS_PROFILING
            [ReadOnly]
#endif
            public ComponentTypeHandle<PhysicsEventBufferCapacity> Settings;
            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var buffers = chunk.GetBufferAccessor(ref Buffers);
                var settings = chunk.GetNativeArray(ref Settings);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var setting = settings[i];
                    var buffer = buffers[i];
                    if (setting.TriggerEnter > buffer.Capacity)
                    {
                        buffer.EnsureCapacity(setting.TriggerEnter);
#if PHYSICS_EVENTS_PROFILING
                        setting.ReservationCount++;
                        settings[i] = setting;
#endif
                    }
                }
            }
        }

        [BurstCompile]
        private struct ReserveTriggerStay : IJobChunk
        {
            public BufferTypeHandle<TriggerStay> Buffers;
#if !PHYSICS_EVENTS_PROFILING
            [ReadOnly]
#endif
            public ComponentTypeHandle<PhysicsEventBufferCapacity> Settings;
            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var buffers = chunk.GetBufferAccessor(ref Buffers);
                var settings = chunk.GetNativeArray(ref Settings);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var setting = settings[i];
                    var buffer = buffers[i];
                    if (setting.TriggerStay > buffer.Capacity)
                    {
                        buffer.EnsureCapacity(setting.TriggerStay);
#if PHYSICS_EVENTS_PROFILING
                        setting.ReservationCount++;
                        settings[i] = setting;
#endif
                    }
                }
            }
        }

        [BurstCompile]
        private struct ReserveTriggerExit : IJobChunk
        {
            public BufferTypeHandle<TriggerExit> Buffers;
#if !PHYSICS_EVENTS_PROFILING
            [ReadOnly]
#endif
            public ComponentTypeHandle<PhysicsEventBufferCapacity> Settings;
            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var buffers = chunk.GetBufferAccessor(ref Buffers);
                var settings = chunk.GetNativeArray(ref Settings);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var setting = settings[i];
                    var buffer = buffers[i];
                    if (setting.TriggerExit > buffer.Capacity)
                    {
                        buffer.EnsureCapacity(setting.TriggerExit);
#if PHYSICS_EVENTS_PROFILING
                        setting.ReservationCount++;
                        settings[i] = setting;
#endif
                    }
                }
            }
        }

        [BurstCompile]
        private struct ReserveCollisionEnter : IJobChunk
        {
            public BufferTypeHandle<CollisionEnter> Buffers;
#if !PHYSICS_EVENTS_PROFILING
            [ReadOnly]
#endif
            public ComponentTypeHandle<PhysicsEventBufferCapacity> Settings;
            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var buffers = chunk.GetBufferAccessor(ref Buffers);
                var settings = chunk.GetNativeArray(ref Settings);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var setting = settings[i];
                    var buffer = buffers[i];
                    if (setting.CollisionEnter > buffer.Capacity)
                    {
                        buffer.EnsureCapacity(setting.CollisionEnter);
#if PHYSICS_EVENTS_PROFILING
                        setting.ReservationCount++;
                        settings[i] = setting;
#endif
                    }
                }
            }
        }

        [BurstCompile]
        private struct ReserveCollisionStay : IJobChunk
        {
            public BufferTypeHandle<CollisionStay> Buffers;
#if !PHYSICS_EVENTS_PROFILING
            [ReadOnly]
#endif
            public ComponentTypeHandle<PhysicsEventBufferCapacity> Settings;
            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var buffers = chunk.GetBufferAccessor(ref Buffers);
                var settings = chunk.GetNativeArray(ref Settings);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var setting = settings[i];
                    var buffer = buffers[i];
                    if (setting.CollisionStay > buffer.Capacity)
                    {
                        buffer.EnsureCapacity(setting.CollisionStay);
#if PHYSICS_EVENTS_PROFILING
                        setting.ReservationCount++;
                        settings[i] = setting;
#endif
                    }
                }
            }
        }

        [BurstCompile]
        private struct ReserveCollisionExit : IJobChunk
        {
            public BufferTypeHandle<CollisionExit> Buffers;
#if !PHYSICS_EVENTS_PROFILING
            [ReadOnly]
#endif
            public ComponentTypeHandle<PhysicsEventBufferCapacity> Settings;
            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var buffers = chunk.GetBufferAccessor(ref Buffers);
                var settings = chunk.GetNativeArray(ref Settings);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var setting = settings[i];
                    var buffer = buffers[i];
                    if (setting.CollisionExit > buffer.Capacity)
                    {
                        buffer.EnsureCapacity(setting.CollisionExit);
#if PHYSICS_EVENTS_PROFILING
                        setting.ReservationCount++;
                        settings[i] = setting;
#endif
                    }
                }
            }
        }
    }
}
