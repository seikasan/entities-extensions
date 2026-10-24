using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;

namespace PhysicsEvents.Entities
{
    /// <summary>
    /// Worldで履歴を共有し、各チャンクが所有するEntityのBufferへ書き込みます。
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(ExportPhysicsWorld))]
    [UpdateBefore(typeof(AfterPhysicsSystemGroup))]
    public partial struct PhysicsEventCollectSystem : ISystem
    {
#if PHYSICS_EVENTS_PROFILING
        internal int UpdateInvocations;
        internal int TriggerUpdateInvocations;
        internal int CollisionUpdateInvocations;
        internal int ManagedUpdateInvocations;
        internal int ManagedTriggerUpdateInvocations;
        internal int ManagedCollisionUpdateInvocations;

        [BurstDiscard]
        private static void DetectManagedUpdate(ref bool managed)
        {
            managed = true;
        }

        public long TriggerSchedules { get; private set; }
        public long CollisionSchedules { get; private set; }
        public long BufferWriteSchedules { get; private set; }
#else
        public long TriggerSchedules => 0;
        public long CollisionSchedules => 0;
        public long BufferWriteSchedules => 0;

#endif
        internal bool ParallelWrites;
        internal bool ReuseUnchangedContacts;
#if PHYSICS_EVENTS_PROFILING
        private Unity.Profiling.ProfilerMarker _scheduleMarker;
#endif
        private EntityQuery _simulation;
#if PHYSICS_EVENTS_PROFILING
        private NativeReference<PhysicsEventMetrics> _triggerMetrics;
        private NativeReference<PhysicsEventMetrics> _collisionMetrics;
#endif
        private EntityQuery _triggerSubscribers;
        private EntityQuery _triggerBuffers;
        private NativeList<DirectedContact> _triggerPrevious;
        private NativeList<DirectedContact> _triggerCurrent;
        private NativeList<DirectedContact> _triggerRawSnapshot;
        private NativeList<PhaseEvent> _triggerEvents;
        private NativeReference<SingleTarget> _triggerSingleTarget;
        private NativeParallelHashMap<Entity, TargetState> _triggerTargets;
#if PHYSICS_EVENTS_PROFILING
        private NativeArray<WriterStatistics> _triggerStatistics;
#endif
        private bool _triggerWasActive;
        private EntityQuery _collisionSubscribers;
        private EntityQuery _collisionBuffers;
        private NativeList<DirectedContact> _collisionPrevious;
        private NativeList<DirectedContact> _collisionCurrent;
        private NativeList<DirectedContact> _collisionRawSnapshot;
        private NativeList<PhaseEvent> _collisionEvents;
        private NativeReference<SingleTarget> _collisionSingleTarget;
        private NativeParallelHashMap<Entity, TargetState> _collisionTargets;
#if PHYSICS_EVENTS_PROFILING
        private NativeArray<WriterStatistics> _collisionStatistics;
#endif
        private bool _collisionWasActive;
        internal int StoredEventCount
        {
            get
            {
                return (_triggerEvents.IsCreated ? _triggerEvents.Length : 0) + (_collisionEvents.IsCreated ? _collisionEvents.Length : 0);
            }
        }

        public bool HasAllocatedEventStorage => _triggerPrevious.IsCreated ||
            _collisionPrevious.IsCreated;

#if PHYSICS_EVENTS_PROFILING
        public const bool ProfilingEnabled = true;
#else
        public const bool ProfilingEnabled = false;
#endif
        /// <summary>
        /// Jobを完了して接触数を返します。累積カウンターはPHYSICS_EVENTS_PROFILING指定時だけ有効です。
        /// </summary>
        public PhysicsEventMetrics CompleteAndGetMetrics(ref SystemState state)
        {
            state.CompleteDependency();

#if PHYSICS_EVENTS_PROFILING
            var trigger = _triggerMetrics.IsCreated ? _triggerMetrics.Value : default;
            var collision = _collisionMetrics.IsCreated ? _collisionMetrics.Value : default;

            return new PhysicsEventMetrics
            {
                UnchangedContactFrames = trigger.UnchangedContactFrames + collision.UnchangedContactFrames,
                SnapshotReallocations = trigger.SnapshotReallocations + collision.SnapshotReallocations,
                ContactReallocations = trigger.ContactReallocations + collision.ContactReallocations,
                BufferReallocations = trigger.BufferReallocations + collision.BufferReallocations,
                EventReallocations = trigger.EventReallocations + collision.EventReallocations,
                WriterStorageReallocations = trigger.WriterStorageReallocations + collision.WriterStorageReallocations,
                TargetCacheReallocations = trigger.TargetCacheReallocations + collision.TargetCacheReallocations,
                EventsWritten = trigger.EventsWritten + collision.EventsWritten,
                TriggerContacts = trigger.TriggerContacts,
                CollisionContacts = collision.CollisionContacts,
                ManagedFallbacks = trigger.ManagedFallbacks + collision.ManagedFallbacks
            };
#else
            return new PhysicsEventMetrics
            {
                TriggerContacts = _triggerWasActive ? _triggerPrevious.Length : 0,
                CollisionContacts = _collisionWasActive ? _collisionPrevious.Length : 0
            };
#endif
        }

        /// <summary>
        /// 接触履歴・対象キー・通知範囲・通知の保持データ量の下限です。Hashコンテナ内部領域は含みません。
        /// </summary>
        internal long ContactStorageBytes
        {
            get
            {
                long bytes = 0;
                if (_triggerPrevious.IsCreated)
                {
                    bytes += (_triggerPrevious.Capacity + (long)_triggerCurrent.Capacity + _triggerRawSnapshot.Capacity) * UnsafeUtility.SizeOf<DirectedContact>();
                }

                if (_triggerTargets.IsCreated)
                {
                    bytes += (long)_triggerTargets.Capacity * (UnsafeUtility.SizeOf<Entity>() + UnsafeUtility.SizeOf<TargetState>());
                    bytes += (long)_triggerEvents.Capacity * UnsafeUtility.SizeOf<PhaseEvent>();
                    bytes += UnsafeUtility.SizeOf<SingleTarget>();
                }

                if (_collisionPrevious.IsCreated)
                {
                    bytes += (_collisionPrevious.Capacity + (long)_collisionCurrent.Capacity + _collisionRawSnapshot.Capacity) * UnsafeUtility.SizeOf<DirectedContact>();
                }

                if (_collisionTargets.IsCreated)
                {
                    bytes += (long)_collisionTargets.Capacity * (UnsafeUtility.SizeOf<Entity>() + UnsafeUtility.SizeOf<TargetState>());
                    bytes += (long)_collisionEvents.Capacity * UnsafeUtility.SizeOf<PhaseEvent>();
                    bytes += UnsafeUtility.SizeOf<SingleTarget>();
                }

#if PHYSICS_EVENTS_PROFILING
                if (_triggerMetrics.IsCreated)
                {
                    bytes += UnsafeUtility.SizeOf<PhysicsEventMetrics>();
                }

                if (_collisionMetrics.IsCreated)
                {
                    bytes += UnsafeUtility.SizeOf<PhysicsEventMetrics>();
                }
                    if (_triggerStatistics.IsCreated)
                {
                    bytes += (long)_triggerStatistics.Length * UnsafeUtility.SizeOf<WriterStatistics>();
                }
                    if (_collisionStatistics.IsCreated)
                {
                    bytes += (long)_collisionStatistics.Length * UnsafeUtility.SizeOf<WriterStatistics>();
                }
#endif
                return bytes;
            }
        }

        /// <summary>
        /// このSystemの依存Jobを完了して、接触関連データの保持容量の下限を返します。
        /// </summary>
        public long GetContactStorageBytes(ref SystemState state)
        {
            state.CompleteDependency();

            return ContactStorageBytes;
        }

        public void OnCreate(ref SystemState state)
        {
            ParallelWrites = true;
            ReuseUnchangedContacts = true;
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker = new Unity.Profiling.ProfilerMarker("PhysicsEvents.Collector.Schedule");
#endif
            _simulation = state.GetEntityQuery(ComponentType.ReadOnly<SimulationSingleton>());
            _triggerSubscribers = state.GetEntityQuery(new EntityQueryDesc
            {
                Any = new[]
                {
                    ComponentType.ReadOnly<TriggerEnter>(),
                    ComponentType.ReadOnly<TriggerStay>(),
                    ComponentType.ReadOnly<TriggerExit>()
                }
            });
            _triggerBuffers = state.GetEntityQuery(new EntityQueryDesc
            {
                Any = new[]
                {
                    ComponentType.ReadWrite<TriggerEnter>(),
                    ComponentType.ReadWrite<TriggerStay>(),
                    ComponentType.ReadWrite<TriggerExit>()
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState | EntityQueryOptions.IncludeDisabledEntities
            });
            _collisionSubscribers = state.GetEntityQuery(new EntityQueryDesc
            {
                Any = new[]
                {
                    ComponentType.ReadOnly<CollisionEnter>(),
                    ComponentType.ReadOnly<CollisionStay>(),
                    ComponentType.ReadOnly<CollisionExit>()
                }
            });
            _collisionBuffers = state.GetEntityQuery(new EntityQueryDesc
            {
                Any = new[]
                {
                    ComponentType.ReadWrite<CollisionEnter>(),
                    ComponentType.ReadWrite<CollisionStay>(),
                    ComponentType.ReadWrite<CollisionExit>()
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState | EntityQueryOptions.IncludeDisabledEntities
            });
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
#if PHYSICS_EVENTS_PROFILING
            if (_triggerMetrics.IsCreated)
            {
                _triggerMetrics.Dispose();
            }

            if (_collisionMetrics.IsCreated)
            {
                _collisionMetrics.Dispose();
            }
#endif
            if (_triggerPrevious.IsCreated)
            {
                _triggerPrevious.Dispose();
                _triggerCurrent.Dispose();
                _triggerRawSnapshot.Dispose();
                _triggerEvents.Dispose();
                _triggerSingleTarget.Dispose();
            }

            if (_triggerTargets.IsCreated)
            {
                _triggerTargets.Dispose();
            }

#if PHYSICS_EVENTS_PROFILING
            if (_triggerStatistics.IsCreated)
            {
                _triggerStatistics.Dispose();
            }
#endif
            if (_collisionPrevious.IsCreated)
            {
                _collisionPrevious.Dispose();
                _collisionCurrent.Dispose();
                _collisionRawSnapshot.Dispose();
                _collisionEvents.Dispose();
                _collisionSingleTarget.Dispose();
            }

            if (_collisionTargets.IsCreated)
            {
                _collisionTargets.Dispose();
            }
#if PHYSICS_EVENTS_PROFILING
            if (_collisionStatistics.IsCreated)
            {
                _collisionStatistics.Dispose();
            }
#endif
        }

        public void OnUpdate(ref SystemState state)
        {
#if PHYSICS_EVENTS_PROFILING
            UpdateInvocations++;
            bool managed = false;
            DetectManagedUpdate(ref managed);
            if (managed)
            {
                ManagedUpdateInvocations++;
            }
#endif
            bool trigger = !_triggerSubscribers.IsEmpty;
            bool collision = !_collisionSubscribers.IsEmpty;
            SimulationSingleton simulation = default;
            if (trigger ||
                collision)
            {
                if (!_simulation.TryGetSingleton(out simulation) ||
                    simulation.Type != SimulationType.UnityPhysics)
                {
                    trigger = false;
                    collision = false;
                }
            }

            // 両経路は同じ入力依存から開始し、互いの当該更新のJobを待ちません。
            var triggerDependency = state.Dependency;
            var collisionDependency = triggerDependency;
            try
            {
                UpdateTrigger(
                    ref state,
                    trigger,
                    simulation,
                    ref triggerDependency);
                UpdateCollision(
                    ref state,
                    collision,
                    simulation,
                    ref collisionDependency);
            }
            finally
            {
                // 途中で例外が発生しても、既に起動したJobの所有権を失わないようにします。
                state.Dependency = JobHandle.CombineDependencies(triggerDependency, collisionDependency);
            }
        }

        private void UpdateTrigger(
            ref SystemState state,
            bool active,
            SimulationSingleton simulation,
            ref JobHandle dependency)
        {
#if PHYSICS_EVENTS_PROFILING
            if (active)
            {
                TriggerUpdateInvocations++;
                bool managed = false;
                DetectManagedUpdate(ref managed);
                if (managed)
                {
                    ManagedTriggerUpdateInvocations++;
                }
            }
            if (_triggerMetrics.IsCreated)
            {
                _scheduleMarker.Begin();
                dependency = new ResetMetrics
                {
                    Metrics = _triggerMetrics
                }.Schedule(dependency);
                _scheduleMarker.End();
            }
#endif
            if (!active)
            {
                if (_triggerWasActive)
                {
                    // 購読がなくなった時点で履歴と対象キャッシュを解放します。
                    dependency.Complete();
                    _triggerPrevious.Dispose();
                    _triggerCurrent.Dispose();
                    _triggerRawSnapshot.Dispose();
                    _triggerEvents.Dispose();
                    _triggerSingleTarget.Dispose();
                    if (_triggerTargets.IsCreated)
                    {
                        _triggerTargets.Dispose();
                    }

#if PHYSICS_EVENTS_PROFILING
                    _triggerStatistics.Dispose();
#endif
                    _triggerWasActive = false;
                }

                if (!_triggerBuffers.IsEmptyIgnoreFilter)
                {
                    var clear = new ClearTriggerBuffers
                    {
                        Enter = state.GetBufferTypeHandle<TriggerEnter>(),
                        Stay = state.GetBufferTypeHandle<TriggerStay>(),
                        Exit = state.GetBufferTypeHandle<TriggerExit>()
                    };
#if PHYSICS_EVENTS_PROFILING
                    _scheduleMarker.Begin();
#endif
                    dependency = ParallelWrites &&
                        _triggerBuffers.CalculateChunkCountWithoutFiltering() > 1 ? clear.ScheduleParallel(_triggerBuffers, dependency) : clear.Schedule(_triggerBuffers, dependency);
#if PHYSICS_EVENTS_PROFILING
                    _scheduleMarker.End();
#endif
#if PHYSICS_EVENTS_PROFILING
                    BufferWriteSchedules++;
#endif
                }

                return;
            }

#if PHYSICS_EVENTS_PROFILING
            if (!_triggerMetrics.IsCreated)
            {
                _triggerMetrics = new NativeReference<PhysicsEventMetrics>(Allocator.Persistent);
            }
#endif
            if (!_triggerPrevious.IsCreated)
            {
                _triggerPrevious = new NativeList<DirectedContact>(16, Allocator.Persistent);
                _triggerCurrent = new NativeList<DirectedContact>(16, Allocator.Persistent);
                _triggerRawSnapshot = new NativeList<DirectedContact>(16, Allocator.Persistent);
                _triggerEvents = new NativeList<PhaseEvent>(16, Allocator.Persistent);
                _triggerSingleTarget = new NativeReference<SingleTarget>(Allocator.Persistent);
            }

            int chunks = _triggerBuffers.CalculateChunkCountWithoutFiltering();
#if PHYSICS_EVENTS_PROFILING
            if (!_triggerStatistics.IsCreated ||
                _triggerStatistics.Length < chunks)
            {
                dependency.Complete();
                if (_triggerStatistics.IsCreated)
                {
                    _triggerStatistics.Dispose();
                }

                _triggerStatistics = new NativeArray<WriterStatistics>(chunks, Allocator.Persistent);
                var metrics = _triggerMetrics.Value;
                metrics.WriterStorageReallocations++;
                _triggerMetrics.Value = metrics;
            }
#endif
            int targets = _triggerSubscribers.CalculateEntityCount();
            bool single = targets == 1;
            if (!_triggerTargets.IsCreated ||
                _triggerTargets.Capacity < targets)
            {
                dependency.Complete();
                if (!_triggerTargets.IsCreated)
                {
                    _triggerTargets = new NativeParallelHashMap<Entity, TargetState>(targets, Allocator.Persistent);
                }
                else
                {
                    _triggerTargets.Capacity = targets;
#if PHYSICS_EVENTS_PROFILING
                    var metrics = _triggerMetrics.Value;
                    metrics.TargetCacheReallocations++;
                    _triggerMetrics.Value = metrics;
#endif
                }
            }

            if (!single)
            {
#if PHYSICS_EVENTS_PROFILING
                _scheduleMarker.Begin();
#endif
                dependency = new PrepareFrame
                {
                    Contacts = _triggerCurrent,
                    Targets = _triggerTargets,
                    Events = _triggerEvents
                }.Schedule(dependency);
#if PHYSICS_EVENTS_PROFILING
                _scheduleMarker.End();
#endif
            }

#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.Begin();
#endif
            dependency = new GatherTriggerTargets
            {
                EntityType = state.GetEntityTypeHandle(),
                Enter = state.GetBufferTypeHandle<TriggerEnter>(true),
                Stay = state.GetBufferTypeHandle<TriggerStay>(true),
                Exit = state.GetBufferTypeHandle<TriggerExit>(true),
                Targets = _triggerTargets,
                Single = single,
                Receiver = _triggerSingleTarget,
                Contacts = _triggerCurrent,
                Events = _triggerEvents
            }.Schedule(_triggerSubscribers, dependency);
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.End();
#endif
            var collect = new CollectTrigger
            {
                Contacts = _triggerCurrent,
                Targets = _triggerTargets,
                Single = single,
                Receiver = _triggerSingleTarget
            };
#if PHYSICS_EVENTS_PROFILING
            collect.Metrics = _triggerMetrics;
            TriggerSchedules++;
#endif
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.Begin();
#endif
            dependency = collect.Schedule(simulation, dependency);
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.End();
#endif
            var build = new BuildNotifications
            {
                Previous = _triggerPrevious,
                Current = _triggerCurrent,
                RawSnapshot = _triggerRawSnapshot,
                ReuseUnchanged = ReuseUnchangedContacts,
                Events = _triggerEvents,
                Targets = _triggerTargets,
                Single = single,
                Receiver = _triggerSingleTarget
            };
#if PHYSICS_EVENTS_PROFILING
            build.Metrics = _triggerMetrics;
            build.Collision = false;
#endif
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.Begin();
#endif
            dependency = build.Schedule(dependency);
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.End();
#endif
            var write = new WriteTriggerBuffers
            {
                EntityType = state.GetEntityTypeHandle(),
                Disabled = state.GetComponentTypeHandle<Disabled>(true),
                Events = _triggerEvents,
                Targets = _triggerTargets,
                Single = single,
                Receiver = _triggerSingleTarget,
#if PHYSICS_EVENTS_PROFILING
                Statistics = _triggerStatistics,
#endif
                Enter = state.GetBufferTypeHandle<TriggerEnter>(),
                Stay = state.GetBufferTypeHandle<TriggerStay>(),
                Exit = state.GetBufferTypeHandle<TriggerExit>()
            };
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.Begin();
#endif
            dependency = ParallelWrites &&
                chunks > 1 ? write.ScheduleParallel(_triggerBuffers, dependency) : write.Schedule(_triggerBuffers, dependency);
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.End();
#endif
#if PHYSICS_EVENTS_PROFILING
            BufferWriteSchedules++;
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.Begin();
#endif
            dependency = new SumStatistics
            {
                Statistics = _triggerStatistics,
                Count = chunks,
                Metrics = _triggerMetrics
            }.Schedule(dependency);
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.End();
#endif
#endif
            _triggerWasActive = true;
        }

        private void UpdateCollision(
            ref SystemState state,
            bool active,
            SimulationSingleton simulation,
            ref JobHandle dependency)
        {
#if PHYSICS_EVENTS_PROFILING
            if (active)
            {
                CollisionUpdateInvocations++;
                bool managed = false;
                DetectManagedUpdate(ref managed);
                if (managed)
                {
                    ManagedCollisionUpdateInvocations++;
                }
            }
            if (_collisionMetrics.IsCreated)
            {
                _scheduleMarker.Begin();
                dependency = new ResetMetrics
                {
                    Metrics = _collisionMetrics
                }.Schedule(dependency);
                _scheduleMarker.End();
            }
#endif
            if (!active)
            {
                if (_collisionWasActive)
                {
                    // 購読がなくなった時点で履歴と対象キャッシュを解放します。
                    dependency.Complete();
                    _collisionPrevious.Dispose();
                    _collisionCurrent.Dispose();
                    _collisionRawSnapshot.Dispose();
                    _collisionEvents.Dispose();
                    _collisionSingleTarget.Dispose();
                    if (_collisionTargets.IsCreated)
                    {
                        _collisionTargets.Dispose();
                    }

#if PHYSICS_EVENTS_PROFILING
                    _collisionStatistics.Dispose();
#endif
                    _collisionWasActive = false;
                }

                if (!_collisionBuffers.IsEmptyIgnoreFilter)
                {
                    var clear = new ClearCollisionBuffers
                    {
                        Enter = state.GetBufferTypeHandle<CollisionEnter>(),
                        Stay = state.GetBufferTypeHandle<CollisionStay>(),
                        Exit = state.GetBufferTypeHandle<CollisionExit>()
                    };
#if PHYSICS_EVENTS_PROFILING
                    _scheduleMarker.Begin();
#endif
                    dependency = ParallelWrites &&
                        _collisionBuffers.CalculateChunkCountWithoutFiltering() > 1 ? clear.ScheduleParallel(_collisionBuffers, dependency) : clear.Schedule(_collisionBuffers, dependency);
#if PHYSICS_EVENTS_PROFILING
                    _scheduleMarker.End();
#endif
#if PHYSICS_EVENTS_PROFILING
                    BufferWriteSchedules++;
#endif
                }

                return;
            }

#if PHYSICS_EVENTS_PROFILING
            if (!_collisionMetrics.IsCreated)
            {
                _collisionMetrics = new NativeReference<PhysicsEventMetrics>(Allocator.Persistent);
            }
#endif
            if (!_collisionPrevious.IsCreated)
            {
                _collisionPrevious = new NativeList<DirectedContact>(16, Allocator.Persistent);
                _collisionCurrent = new NativeList<DirectedContact>(16, Allocator.Persistent);
                _collisionRawSnapshot = new NativeList<DirectedContact>(16, Allocator.Persistent);
                _collisionEvents = new NativeList<PhaseEvent>(16, Allocator.Persistent);
                _collisionSingleTarget = new NativeReference<SingleTarget>(Allocator.Persistent);
            }

            int chunks = _collisionBuffers.CalculateChunkCountWithoutFiltering();
#if PHYSICS_EVENTS_PROFILING
            if (!_collisionStatistics.IsCreated ||
                _collisionStatistics.Length < chunks)
            {
                dependency.Complete();
                if (_collisionStatistics.IsCreated)
                {
                    _collisionStatistics.Dispose();
                }

                _collisionStatistics = new NativeArray<WriterStatistics>(chunks, Allocator.Persistent);
                var metrics = _collisionMetrics.Value;
                metrics.WriterStorageReallocations++;
                _collisionMetrics.Value = metrics;
            }
#endif
            int targets = _collisionSubscribers.CalculateEntityCount();
            bool single = targets == 1;
            if (!_collisionTargets.IsCreated ||
                _collisionTargets.Capacity < targets)
            {
                dependency.Complete();
                if (!_collisionTargets.IsCreated)
                {
                    _collisionTargets = new NativeParallelHashMap<Entity, TargetState>(targets, Allocator.Persistent);
                }
                else
                {
                    _collisionTargets.Capacity = targets;
#if PHYSICS_EVENTS_PROFILING
                    var metrics = _collisionMetrics.Value;
                    metrics.TargetCacheReallocations++;
                    _collisionMetrics.Value = metrics;
#endif
                }
            }

            if (!single)
            {
#if PHYSICS_EVENTS_PROFILING
                _scheduleMarker.Begin();
#endif
                dependency = new PrepareFrame
                {
                    Contacts = _collisionCurrent,
                    Targets = _collisionTargets,
                    Events = _collisionEvents
                }.Schedule(dependency);
#if PHYSICS_EVENTS_PROFILING
                _scheduleMarker.End();
#endif
            }

#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.Begin();
#endif
            dependency = new GatherCollisionTargets
            {
                EntityType = state.GetEntityTypeHandle(),
                Enter = state.GetBufferTypeHandle<CollisionEnter>(true),
                Stay = state.GetBufferTypeHandle<CollisionStay>(true),
                Exit = state.GetBufferTypeHandle<CollisionExit>(true),
                Targets = _collisionTargets,
                Single = single,
                Receiver = _collisionSingleTarget,
                Contacts = _collisionCurrent,
                Events = _collisionEvents
            }.Schedule(_collisionSubscribers, dependency);
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.End();
#endif
            var collect = new CollectCollision
            {
                Contacts = _collisionCurrent,
                Targets = _collisionTargets,
                Single = single,
                Receiver = _collisionSingleTarget
            };
#if PHYSICS_EVENTS_PROFILING
            collect.Metrics = _collisionMetrics;
            CollisionSchedules++;
#endif
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.Begin();
#endif
            dependency = collect.Schedule(simulation, dependency);
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.End();
#endif
            var build = new BuildNotifications
            {
                Previous = _collisionPrevious,
                Current = _collisionCurrent,
                RawSnapshot = _collisionRawSnapshot,
                ReuseUnchanged = ReuseUnchangedContacts,
                Events = _collisionEvents,
                Targets = _collisionTargets,
                Single = single,
                Receiver = _collisionSingleTarget
            };
#if PHYSICS_EVENTS_PROFILING
            build.Metrics = _collisionMetrics;
            build.Collision = true;
#endif
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.Begin();
#endif
            dependency = build.Schedule(dependency);
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.End();
#endif
            var write = new WriteCollisionBuffers
            {
                EntityType = state.GetEntityTypeHandle(),
                Disabled = state.GetComponentTypeHandle<Disabled>(true),
                Events = _collisionEvents,
                Targets = _collisionTargets,
                Single = single,
                Receiver = _collisionSingleTarget,
#if PHYSICS_EVENTS_PROFILING
                Statistics = _collisionStatistics,
#endif
                Enter = state.GetBufferTypeHandle<CollisionEnter>(),
                Stay = state.GetBufferTypeHandle<CollisionStay>(),
                Exit = state.GetBufferTypeHandle<CollisionExit>()
            };
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.Begin();
#endif
            dependency = ParallelWrites &&
                chunks > 1 ? write.ScheduleParallel(_collisionBuffers, dependency) : write.Schedule(_collisionBuffers, dependency);
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.End();
#endif
#if PHYSICS_EVENTS_PROFILING
            BufferWriteSchedules++;
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.Begin();
#endif
            dependency = new SumStatistics
            {
                Statistics = _collisionStatistics,
                Count = chunks,
                Metrics = _collisionMetrics
            }.Schedule(dependency);
#if PHYSICS_EVENTS_PROFILING
            _scheduleMarker.End();
#endif
#endif
            _collisionWasActive = true;
        }

        [BurstCompile]
        private struct CollectTrigger : ITriggerEventsJob
        {
            [ReadOnly]
            public NativeParallelHashMap<Entity, TargetState> Targets;
            public bool Single;
            [ReadOnly]
            public NativeReference<SingleTarget> Receiver;
            public NativeList<DirectedContact> Contacts;
#if PHYSICS_EVENTS_PROFILING
            public NativeReference<PhysicsEventMetrics> Metrics;
#endif
            public void Execute(TriggerEvent contact)
            {
#if PHYSICS_EVENTS_PROFILING
                MarkManaged(Metrics);
#endif
                Add(contact.EntityA, contact.EntityB);
                if (contact.EntityA != contact.EntityB)
                {
                    Add(contact.EntityB, contact.EntityA);
                }
            }

            private void Add(Entity self, Entity other)
            {
                if (Single ? self != Receiver.Value.Entity : !Targets.ContainsKey(self))
                {
                    return;
                }

#if PHYSICS_EVENTS_PROFILING
                if (Contacts.Length == Contacts.Capacity)
                {
                    var metrics = Metrics.Value;
                    metrics.ContactReallocations++;
                    Metrics.Value = metrics;
                }
#endif
                Contacts.Add(new DirectedContact
                {
                    Self = self,
                    Other = other
                });
            }
        }

        [BurstCompile]
        private struct ClearTriggerBuffers : IJobChunk
        {
            public BufferTypeHandle<TriggerEnter> Enter;
            public BufferTypeHandle<TriggerStay> Stay;
            public BufferTypeHandle<TriggerExit> Exit;
            public void Execute(
                in ArchetypeChunk chunk,
                int index,
                bool useEnabledMask,
                in v128 enabledMask)
            {
                if (chunk.Has(ref Enter))
                {
                    var buffers = chunk.GetBufferAccessor(ref Enter);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        buffers[i].Clear();
                    }
                }

                if (chunk.Has(ref Stay))
                {
                    var buffers = chunk.GetBufferAccessor(ref Stay);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        buffers[i].Clear();
                    }
                }

                if (chunk.Has(ref Exit))
                {
                    var buffers = chunk.GetBufferAccessor(ref Exit);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        buffers[i].Clear();
                    }
                }
            }
        }

        // 各チャンクは自分のEntityだけを書き込みます。任意EntityへのLookup書き込みは行いません。
        [BurstCompile]
        private struct WriteTriggerBuffers : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle EntityType;
            [ReadOnly]
            public ComponentTypeHandle<Disabled> Disabled;
            [ReadOnly]
            public NativeList<PhaseEvent> Events;
            [ReadOnly]
            public NativeParallelHashMap<Entity, TargetState> Targets;
            public bool Single;
            [ReadOnly]
            public NativeReference<SingleTarget> Receiver;
