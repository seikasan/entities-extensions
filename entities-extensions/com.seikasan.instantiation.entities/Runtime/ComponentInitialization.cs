using System;
using Unity.Entities;

namespace Instantiation.Entities
{
    internal static class ComponentInitialization
    {
        /// <summary>
        /// EntityManager版では、初期化を始める前に生成元の存在を確認します。
        /// </summary>
        internal static void ValidatePrefab(EntityManager manager, Entity prefab)
        {
            if (!manager.Exists(prefab))
            {
                throw new ArgumentException("生成元のEntityが存在しません。", nameof(prefab));
            }
        }

        /// <summary>
        /// Componentがない場合はInstantiate前に拒否し、生成済みEntityを残しません。
        /// ValidatePrefabを先に呼んでください。
        /// </summary>
        internal static void Require<T>(EntityManager manager, Entity prefab)
            where T : unmanaged, IComponentData
        {
            Validate<T>();
            if (!manager.HasComponent<T>(prefab))
            {
                throw new ArgumentException(
                    "生成元のEntityに初期化対象のComponentがありません。",
                    nameof(prefab));
            }
        }

        internal static void Validate<T>()
            where T : unmanaged, IComponentData
        {
            if (TypeManager.GetTypeIndex<T>().IsZeroSized)
            {
                throw new ArgumentException("初期化するComponentにはデータが必要です。サイズ0のTagは指定できません。");
            }
        }
    }
}
