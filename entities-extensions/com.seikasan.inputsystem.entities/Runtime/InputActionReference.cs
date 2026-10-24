using Unity.Entities;

namespace InputSystem.Entities
{
    /// <summary>
    /// Bakingで変換した識別情報です。対応する生成済みInputActionAssetのスナップショットで解決します。
    /// </summary>
    public struct InputActionReference : IComponentData
    {
        public Hash128 AssetId;
        public Hash128 ActionId;
        public bool IsValid => AssetId.IsValid &&
            ActionId.IsValid;
    }
}