#if PHYSICS_EVENTS_PROFILING
            public NativeArray<WriterStatistics> Statistics;
#endif
            public BufferTypeHandle<TriggerEnter> Enter;
            public BufferTypeHandle<TriggerStay> Stay;
            public BufferTypeHandle<TriggerExit> Exit;
            public void Execute(
                in ArchetypeChunk chunk,
                int index,
                bool useEnabledMask,
                in v128 enabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                var receiver = Single ? Receiver.Value : default;
                bool disabled = chunk.Has(ref Disabled);
#if PHYSICS_EVENTS_PROFILING
                var statistics = new WriterStatistics();
                MarkManaged(ref statistics);
#endif
                bool hasEnter = chunk.Has(ref Enter);
                BufferAccessor<TriggerEnter> enter = default;
                if (hasEnter)
                {
                    enter = chunk.GetBufferAccessor(ref Enter);
                }

                bool hasStay = chunk.Has(ref Stay);
                BufferAccessor<TriggerStay> stay = default;
                if (hasStay)
                {
                    stay = chunk.GetBufferAccessor(ref Stay);
                }

                bool hasExit = chunk.Has(ref Exit);
                BufferAccessor<TriggerExit> exit = default;
                if (hasExit)
                {
                    exit = chunk.GetBufferAccessor(ref Exit);
                }

                for (int i = 0; i < chunk.Count; i++)
                {
                    if (hasEnter)
                    {
                        enter[i].Clear();
                    }

                    if (hasStay)
                    {
                        stay[i].Clear();
                    }

                    if (hasExit)
                    {
                        exit[i].Clear();
                    }

                    if (disabled)
                    {
                        continue;
                    }

                    TargetState target;
                    if (Single)
                    {
                        if (entities[i] != receiver.Entity)
                        {
                            continue;
                        }

                        // 単一受信者向けの通知だけが並ぶため、範囲検索も不要です。
                        target = new TargetState { Count = Events.Length };
                    }
                    else if (!Targets.TryGetValue(entities[i], out target))
                    {
                        continue;
                    }

                    for (int e = target.Start; e < target.Start + target.Count; e++)
                    {
                        var contact = Events[e];
                        byte phase = contact.Phase;
                        if (phase == 1)
                        {
                            var buffer = enter[i];
#if PHYSICS_EVENTS_PROFILING
                            if (buffer.Length == buffer.Capacity)
                            {
                                statistics.BufferReallocations++;
                            }
#endif
                            buffer.Add(new TriggerEnter { Other = contact.Other });
#if PHYSICS_EVENTS_PROFILING
                            statistics.EventsWritten++;
#endif
                        }
                        else if (phase == 2)
                        {
                            var buffer = stay[i];
#if PHYSICS_EVENTS_PROFILING
                            if (buffer.Length == buffer.Capacity)
                            {
                                statistics.BufferReallocations++;
                            }
#endif
                            buffer.Add(new TriggerStay { Other = contact.Other });
#if PHYSICS_EVENTS_PROFILING
                            statistics.EventsWritten++;
#endif
                        }
                        else if (phase == 4)
                        {
                            var buffer = exit[i];
#if PHYSICS_EVENTS_PROFILING
                            if (buffer.Length == buffer.Capacity)
                            {
                                statistics.BufferReallocations++;
                            }
#endif
                            buffer.Add(new TriggerExit { Other = contact.Other });
#if PHYSICS_EVENTS_PROFILING
                            statistics.EventsWritten++;
#endif
                        }
                    }
                }
#if PHYSICS_EVENTS_PROFILING
                Statistics[index] = statistics;
#endif
            }
        }

        [BurstCompile]
        private struct CollectCollision : ICollisionEventsJob
        {
            [ReadOnly]
            public NativeParallelHashMap<Entity, TargetState> Targets;
            public bool Single;
            [ReadOnly]
            public NativeReference<SingleTarget> Receiver;
            public NativeList<DirectedContact> Contacts;
#if PHYSICS_EVENTS_PROFILING
            public NativeReference<PhysicsEventMetrics> Metrics;
#endif
            public void Execute(CollisionEvent contact)
            {
#if PHYSICS_EVENTS_PROFILING
                MarkManaged(Metrics);
#endif
                Add(contact.EntityA, contact.EntityB);
                if (contact.EntityA != contact.EntityB)
                {
                    Add(contact.EntityB, contact.EntityA);
                }
            }

            private void Add(Entity self, Entity other)
            {
                if (Single ? self != Receiver.Value.Entity : !Targets.ContainsKey(self))
                {
                    return;
                }

#if PHYSICS_EVENTS_PROFILING
                if (Contacts.Length == Contacts.Capacity)
                {
                    var metrics = Metrics.Value;
                    metrics.ContactReallocations++;
                    Metrics.Value = metrics;
                }
#endif
                Contacts.Add(new DirectedContact
                {
                    Self = self,
                    Other = other
                });
            }
        }

        [BurstCompile]
        private struct ClearCollisionBuffers : IJobChunk
        {
            public BufferTypeHandle<CollisionEnter> Enter;
            public BufferTypeHandle<CollisionStay> Stay;
            public BufferTypeHandle<CollisionExit> Exit;
            public void Execute(
                in ArchetypeChunk chunk,
                int index,
                bool useEnabledMask,
                in v128 enabledMask)
            {
                if (chunk.Has(ref Enter))
                {
                    var buffers = chunk.GetBufferAccessor(ref Enter);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        buffers[i].Clear();
                    }
                }

                if (chunk.Has(ref Stay))
                {
                    var buffers = chunk.GetBufferAccessor(ref Stay);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        buffers[i].Clear();
                    }
                }

                if (chunk.Has(ref Exit))
                {
                    var buffers = chunk.GetBufferAccessor(ref Exit);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        buffers[i].Clear();
                    }
                }
            }
        }

        // 各チャンクは自分のEntityだけを書き込みます。任意EntityへのLookup書き込みは行いません。
        [BurstCompile]
        private struct WriteCollisionBuffers : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle EntityType;
            [ReadOnly]
            public ComponentTypeHandle<Disabled> Disabled;
            [ReadOnly]
            public NativeList<PhaseEvent> Events;
            [ReadOnly]
            public NativeParallelHashMap<Entity, TargetState> Targets;
            public bool Single;
            [ReadOnly]
            public NativeReference<SingleTarget> Receiver;
