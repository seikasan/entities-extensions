using System;
using Unity.Cinemachine;
using UnityEngine;

namespace GameObjectBridge.Entities.Cinemachine
{
    /// <summary>
    /// 表示用Prefab内またはシーン内のCinemachineCameraを、Entityの表示用GameObjectへ追従させます。
    /// </summary>
    [AddComponentMenu("Entities/GameObject Bridge/Cinemachine Setup")]
    public sealed class CinemachineBridgeSetup : MonoBehaviour, IGameObjectBridgeSetup
    {
        [Tooltip("シーン内カメラの識別IDです。空欄の場合は表示用Prefab内のCinemachineCameraを使用します。")]
        [SerializeField]
        private string _sceneCameraId = "";

        [Tooltip("Scene Camera Idが空欄のときに使用します。未指定なら同じGameObjectのCinemachineCameraを使用します。")]
        [SerializeField]
        private CinemachineCamera _camera;

        [SerializeField]
        private bool _follow = true;

        [SerializeField]
        private bool _lookAt = true;

        private EntityGameObjectBridge _bridge;
        private bool _registered;

        public string SceneCameraId => _sceneCameraId;
        public CinemachineCamera Camera => _camera;
        public bool Follow => _follow;
        public bool LookAt => _lookAt;
        internal EntityGameObjectBridge Bridge => _bridge;

        public void Configure(EntityGameObjectBridge bridge)
        {
            if (bridge == null || !bridge.IsBound)
            {
                throw new ArgumentException("有効なGameObject Bridgeの関連付けが必要です。", nameof(bridge));
            }

            // 設定をやり直す前に、以前のシーン内カメラとの関連付けを解除します。
            DetachSource();
            if (string.IsNullOrWhiteSpace(_sceneCameraId))
            {
                var camera = Camera != null ? Camera : GetComponent<CinemachineCamera>();
                if (camera == null)
                {
                    throw new InvalidOperationException(
                        "表示用PrefabのCinemachineCameraを指定するか、Scene Camera Idを設定してください。");
                }

                if (Follow)
                {
                    camera.FollowEntity(bridge);
                }

                if (LookAt)
                {
                    camera.LookAtEntity(bridge);
                }

                return;
            }

            _bridge = bridge;
            bridge.Unbound += OnBridgeUnbound;
            RegisterSource();
        }

        private void RegisterSource()
        {
            if (_registered || _bridge == null || !_bridge.IsBound)
            {
                return;
            }

            _registered = true;
            CinemachineBridgeSceneRegistry.RegisterSource(this);
        }

        private void UnregisterSource()
        {
            if (!_registered)
            {
                return;
            }

            _registered = false;
            CinemachineBridgeSceneRegistry.UnregisterSource(this);
        }

        private void DetachSource()
        {
            UnregisterSource();
            if (_bridge != null)
            {
                _bridge.Unbound -= OnBridgeUnbound;
            }

            _bridge = null;
        }

        private void OnBridgeUnbound(EntityGameObjectBridge _) => DetachSource();

        private void OnEnable()
        {
            if (_bridge != null)
            {
                RegisterSource();
            }
        }

        private void OnDisable() => UnregisterSource();

        private void OnDestroy() => DetachSource();
    }
}
