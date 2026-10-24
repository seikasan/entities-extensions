using Unity.Entities;
using System;
using UnityEngine;
using ManagedAsset = UnityEngine.InputSystem.InputActionAsset;
using UnityInputSystem = UnityEngine.InputSystem.InputSystem;

namespace InputSystem.Entities
{
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
    public partial class InputCollectSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// アセットごとに生成する収集Systemで共有するライフサイクルです。
    /// </summary>
    public abstract partial class InputCollectSystem<T> : SystemBase
        where T : unmanaged, IComponentData
    {
        private Entity _entity;
        private ManagedAsset _asset;
        private bool _ownsAsset;

        protected abstract string AssetGuid { get; }

        protected abstract void Initialize(ManagedAsset asset);
        protected abstract T ReadActions();

        protected override void OnCreate()
        {
            _entity = EntityManager.CreateEntity(typeof(T));
            EntityManager.SetName(_entity, "InputActionAsset");
        }

        protected override void OnStartRunning()
        {
            var registry = Resources.Load<InputActionAssetRegistry>(InputActionAssetRegistry.ResourceName);
            var source = registry != null
                ? registry.Find(AssetGuid)
                : null;

            if (source == null)
            {
                throw new InvalidOperationException("Input System for Entities: asset registry is missing or stale. Use Tools > Input System for Entities > Regenerate.");
            }

            // プロジェクト全体のアセットはUnityが所有し有効化します。MapとBindingの上書きを維持します。
            _ownsAsset = source != UnityInputSystem.actions;
            _asset = _ownsAsset ? UnityEngine.Object.Instantiate(source) : source;
            Initialize(_asset);
            if (_ownsAsset)
            {
                _asset.Enable();
            }
        }

        protected override void OnUpdate()
        {
            // この書き込みにより、前回のスナップショットを読み取る未完了Jobを完了させます。
            EntityManager.SetComponentData(_entity, ReadActions());
        }

        protected override void OnStopRunning()
        {
            ReleaseAsset();
            if (EntityManager.Exists(_entity))
            {
                EntityManager.SetComponentData(_entity, default(T));
            }
        }

        protected override void OnDestroy()
        {
            ReleaseAsset();
            if (EntityManager.Exists(_entity))
            {
                EntityManager.DestroyEntity(_entity);
            }
        }

        private void ReleaseAsset()
        {
            if (_ownsAsset &&
                _asset != null)
            {
                _asset.Disable();
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_asset);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_asset);
                }
            }

            _asset = null;
            _ownsAsset = false;
        }
    }
}
