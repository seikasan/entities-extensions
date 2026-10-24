using System;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameObjectBridge.Entities
{
    public enum TransformSyncDirection
    {
        EntityToGameObject,
        GameObjectToEntity
    }

    /// <summary>
    /// Entity破棄後の後始末まで保持する内部の関連付け情報です。手動で追加・複製しないでください。
    /// </summary>
    public struct GameObjectBridgeLink : ICleanupComponentData
    {
        public UnityObjectRef<EntityGameObjectBridge> Bridge;
        public ulong Version;
        internal UnityObjectRef<GameObject> SourcePrefab;
    }

    internal struct BridgeAlive : IComponentData
    {
    }

    /// <summary>
    /// 指定WorldのEntityとシーン内のGameObjectを、メインスレッドで1対1に関連付けます。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class EntityGameObjectBridge : MonoBehaviour
    {
        public World World { get; private set; }
        public Entity Entity { get; private set; }
        public TransformSyncDirection Direction { get; private set; }
        public bool SynchronizeScale { get; private set; }
        public bool DestroyGameObjectOnUnbind { get; private set; }
        public ulong BindingVersion { get; private set; }
        /// <summary>EntityまたはWorldの破棄を含め、関連付けが解除されたときに通知します。</summary>
        public event Action<EntityGameObjectBridge> Unbound;
        public bool LastSyncSucceeded { get; private set; }

        private bool _warned;
        private bool _disabledByEntity;
        private bool _activeBeforeDisable;
        public bool IsBound => World != null &&
            World.IsCreated &&
            World.EntityManager.Exists(Entity) &&
            World.EntityManager.HasComponent<BridgeAlive>(Entity);

        /// <summary>
        /// 既存のシーンオブジェクトを登録します。明示的に所有させない限り、解除時にも保持します。
        /// </summary>
        /// <remarks>
        /// LocalTransformとLocalToWorldが必要です。Unityのメインスレッドから呼び出してください。
        /// </remarks>
        public static EntityGameObjectBridge Bind(
            World world,
            Entity entity,
            GameObject gameObject,
            TransformSyncDirection direction = TransformSyncDirection.EntityToGameObject,
            bool synchronizeScale = false,
            bool destroyGameObjectOnUnbind = false)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world), "Worldがnullです。");
            }

            if (!world.IsCreated)
            {
                throw new ObjectDisposedException(nameof(world), "指定したWorldはすでに破棄されています。");
            }

            if (gameObject == null)
            {
                throw new ArgumentNullException(nameof(gameObject), "GameObjectがnullです。");
            }

            if (!gameObject.scene.IsValid())
            {
                throw new ArgumentException("Prefabアセットではなく、シーン内のGameObjectを指定してください。", nameof(gameObject));
            }

            if (direction != TransformSyncDirection.EntityToGameObject &&
                direction != TransformSyncDirection.GameObjectToEntity)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), "同期方向の指定が不正です。");
            }

            var manager = world.EntityManager;

            if (!manager.Exists(entity))
            {
                throw new ArgumentException("指定したWorldにEntityが存在しません。", nameof(entity));
            }

            if (manager.HasComponent<Prefab>(entity))
            {
                throw new ArgumentException("Entity Prefabではなく、生成済みのEntityを指定してください。", nameof(entity));
            }

            if (!manager.HasComponent<LocalTransform>(entity) ||
                !manager.HasComponent<LocalToWorld>(entity))
            {
                throw new ArgumentException("EntityにはLocalTransformとLocalToWorldが必要です。", nameof(entity));
            }

            if (synchronizeScale &&
                (gameObject.transform.parent != null ||
                    manager.HasComponent<Parent>(entity)))
            {
                throw new ArgumentException("Scaleの同期は親を持たないルートオブジェクトにのみ対応しています。");
            }

            if (manager.HasComponent<GameObjectBridgeLink>(entity))
            {
                if (TryGet(
                    world,
                    entity,
                    out _))
                {
                    throw new InvalidOperationException("このEntityはすでに関連付けられています。");
                }

                manager.RemoveComponent<GameObjectBridgeLink>(entity);
                manager.RemoveComponent<BridgeAlive>(entity);
            }

            var bridge = gameObject.GetComponent<EntityGameObjectBridge>();

            if (bridge != null &&
                bridge.World != null)
            {
                throw new InvalidOperationException("このGameObjectはすでに関連付けられています。再登録する前にUnbind()を呼び出してください。");
            }

            // 手動生成したWorldでも、World.Dispose時に関連付けを解放するSystemを用意します。
            world.GetOrCreateSystemManaged<GameObjectBridgeSystem>();
            world.GetOrCreateSystemManaged<GameObjectToEntitySystem>();
            if (bridge == null)
            {
                bridge = gameObject.AddComponent<EntityGameObjectBridge>();
            }

            bridge.World = world;
            bridge.Entity = entity;
            bridge.Direction = direction;
            bridge.SynchronizeScale = synchronizeScale;
            bridge.DestroyGameObjectOnUnbind = destroyGameObjectOnUnbind;
            bridge.BindingVersion++;
            bridge.LastSyncSucceeded = false;
            bridge._warned = false;
            manager.AddComponent<BridgeAlive>(entity);
            manager.AddComponentData(entity, new GameObjectBridgeLink
            {
                Bridge = bridge,
                Version = bridge.BindingVersion
            });

            return bridge;
        }

        /// <summary>
        /// World、Entity、世代番号を確認し、有効な関連付けを取得します。
        /// </summary>
        public static bool TryGet(
            World world,
            Entity entity,
            out EntityGameObjectBridge bridge)
        {
            bridge = null;

            if (world == null ||
                !world.IsCreated)
            {
                return false;
            }

            var manager = world.EntityManager;

            if (!manager.Exists(entity) ||
                !manager.HasComponent<BridgeAlive>(entity) ||
                !manager.HasComponent<GameObjectBridgeLink>(entity))
            {
                return false;
            }

            var link = manager.GetComponentData<GameObjectBridgeLink>(entity);
            var candidate = link.Bridge.Value;

            if (candidate == null ||
                candidate.World != world ||
                candidate.Entity != entity ||
                candidate.BindingVersion != link.Version)
            {
                return false;
            }

            bridge = candidate;

            return true;
        }

        /// <summary>
        /// 関連付けを解除します。destroyOwnedGameObjectがfalseでなければ、所有するGameObjectを破棄します。
        /// </summary>
        public void Unbind(bool destroyOwnedGameObject = true)
        {
            var world = World;
            var entity = Entity;
            if (world != null &&
                world.IsCreated &&
                world.EntityManager.Exists(entity))
            {
                var manager = world.EntityManager;
                if (manager.HasComponent<GameObjectBridgeLink>(entity))
                {
                    var link = manager.GetComponentData<GameObjectBridgeLink>(entity);
                    if (link.Bridge.Value == this &&
                        link.Version == BindingVersion)
                    {
                        manager.RemoveComponent<BridgeAlive>(entity);
                        manager.RemoveComponent<GameObjectBridgeLink>(entity);
                    }
                }
            }

            Detach(destroyOwnedGameObject);
        }

        /// <summary>
        /// この組を即時同期します。必要なTransformの依存関係を完了し、Entityからは現在のLocalToWorldを読み取ります。
        /// </summary>
        public bool Synchronize()
        {
            if (!IsBound)
            {
                return false;
            }

            return Direction == TransformSyncDirection.EntityToGameObject ? World.GetOrCreateSystemManaged<GameObjectBridgeSystem>().SynchronizeOne(this) : World.GetOrCreateSystemManaged<GameObjectToEntitySystem>().SynchronizeOne(this);
        }

        internal void SetSyncResult(bool success)
        {
            LastSyncSucceeded = success;
            if (!success &&
                !_warned)
            {
                Debug.LogWarning(
                    "GameObject Bridge: 未対応または不正なTransformのため同期をスキップしました。値が有限であること、親のScaleが正の一様値であること、Scale同期の対象が親を持たないルートであることを確認してください。",
                    this);
                _warned = true;
            }

            if (success)
            {
                _warned = false;
            }
        }

        internal void Detach(bool destroyOwnedGameObject)
        {
            bool destroy = destroyOwnedGameObject &&
                DestroyGameObjectOnUnbind;
            World = null;
            Entity = Entity.Null;
            BindingVersion++;
            LastSyncSucceeded = false;
            NotifyUnbound();

            if (!destroy)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(gameObject);
            }
            else
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        internal void SetEntityDisabled(bool disabled)
        {
            if (_disabledByEntity == disabled)
            {
                return;
            }

            _disabledByEntity = disabled;
            if (disabled)
            {
                _activeBeforeDisable = gameObject.activeSelf;
                gameObject.SetActive(false);
            }
            else
            {
                gameObject.SetActive(_activeBeforeDisable);
            }
        }

        private void NotifyUnbound()
        {
            var handlers = Unbound;
            Unbound = null;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<EntityGameObjectBridge> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        void OnDestroy()
        {
            NotifyUnbound();
            // 別のECSクエリが列挙中の可能性があるため、Unityのコールバック内では構造変更しません。
            // Worldの後始末用Systemが次回更新時に無効な参照を取り除きます。
            World = null;
            Entity = Entity.Null;
            BindingVersion++;
            LastSyncSucceeded = false;
        }
    }
}