#if PHYSICS_EVENTS_PROFILING
            public NativeArray<WriterStatistics> Statistics;
#endif
            public BufferTypeHandle<CollisionEnter> Enter;
            public BufferTypeHandle<CollisionStay> Stay;
            public BufferTypeHandle<CollisionExit> Exit;
            public void Execute(
                in ArchetypeChunk chunk,
                int index,
                bool useEnabledMask,
                in v128 enabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                var receiver = Single ? Receiver.Value : default;
                bool disabled = chunk.Has(ref Disabled);
#if PHYSICS_EVENTS_PROFILING
                var statistics = new WriterStatistics();
                MarkManaged(ref statistics);
#endif
                bool hasEnter = chunk.Has(ref Enter);
                BufferAccessor<CollisionEnter> enter = default;
                if (hasEnter)
                {
                    enter = chunk.GetBufferAccessor(ref Enter);
                }

                bool hasStay = chunk.Has(ref Stay);
                BufferAccessor<CollisionStay> stay = default;
                if (hasStay)
                {
                    stay = chunk.GetBufferAccessor(ref Stay);
                }

                bool hasExit = chunk.Has(ref Exit);
                BufferAccessor<CollisionExit> exit = default;
                if (hasExit)
                {
                    exit = chunk.GetBufferAccessor(ref Exit);
                }

                for (int i = 0; i < chunk.Count; i++)
                {
                    if (hasEnter)
                    {
                        enter[i].Clear();
                    }

                    if (hasStay)
                    {
                        stay[i].Clear();
                    }

                    if (hasExit)
                    {
                        exit[i].Clear();
                    }

                    if (disabled)
                    {
                        continue;
                    }

                    TargetState target;
                    if (Single)
                    {
                        if (entities[i] != receiver.Entity)
                        {
                            continue;
                        }

                        // 単一受信者向けの通知だけが並ぶため、範囲検索も不要です。
                        target = new TargetState { Count = Events.Length };
                    }
                    else if (!Targets.TryGetValue(entities[i], out target))
                    {
                        continue;
                    }

                    for (int e = target.Start; e < target.Start + target.Count; e++)
                    {
                        var contact = Events[e];
                        byte phase = contact.Phase;
                        if (phase == 1)
                        {
                            var buffer = enter[i];
#if PHYSICS_EVENTS_PROFILING
                            if (buffer.Length == buffer.Capacity)
                            {
                                statistics.BufferReallocations++;
                            }
#endif
                            buffer.Add(new CollisionEnter { Other = contact.Other });
#if PHYSICS_EVENTS_PROFILING
                            statistics.EventsWritten++;
#endif
                        }
                        else if (phase == 2)
                        {
                            var buffer = stay[i];
#if PHYSICS_EVENTS_PROFILING
                            if (buffer.Length == buffer.Capacity)
                            {
                                statistics.BufferReallocations++;
                            }
#endif
                            buffer.Add(new CollisionStay { Other = contact.Other });
#if PHYSICS_EVENTS_PROFILING
                            statistics.EventsWritten++;
#endif
                        }
                        else if (phase == 4)
                        {
                            var buffer = exit[i];
#if PHYSICS_EVENTS_PROFILING
                            if (buffer.Length == buffer.Capacity)
                            {
                                statistics.BufferReallocations++;
                            }
#endif
                            buffer.Add(new CollisionExit { Other = contact.Other });
#if PHYSICS_EVENTS_PROFILING
                            statistics.EventsWritten++;
#endif
                        }
                    }
                }
#if PHYSICS_EVENTS_PROFILING
                Statistics[index] = statistics;
#endif
            }
        }

        internal static bool SameContacts(NativeArray<DirectedContact> current, NativeArray<DirectedContact> previousRaw)
        {
            if (current.Length != previousRaw.Length)
            {
                return false;
            }

            for (int i = 0; i < current.Length; i++)
            {
                if (current[i].Self != previousRaw[i].Self ||
                    current[i].Other != previousRaw[i].Other)
                {
                    return false;
                }
            }

            return true;
        }

        private struct SingleTarget
        {
            public Entity Entity;
            public byte Phases;
        }

        private struct TargetState
        {
            public int Start;
            public int Count;
            public byte Phases;
        }

        private struct PhaseEvent
        {
            public Entity Other;
            public byte Phase;
        }

        [BurstCompile]
        private struct PrepareFrame : IJob
        {
            public NativeList<DirectedContact> Contacts;
            public NativeParallelHashMap<Entity, TargetState> Targets;
            public NativeList<PhaseEvent> Events;
            public void Execute()
            {
                Contacts.Clear();
                Targets.Clear();
                Events.Clear();
            }
        }

        [BurstCompile]
        private struct BuildNotifications : IJob
        {
            public NativeList<DirectedContact> Previous;
            public NativeList<DirectedContact> Current;
            public NativeList<DirectedContact> RawSnapshot;
            public bool ReuseUnchanged;
            public NativeParallelHashMap<Entity, TargetState> Targets;
            public bool Single;
            [ReadOnly]
            public NativeReference<SingleTarget> Receiver;
            public NativeList<PhaseEvent> Events;
#if PHYSICS_EVENTS_PROFILING
            public bool Collision;
            public NativeReference<PhysicsEventMetrics> Metrics;
#endif
            public void Execute()
            {
#if PHYSICS_EVENTS_PROFILING
                MarkManaged(Metrics);
#endif
                bool unchanged = ReuseUnchanged &&
                    SameContacts(Current.AsArray(), RawSnapshot.AsArray());
                if (!unchanged)
                {
                    if (ReuseUnchanged)
                    {
#if PHYSICS_EVENTS_PROFILING
                        int capacity = RawSnapshot.Capacity;
#endif
                        RawSnapshot.CopyFrom(Current.AsArray());
#if PHYSICS_EVENTS_PROFILING
                        if (RawSnapshot.Capacity != capacity)
                        {
                            var snapshotMetrics = Metrics.Value;
                            snapshotMetrics.SnapshotReallocations++;
                            Metrics.Value = snapshotMetrics;
                        }
#endif
                    }

                    Current.Sort();
                    int unique = 0;
                    for (int i = 0; i < Current.Length; i++)
                    {
                        if (unique == 0 ||
                            Current[i].CompareTo(Current[unique - 1]) != 0)
                        {
                            Current[unique++] = Current[i];
                        }
                    }

                    Current.ResizeUninitialized(unique);
                }

                // 完全一致時は確定済み履歴を直接読み、コピーと履歴交換を省きます。
                var contacts = unchanged ? Previous : Current;
#if PHYSICS_EVENTS_PROFILING
                var metrics = Metrics.Value;
                if (Collision)
                {
                    metrics.CollisionContacts = contacts.Length;
                }
                else
                {
                    metrics.TriggerContacts = contacts.Length;
                }
                if (unchanged)
                {
                    metrics.UnchangedContactFrames++;
                }
                Metrics.Value = metrics;
#endif
                var receiver = Single ? Receiver.Value : default;
                int old = 0;
                int next = 0;
                Entity self = Entity.Null;
                TargetState target = default;
                bool subscribed = false;
                int start = 0;
                while (old < Previous.Length ||
                    next < contacts.Length)
                {
                    int order = unchanged ? 0 : old >= Previous.Length ? 1 : next >= Current.Length ? -1 : Previous[old].CompareTo(Current[next]);
                    DirectedContact contact;
                    byte phase;
                    if (order < 0)
                    {
                        contact = Previous[old++];
                        phase = 4;
                    }
                    else if (order > 0)
                    {
                        contact = contacts[next++];
                        phase = 1;
                    }
                    else
                    {
                        contact = contacts[next++];
                        old++;
                        phase = 2;
                    }

                    if (contact.Self != self)
                    {
                        if (subscribed &&
                            !Single)
                        {
                            target.Start = start;
                            target.Count = Events.Length - start;
                            Targets[self] = target;
                        }

                        self = contact.Self;
                        if (Single)
                        {
                            subscribed = self == receiver.Entity;
                            target = new TargetState { Phases = receiver.Phases };
                        }
                        else
                        {
                            subscribed = Targets.TryGetValue(self, out target);
                        }
                        start = Events.Length;
                    }

                    if (subscribed &&
                        (target.Phases & phase) != 0)
                    {
#if PHYSICS_EVENTS_PROFILING
                        if (Events.Length == Events.Capacity)
                        {
                            metrics = Metrics.Value;
                            metrics.EventReallocations++;
                            Metrics.Value = metrics;
                        }
#endif
                        Events.Add(new PhaseEvent
                        {
                            Other = contact.Other,
                            Phase = phase
                        });
                    }
                }

                if (subscribed &&
                    !Single)
                {
                    target.Start = start;
                    target.Count = Events.Length - start;
                    Targets[self] = target;
                }

                if (!unchanged)
                {
#if PHYSICS_EVENTS_PROFILING
                    int capacity = Previous.Capacity;
#endif
                    // 差分通知の生成後にだけ、新しい確定済み履歴を反映します。
                    Previous.CopyFrom(Current.AsArray());
#if PHYSICS_EVENTS_PROFILING
                    if (Previous.Capacity != capacity)
                    {
                        metrics = Metrics.Value;
                        metrics.ContactReallocations++;
                        Metrics.Value = metrics;
                    }
#endif
                }
            }
        }

        [BurstCompile]
        private struct GatherTriggerTargets : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle EntityType;
            [ReadOnly]
            public BufferTypeHandle<TriggerEnter> Enter;
            [ReadOnly]
            public BufferTypeHandle<TriggerStay> Stay;
            [ReadOnly]
            public BufferTypeHandle<TriggerExit> Exit;
            public NativeParallelHashMap<Entity, TargetState> Targets;
            public bool Single;
            public NativeReference<SingleTarget> Receiver;
            public NativeList<DirectedContact> Contacts;
            public NativeList<PhaseEvent> Events;
            public void Execute(
                in ArchetypeChunk chunk,
                int index,
                bool useEnabledMask,
                in v128 enabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                bool hasEnter = chunk.Has(ref Enter);
                bool hasStay = chunk.Has(ref Stay);
                bool hasExit = chunk.Has(ref Exit);
                EnabledMask enter = hasEnter ? chunk.GetEnabledMask(ref Enter) : default;
                EnabledMask stay = hasStay ? chunk.GetEnabledMask(ref Stay) : default;
                EnabledMask exit = hasExit ? chunk.GetEnabledMask(ref Exit) : default;
                var iterator = new ChunkEntityEnumerator(
                    useEnabledMask,
                    enabledMask,
                    chunk.Count);
                while (iterator.NextEntityIndex(out int i))
                {
                    byte phases = (byte)((hasEnter &&
                        enter[i] ? 1 : 0) | (hasStay &&
                            stay[i] ? 2 : 0) | (hasExit &&
                                exit[i] ? 4 : 0));
                    if (Single)
                    {
                        // 有効受信者は全チャンクを通して1体だけなので、ここで準備も済ませます。
                        Contacts.Clear();
                        Events.Clear();
                        Receiver.Value = new SingleTarget
                        {
                            Entity = entities[i],
                            Phases = phases
                        };
                    }
                    else
                    {
                        Targets.TryAdd(entities[i], new TargetState { Phases = phases });
                    }
                }
            }
        }

        [BurstCompile]
        private struct GatherCollisionTargets : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle EntityType;
            [ReadOnly]
            public BufferTypeHandle<CollisionEnter> Enter;
            [ReadOnly]
            public BufferTypeHandle<CollisionStay> Stay;
            [ReadOnly]
            public BufferTypeHandle<CollisionExit> Exit;
            public NativeParallelHashMap<Entity, TargetState> Targets;
            public bool Single;
            public NativeReference<SingleTarget> Receiver;
            public NativeList<DirectedContact> Contacts;
            public NativeList<PhaseEvent> Events;
            public void Execute(
                in ArchetypeChunk chunk,
                int index,
                bool useEnabledMask,
                in v128 enabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                bool hasEnter = chunk.Has(ref Enter);
                bool hasStay = chunk.Has(ref Stay);
                bool hasExit = chunk.Has(ref Exit);
                EnabledMask enter = hasEnter ? chunk.GetEnabledMask(ref Enter) : default;
                EnabledMask stay = hasStay ? chunk.GetEnabledMask(ref Stay) : default;
                EnabledMask exit = hasExit ? chunk.GetEnabledMask(ref Exit) : default;
                var iterator = new ChunkEntityEnumerator(
                    useEnabledMask,
                    enabledMask,
                    chunk.Count);
                while (iterator.NextEntityIndex(out int i))
                {
                    byte phases = (byte)((hasEnter &&
                        enter[i] ? 1 : 0) | (hasStay &&
                            stay[i] ? 2 : 0) | (hasExit &&
                                exit[i] ? 4 : 0));
                    if (Single)
                    {
                        // 有効受信者は全チャンクを通して1体だけなので、ここで準備も済ませます。
                        Contacts.Clear();
                        Events.Clear();
                        Receiver.Value = new SingleTarget
                        {
                            Entity = entities[i],
                            Phases = phases
                        };
                    }
                    else
                    {
                        Targets.TryAdd(entities[i], new TargetState { Phases = phases });
                    }
                }
            }
        }
