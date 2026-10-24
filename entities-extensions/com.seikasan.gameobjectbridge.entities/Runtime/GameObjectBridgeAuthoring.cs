using Unity.Entities;
using UnityEngine;

namespace GameObjectBridge.Entities
{
    /// <summary>
    /// Entityに対応する表示用Prefabを指定します。Prefab内の通常のComponentはGameObjectとして保持します。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Entities/GameObject Bridge")]
    public sealed class GameObjectBridgeAuthoring : MonoBehaviour
    {
        [Tooltip("Entityの生成時にGameObjectとして生成する表示用Prefabアセットです。")]
        [SerializeField]
        private GameObject _prefab;

        [Tooltip("親のないEntityの正の一様Scaleも表示へ反映します。")]
        [SerializeField]
        private bool _synchronizeScale;

        public GameObject Prefab => _prefab;
        public bool SynchronizeScale => _synchronizeScale;
        private sealed class Baker : global::Unity.Entities.Baker<GameObjectBridgeAuthoring>
        {
            public override void Bake(GameObjectBridgeAuthoring authoring)
            {
                DependsOn(authoring.Prefab);

                if (authoring.Prefab == null)
                {
                    return;
                }

                if (authoring.Prefab.scene.IsValid())
                {
                    Debug.LogError("GameObject Bridgeにはシーン内のオブジェクトではなく、表示用Prefabアセットを指定してください。", authoring);

                    return;
                }

                AddComponent(
                    GetEntity(TransformUsageFlags.Dynamic),
                    new GameObjectBridgePrefab
                    {
                        Prefab = authoring.Prefab,
                        SynchronizeScale = authoring.SynchronizeScale
                    });
            }
        }
    }
}
