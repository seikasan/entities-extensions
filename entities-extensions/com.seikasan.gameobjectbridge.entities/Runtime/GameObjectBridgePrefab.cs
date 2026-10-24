using Unity.Entities;
using UnityEngine;

namespace GameObjectBridge.Entities
{
    /// <summary>
    /// Entityの生存中に表示用GameObjectを1つ所有します。Entity Prefabと一緒に複製されます。
    /// </summary>
    public struct GameObjectBridgePrefab : IComponentData
    {
        public UnityObjectRef<GameObject> Prefab;
        public bool SynchronizeScale;
    }

    /// <summary>
    /// 表示用Prefabに保存したSDK設定を、自動生成と関連付けの後に1回だけ適用します。
    /// </summary>
    /// <remarks>
    /// optionalなSDKをRuntimeから直接参照しないための設定用の契約です。毎フレームは呼び出しません。
    /// </remarks>
    public interface IGameObjectBridgeSetup
    {
        void Configure(EntityGameObjectBridge bridge);
    }
}
