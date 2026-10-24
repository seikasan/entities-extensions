using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace GameObjectBridge.Entities.Samples
{
    /// <summary>
    /// Prefabを1つ指定すると、Entityの生成・破棄だけで表示も生成・破棄されるサンプルです。
    /// </summary>
    public sealed class AutomaticBridgeSample : MonoBehaviour
    {
        [SerializeField]
        private GameObject _viewPrefab;

        public GameObject ViewPrefab
        {
            get => _viewPrefab;
            set => _viewPrefab = value;
        }
        private World _world;
        private Entity _prefab;
        private Entity[] _instances;
        private float _elapsed;
        private void Start()
        {
            _world = World.DefaultGameObjectInjectionWorld;

            if (_world == null ||
                !_world.IsCreated ||
                ViewPrefab == null)
            {
                Debug.LogError("表示用Prefabと初期化済みのEntities Worldが必要です。", this);

                return;
            }

            var manager = _world.EntityManager;
            _prefab = manager.CreateEntity(
                typeof(Prefab),
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(GameObjectBridgePrefab));
            manager.SetComponentData(_prefab, LocalTransform.Identity);
            manager.SetComponentData(_prefab, new LocalToWorld { Value = float4x4.identity });
            manager.SetComponentData(_prefab, new GameObjectBridgePrefab { Prefab = ViewPrefab });
            _instances = new Entity[3];
            for (int i = 0; i < _instances.Length; i++)
            {
                var entity = manager.Instantiate(_prefab);
                manager.SetComponentData(entity, LocalTransform.FromPosition(
                    i * 2 - 2,
                    0,
                    0));
                _instances[i] = entity;
            }
        }

        private void Update()
        {
            if (_instances == null ||
                _world == null ||
                !_world.IsCreated)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            var manager = _world.EntityManager;
            for (int i = 0; i < _instances.Length; i++)
            {
                var entity = _instances[i];
                if (!manager.HasComponent<LocalTransform>(entity))
                {
                    continue;
                }

                if (_elapsed > 4 &&
                    i == 0)
                {
                    manager.DestroyEntity(entity);
                    continue;
                }

                manager.SetComponentData(entity, LocalTransform.FromPosition(
                    i * 2 - 2,
                    math.sin(_elapsed + i),
                    0));
            }
        }

        private void OnDestroy()
        {
            if (_world == null ||
                !_world.IsCreated ||
                _instances == null)
            {
                return;
            }

            var manager = _world.EntityManager;
            foreach (var entity in _instances)
            {
                if (manager.Exists(entity))
                {
                    manager.DestroyEntity(entity);
                }
            }

            if (manager.Exists(_prefab))
            {
                manager.DestroyEntity(_prefab);
            }
        }
    }
}
