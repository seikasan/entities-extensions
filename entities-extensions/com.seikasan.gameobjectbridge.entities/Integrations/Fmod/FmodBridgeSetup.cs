using System;
using FMODUnity;
using UnityEngine;

namespace GameObjectBridge.Entities.Fmod
{
    /// <summary>
    /// 表示用Prefabに保存したFMODイベントを生成し、Entityとともに所有します。
    /// </summary>
    [AddComponentMenu("Entities/GameObject Bridge/FMOD Setup")]
    public sealed class FmodBridgeSetup : MonoBehaviour, IGameObjectBridgeSetup
    {
        [Tooltip("同じEntity内で一意の対象IDです。既定のコマンドは0に送られます。")]
        [SerializeField]
        [Min(0)]
        private int _targetId;
        [SerializeField]
        private EventReference _event;
        [SerializeField]
        private bool _playOnSpawn = true;
        public EventReference Event => _event;
        public bool PlayOnSpawn => _playOnSpawn;
        public int TargetId => _targetId;

        public void Configure(EntityGameObjectBridge bridge)
        {
            var manager = bridge.World.EntityManager;
            if (manager.HasComponent<FmodBridgeInstance>(bridge.Entity))
            {
                var existing = manager.GetComponentObject<FmodBridgeInstance>(bridge.Entity);

                if (existing.Bridge.Value == bridge &&
                    existing.Version == bridge.BindingVersion &&
                    existing.Find(TargetId) != null)
                {
                    throw new InvalidOperationException("FMODの設定で対象IDが重複しています。このPrefab内の各設定に異なるTarget IDを指定してください。");
                }
            }

            bridge.ConfigureFmod(
                TargetId,
                Event,
                transform);
            if (PlayOnSpawn)
            {
                bridge.World.EntityManager.GetBuffer<FmodCommand>(bridge.Entity).Add(FmodCommand.Play(TargetId));
            }
        }
    }
}
