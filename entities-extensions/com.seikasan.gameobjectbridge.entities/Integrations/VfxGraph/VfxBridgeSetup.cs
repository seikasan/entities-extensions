using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.VFX;

namespace GameObjectBridge.Entities.VfxGraph
{
    /// <summary>
    /// 表示用Prefabに保存したVisualEffectの設定を、自動生成時に適用します。
    /// </summary>
    [AddComponentMenu("Entities/GameObject Bridge/VFX Setup")]
    public sealed class VfxBridgeSetup : MonoBehaviour, IGameObjectBridgeSetup
    {
        [Tooltip("同じEntity内で一意の対象IDです。既定のコマンドは0に送られます。")]
        [SerializeField]
        [Min(0)]
        private int _targetId;
        [Tooltip("未指定なら、このGameObjectのVisualEffectを使用します。")]
        [SerializeField]
        private VisualEffect _effect;
        public VisualEffect Effect => _effect;
        public int TargetId => _targetId;

        public void Configure(EntityGameObjectBridge bridge)
        {
            var manager = bridge.World.EntityManager;
            if (manager.HasBuffer<VfxBridgeTarget>(bridge.Entity))
            {
                foreach (var target in manager.GetBuffer<VfxBridgeTarget>(bridge.Entity))
                {
                    if (target.TargetId == TargetId &&
                        target.Bridge.Value == bridge &&
                        target.Version == bridge.BindingVersion)
                    {
                        throw new InvalidOperationException("VFXの設定で対象IDが重複しています。このPrefab内の各設定に異なるTarget IDを指定してください。");
                    }
                }
            }

            var effect = Effect != null ? Effect : GetComponent<VisualEffect>();
            bridge.ConfigureVisualEffect(TargetId, effect);
        }
    }
}