#if PHYSICS_EVENTS_PROFILING
        [BurstCompile]
        private struct ResetMetrics : IJob
        {
            public NativeReference<PhysicsEventMetrics> Metrics;
            public void Execute()
            {
                var metrics = Metrics.Value;
                metrics.TriggerContacts = 0;
                metrics.CollisionContacts = 0;
                Metrics.Value = metrics;
            }
        }
        [BurstCompile]
        private struct SumStatistics : IJob
        {
            [ReadOnly]
            public NativeArray<WriterStatistics> Statistics;
            public int Count;
            public NativeReference<PhysicsEventMetrics> Metrics;
            public void Execute()
            {
                var metrics = Metrics.Value;
                for (int i = 0; i < Count; i++)
                {
                    metrics.EventsWritten += Statistics[i].EventsWritten;
                    metrics.BufferReallocations += Statistics[i].BufferReallocations;
                    metrics.ManagedFallbacks += Statistics[i].ManagedFallbacks;
                }

                Metrics.Value = metrics;
            }
        }
        [BurstDiscard]
        private static void MarkManaged(NativeReference<PhysicsEventMetrics> reference)
        {
            var metrics = reference.Value;
            metrics.ManagedFallbacks++;
            reference.Value = metrics;
        }
        [BurstDiscard]
        private static void MarkManaged(ref WriterStatistics statistics)
        {
            statistics.ManagedFallbacks++;
        }
        private struct WriterStatistics
        {
            public long EventsWritten;
            public long BufferReallocations;
            public int ManagedFallbacks;
        }
#endif
    }

    /// <summary>
    /// 累積の容量拡張・配信回数と、直近の接触数です。
    /// </summary>
    public struct PhysicsEventMetrics
    {
        public long UnchangedContactFrames;
        public long SnapshotReallocations;
        public long ContactReallocations;
        public long BufferReallocations;
        public long EventReallocations;
        public long WriterStorageReallocations;
        public long TargetCacheReallocations;
        public long EventsWritten;
        public int TriggerContacts;
        public int CollisionContacts;
        public int ManagedFallbacks;
    }
}
