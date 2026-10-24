using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameObjectBridge.Entities
{
    /// <summary>
    /// SimulationとEntityのTransform伝播より前にGameObjectを読み取ります。
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class GameObjectToEntitySystem : SystemBase
    {
        private ComponentLookup<LocalTransform> _transforms;
        private ComponentLookup<Parent> _parents;
        private ComponentLookup<PostTransformMatrix> _post;
        protected override void OnCreate()
        {
            _transforms = GetComponentLookup<LocalTransform>(true);
            _parents = GetComponentLookup<Parent>(true);
            _post = GetComponentLookup<PostTransformMatrix>(true);
        }

        public bool SynchronizeOne(EntityGameObjectBridge bridge)
        {
            EntityManager.CompleteDependencyBeforeRW<LocalTransform>();
            EntityManager.CompleteDependencyBeforeRO<Parent>();
            EntityManager.CompleteDependencyBeforeRO<PostTransformMatrix>();
            _transforms.Update(this);
            _parents.Update(this);
            _post.Update(this);
            bool result = BridgeTransformSynchronization.ToEntity(
                EntityManager,
                bridge,
                ref _transforms,
                ref _parents,
                ref _post);
            bridge.SetSyncResult(result);

            return result;
        }

        protected override void OnUpdate()
        {
            foreach (var (link, entity) in SystemAPI.Query<RefRO<GameObjectBridgeLink>>().WithAll<BridgeAlive>().WithEntityAccess())
            {
                var bridge = link.ValueRO.Bridge.Value;
                if (bridge != null &&
                    bridge.World == World &&
                    bridge.Entity == entity &&
                    bridge.BindingVersion == link.ValueRO.Version &&
                    bridge.Direction == TransformSyncDirection.GameObjectToEntity)
                {
                    SynchronizeOne(bridge);
                }
            }
        }
    }

    /// <summary>
    /// EntityのTransform更新とSimulation末尾のPlayback後、通常のMonoBehaviour.LateUpdate前に同期します。
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial class GameObjectBridgeSystem : SystemBase
    {
        private EntityQuery _cleanupQuery;
        private EntityQuery _prefabQuery;
        private readonly List<MonoBehaviour> _setupComponents = new List<MonoBehaviour>();
        private GameObject _inactiveRoot;
        private ComponentLookup<LocalToWorld> _worldTransforms;
        private ComponentLookup<Parent> _parents;
        private ComponentLookup<BridgeAlive> _alive;
        private ComponentLookup<Disabled> _disabled;
        private ComponentLookup<GameObjectBridgePrefab> _requests;
        protected override void OnCreate()
        {
            _worldTransforms = GetComponentLookup<LocalToWorld>(true);
            _parents = GetComponentLookup<Parent>(true);
            _alive = GetComponentLookup<BridgeAlive>(true);
            _disabled = GetComponentLookup<Disabled>(true);
            _requests = GetComponentLookup<GameObjectBridgePrefab>(true);
            _cleanupQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<GameObjectBridgeLink>() },
                Options = EntityQueryOptions.IncludeDisabledEntities
            });
            _prefabQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<GameObjectBridgePrefab>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<LocalToWorld>()
                },
                None = new[] { ComponentType.ReadOnly<GameObjectBridgeLink>() }
            });
        }

        public bool SynchronizeOne(EntityGameObjectBridge bridge)
        {
            EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
            bool result = BridgeTransformSynchronization.ToGameObject(EntityManager, bridge);
            bridge.SetSyncResult(result);

            return result;
        }

        protected override void OnUpdate()
        {
            // 一括同期では依存関係の完了とLookupの更新を1回にまとめます。
            EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
            EntityManager.CompleteDependencyBeforeRO<Parent>();
            EntityManager.CompleteDependencyBeforeRO<BridgeAlive>();
            EntityManager.CompleteDependencyBeforeRO<Disabled>();
            EntityManager.CompleteDependencyBeforeRO<GameObjectBridgePrefab>();
            _worldTransforms.Update(this);
            _parents.Update(this);
            _alive.Update(this);
            _disabled.Update(this);
            _requests.Update(this);
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (link, entity) in SystemAPI.Query<RefRO<GameObjectBridgeLink>>().WithOptions(EntityQueryOptions.IncludeDisabledEntities).WithEntityAccess())
            {
                var bridge = link.ValueRO.Bridge.Value;
                bool matches = bridge != null &&
                    bridge.World == World &&
                    bridge.Entity == entity &&
                    bridge.BindingVersion == link.ValueRO.Version;
                bool automatic = !link.ValueRO.SourcePrefab.Equals(default(UnityObjectRef<GameObject>));
                bool requestChanged = automatic &&
                    matches &&
                    (!_requests.TryGetComponent(entity, out var request) ||
                        !request.Prefab.Equals(link.ValueRO.SourcePrefab) ||
                        request.SynchronizeScale != bridge.SynchronizeScale);
                if (!_alive.HasComponent(entity) ||
                    !matches ||
                    requestChanged)
                {
                    if (matches)
                    {
                        bridge.Detach(true);
                    }

                    ecb.RemoveComponent<BridgeAlive>(entity);
                    ecb.RemoveComponent<GameObjectBridgeLink>(entity);
                    continue;
                }

                if (automatic)
                {
                    bridge.SetEntityDisabled(_disabled.HasComponent(entity));
                }

                if (bridge.Direction == TransformSyncDirection.EntityToGameObject &&
                    !_disabled.HasComponent(entity))
                {
                    bool synced = _worldTransforms.TryGetComponent(entity, out var transform) &&
                        BridgeTransformSynchronization.ToGameObject(
                            bridge,
                            transform.Value,
                            _parents.HasComponent(entity));
                    bridge.SetSyncResult(synced);
                }
            }

            ecb.Playback(EntityManager);
            SpawnViews();
        }

        private void SpawnViews()
        {
            if (_prefabQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            if (_inactiveRoot == null)
            {
                _inactiveRoot = new GameObject("GameObject Bridge initialization");
                _inactiveRoot.SetActive(false);
                if (Application.isPlaying)
                {
                    Object.DontDestroyOnLoad(_inactiveRoot);
                }
            }

            // BindとSDK設定は構造変更を伴うため、クエリの列挙を終えてから生成します。
            using var entities = _prefabQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (!EntityManager.HasComponent<GameObjectBridgePrefab>(entity))
                {
                    continue;
                }

                var request = EntityManager.GetComponentData<GameObjectBridgePrefab>(entity);
                var prefab = request.Prefab.Value;
                if (prefab == null)
                {
                    continue;
                }

                GameObject instance = null;
                bool activate;
                EntityGameObjectBridge bridge = null;
                try
                {
                    // 親も含めて非アクティブにし、Prefab内のAwakeより先に関連付けと同期を完了します。
                    instance = Object.Instantiate(
                        prefab,
                        _inactiveRoot.transform,
                        false);
                    activate = instance.activeSelf;
                    instance.SetActive(false);
                    instance.transform.SetParent(null, true);
                    bridge = EntityGameObjectBridge.Bind(
                        World,
                        entity,
                        instance,
                        synchronizeScale: request.SynchronizeScale,
                        destroyGameObjectOnUnbind: true);
                    var link = EntityManager.GetComponentData<GameObjectBridgeLink>(entity);
                    link.SourcePrefab = request.Prefab;
                    EntityManager.SetComponentData(entity, link);

                    if (!SynchronizeOne(bridge))
                    {
                        throw new InvalidOperationException("自動生成する表示用GameObjectには、有効な初期LocalToWorldが必要です。");
                    }
                }
                catch (Exception exception)
                {
                    if (bridge != null)
                    {
                        bridge.Unbind(false);
                    }

                    // 不正な設定を毎フレーム再生成しないよう、失敗した要求を外します。
                    EntityManager.RemoveComponent<GameObjectBridgePrefab>(entity);
                    if (instance != null)
                    {
                        if (Application.isPlaying)
                        {
                            Object.Destroy(instance);
                        }
                        else
                        {
                            Object.DestroyImmediate(instance);
                        }
                    }

                    Debug.LogException(exception);
                    continue;
                }

                instance.GetComponentsInChildren(true, _setupComponents);
                foreach (var component in _setupComponents)
                {
                    if (!(component is IGameObjectBridgeSetup setup) ||
                        !component.enabled)
                    {
                        continue;
                    }

                    try
                    {
                        setup.Configure(bridge);
                    }
                    catch (Exception exception)
                    {
                        // 1つのSDK設定の失敗で、他の表示や設定の初期化を止めません。
                        Debug.LogException(exception, component);
                    }
                }

                _setupComponents.Clear();
                instance.SetActive(activate);
            }
        }

        protected override void OnDestroy()
        {
            using var entities = _cleanupQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var link = EntityManager.GetComponentData<GameObjectBridgeLink>(entity);
                var bridge = link.Bridge.Value;
                if (bridge != null &&
                    bridge.World == World &&
                    bridge.Entity == entity &&
                    bridge.BindingVersion == link.Version)
                {
                    bridge.Detach(true);
                }
            }

            if (_inactiveRoot != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(_inactiveRoot);
                }
                else
                {
                    Object.DestroyImmediate(_inactiveRoot);
                }
            }
        }
    }
}
