using Unity.Cinemachine;
using UnityEngine;

namespace GameObjectBridge.Entities.Cinemachine
{
    /// <summary>
    /// シーンに存在するCinemachineCameraに追加し、表示用Prefabの
    /// CinemachineBridgeSetup.SceneCameraIdとIDを一致させて自動接続します。カメラは新規生成しません。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CinemachineCamera))]
    [AddComponentMenu("Entities/GameObject Bridge/Cinemachine Scene Target")]
    public sealed class CinemachineBridgeTarget : MonoBehaviour
    {
        [Tooltip("表示用PrefabのCinemachineBridgeSetupに設定したScene Camera Idと同じIDを指定してください。")]
        [SerializeField]
        private string _targetId = "Player";

        private CinemachineCamera _camera;
        private string _registeredId;
        private Transform _assigned;
        private Transform _previousFollow;
        private Transform _previousLookAt;
        private bool _assignedFollow;
        private bool _assignedLookAt;

        public string TargetId => _targetId;
        internal string RegisteredId => _registeredId;

        private void OnEnable()
        {
            _camera = GetComponent<CinemachineCamera>();
            _registeredId = _targetId?.Trim();
            if (string.IsNullOrEmpty(_registeredId))
            {
                Debug.LogWarning("Cinemachine Bridge: シーン内カメラのTarget Idが空欄です。IDを指定してください。", this);
                return;
            }

            CinemachineBridgeSceneRegistry.RegisterTarget(this);
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(_registeredId))
            {
                CinemachineBridgeSceneRegistry.UnregisterTarget(this);
                _registeredId = null;
            }
            ClearBinding();
        }

        private void OnDestroy() => OnDisable();

        internal void Assign(EntityGameObjectBridge bridge, bool follow, bool lookAt)
        {
            ClearBinding();
            if (!follow && !lookAt)
            {
                return;
            }

            _assigned = bridge.transform;
            _previousFollow = _camera.Follow;
            _previousLookAt = _camera.LookAt;

            if (follow)
            {
                _camera.FollowEntity(bridge);
                _assignedFollow = true;
            }

            if (lookAt)
            {
                _camera.LookAtEntity(bridge);
                _assignedLookAt = true;
            }
        }

        internal void ClearBinding()
        {
            if (_camera != null)
            {
                // 別のカメラ制御処理が設定を変更していた場合は、その変更を上書きしません。
                if (_assignedFollow && _camera.Follow == _assigned)
                {
                    _camera.Follow = _previousFollow;
                }

                if (_assignedLookAt && _camera.LookAt == _assigned)
                {
                    _camera.LookAt = _previousLookAt;
                }
            }

            _assigned = null;
            _previousFollow = null;
            _previousLookAt = null;
            _assignedFollow = false;
            _assignedLookAt = false;
        }
    }
}
