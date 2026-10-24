using System;
using UnityEngine;
using ManagedAsset = UnityEngine.InputSystem.InputActionAsset;

namespace InputSystem.Entities
{
    /// <summary>
    /// 生成したアセット参照により、入力定義をPlayerビルドに含めます。
    /// </summary>
    public sealed class InputActionAssetRegistry : ScriptableObject
    {
        public const string ResourceName = "InputSystemEntitiesRegistry";

        [Serializable]
        public struct Entry
        {
            public string Guid;
            public ManagedAsset Asset;
        }

        [HideInInspector]
        public Entry[] Assets = Array.Empty<Entry>();

        public ManagedAsset Find(string assetGuid)
        {
            foreach (var entry in Assets)
            {
                if (entry.Guid == assetGuid)
                {
                    return entry.Asset;
                }
            }

            return null;
        }
    }
}
