using Instantiation.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.Entities
{
    /// <summary>
    /// Entityを即時生成し、既存Componentを初期化します。
    /// </summary>
    /// <remarks>
    /// 標準のEntityManagerと同じ構造変更および同期ポイントを使用します。
    /// 初期化対象のComponentがない場合は生成前に失敗し、Entityを生成しません。
    /// PrefabがParentを持つ場合、Transformにはローカル座標を使用します。
    /// </remarks>
    public static class EntityManagerExtensions
    {
        /// <summary>
        /// 指定位置、無回転、Scale 1でEntityを生成します。
        /// </summary>
        /// <param name = "entityManager">生成に使用するEntityManager。</param>
        /// <param name = "prefab">LocalTransformを持つ生成元Entity。</param>
        /// <param name = "position">初期ローカル位置。</param>
        /// <returns>生成済みの有効なEntity。</returns>
        public static Entity Instantiate(
            this EntityManager entityManager,
            Entity prefab,
            float3 position) => entityManager.Instantiate(prefab, LocalTransform.FromPosition(position));

        /// <summary>
        /// 指定位置・回転とScale 1でEntityを生成します。
        /// </summary>
        /// <param name = "entityManager">生成に使用するEntityManager。</param>
        /// <param name = "prefab">LocalTransformを持つ生成元Entity。</param>
        /// <param name = "position">初期ローカル位置。</param>
        /// <param name = "rotation">初期ローカル回転。</param>
        /// <returns>生成済みの有効なEntity。</returns>
        public static Entity Instantiate(
            this EntityManager entityManager,
            Entity prefab,
            float3 position,
            quaternion rotation) => entityManager.Instantiate(prefab, LocalTransform.FromPositionRotation(position, rotation));

        /// <summary>
        /// 指定位置・回転・一様ScaleでEntityを生成します。
        /// </summary>
        /// <param name = "entityManager">生成に使用するEntityManager。</param>
        /// <param name = "prefab">LocalTransformを持つ生成元Entity。</param>
        /// <param name = "position">初期ローカル位置。</param>
        /// <param name = "rotation">初期ローカル回転。</param>
        /// <param name = "scale">初期一様Scale。</param>
        /// <returns>生成済みの有効なEntity。</returns>
        public static Entity Instantiate(
            this EntityManager entityManager,
            Entity prefab,
            float3 position,
            quaternion rotation,
            float scale) => entityManager.Instantiate(prefab, LocalTransform.FromPositionRotationScale(
                position,
                rotation,
                scale));

        /// <summary>
        /// Entityを生成し、既存のLocalTransform全体を上書きします。
        /// </summary>
        /// <param name = "entityManager">生成に使用するEntityManager。</param>
        /// <param name = "prefab">LocalTransformを持つ生成元Entity。</param>
        /// <param name = "transform">初期化するLocalTransform全体。</param>
        /// <returns>生成済みの有効なEntity。</returns>
        public static Entity Instantiate(
            this EntityManager entityManager,
            Entity prefab,
            LocalTransform transform)
        {
            ComponentInitialization.ValidatePrefab(entityManager, prefab);
            ComponentInitialization.Require<LocalTransform>(entityManager, prefab);
            var entity = entityManager.Instantiate(prefab);
            entityManager.SetComponentData(entity, transform);

            return entity;
        }

        /// <summary>
        /// Entityを生成して既存Componentを1個初期化し、それ以外の値を引き継ぎます。
        /// </summary>
        /// <typeparam name = "T">データを持つunmanagedなComponent。サイズ0のTagは生成前に拒否します。</typeparam>
        /// <param name = "entityManager">生成に使用するEntityManager。</param>
        /// <param name = "prefab">Tを持つ生成元Entity。</param>
        /// <param name = "component">Componentの初期値。</param>
        /// <returns>生成済みの有効なEntity。</returns>
        public static Entity Instantiate<T>(
            this EntityManager entityManager,
            Entity prefab,
            T component)
            where T : unmanaged, IComponentData
        {
            ComponentInitialization.ValidatePrefab(entityManager, prefab);
            ComponentInitialization.Require<T>(entityManager, prefab);
            var entity = entityManager.Instantiate(prefab);
            entityManager.SetComponentData(entity, component);

            return entity;
        }

        /// <summary>
        /// Entityの生成後、LocalTransform、既存Component 1個の順に初期化します。
        /// </summary>
        /// <typeparam name = "T">データを持つunmanagedなComponent。サイズ0のTagは生成前に拒否します。</typeparam>
        /// <param name = "entityManager">生成に使用するEntityManager。</param>
        /// <param name = "prefab">LocalTransformとTを持つ生成元Entity。</param>
        /// <param name = "transform">初期化するLocalTransform全体。</param>
        /// <param name = "component">Componentの初期値。TがLocalTransformの場合はこちらの値を優先します。</param>
        /// <returns>生成済みの有効なEntity。</returns>
        public static Entity Instantiate<T>(
            this EntityManager entityManager,
            Entity prefab,
            LocalTransform transform,
            T component)
            where T : unmanaged, IComponentData
        {
            ComponentInitialization.ValidatePrefab(entityManager, prefab);
            ComponentInitialization.Require<LocalTransform>(entityManager, prefab);
            ComponentInitialization.Require<T>(entityManager, prefab);
            var entity = entityManager.Instantiate(prefab);
            entityManager.SetComponentData(entity, transform);
            entityManager.SetComponentData(entity, component);

            return entity;
        }
        /// <summary>
        /// 既存の2個のComponentをまとめて初期化します。Component不足は生成前に検出します。
        /// </summary>
        /// <remarks>
        /// 同じ型を複数回指定した場合は最後の値が優先されます。LocalTransformも指定順で上書きします。
        /// </remarks>
        public static Entity Instantiate<T1, T2>(
            this EntityManager entityManager,
            Entity prefab,
            T1 component1,
            T2 component2)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
        {
            ComponentInitialization.ValidatePrefab(entityManager, prefab);
            ComponentInitialization.Require<T1>(entityManager, prefab);
            ComponentInitialization.Require<T2>(entityManager, prefab);
            var entity = entityManager.Instantiate(prefab);
            entityManager.SetComponentData(entity, component1);
            entityManager.SetComponentData(entity, component2);

            return entity;
        }

        /// <summary>
        /// LocalTransformと2個のComponentをまとめて初期化します。Component不足は生成前に検出します。
        /// </summary>
        /// <remarks>
        /// 同じ型を複数回指定した場合は最後の値が優先されます。LocalTransformも指定順で上書きします。
        /// </remarks>
        public static Entity Instantiate<T1, T2>(
            this EntityManager entityManager,
            Entity prefab,
            LocalTransform transform,
            T1 component1,
            T2 component2)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
        {
            ComponentInitialization.ValidatePrefab(entityManager, prefab);
            ComponentInitialization.Require<LocalTransform>(entityManager, prefab);
            ComponentInitialization.Require<T1>(entityManager, prefab);
            ComponentInitialization.Require<T2>(entityManager, prefab);
            var entity = entityManager.Instantiate(prefab);
            entityManager.SetComponentData(entity, transform);
            entityManager.SetComponentData(entity, component1);
            entityManager.SetComponentData(entity, component2);

            return entity;
        }

        /// <summary>
        /// 既存の3個のComponentをまとめて初期化します。Component不足は生成前に検出します。
        /// </summary>
        /// <remarks>
        /// 同じ型を複数回指定した場合は最後の値が優先されます。LocalTransformも指定順で上書きします。
        /// </remarks>
        public static Entity Instantiate<T1, T2, T3>(
            this EntityManager entityManager,
            Entity prefab,
            T1 component1,
            T2 component2,
            T3 component3)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
        {
            ComponentInitialization.ValidatePrefab(entityManager, prefab);
            ComponentInitialization.Require<T1>(entityManager, prefab);
            ComponentInitialization.Require<T2>(entityManager, prefab);
            ComponentInitialization.Require<T3>(entityManager, prefab);
            var entity = entityManager.Instantiate(prefab);
            entityManager.SetComponentData(entity, component1);
            entityManager.SetComponentData(entity, component2);
            entityManager.SetComponentData(entity, component3);

            return entity;
        }

        /// <summary>
        /// LocalTransformと3個のComponentをまとめて初期化します。Component不足は生成前に検出します。
        /// </summary>
        /// <remarks>
        /// 同じ型を複数回指定した場合は最後の値が優先されます。LocalTransformも指定順で上書きします。
        /// </remarks>
        public static Entity Instantiate<T1, T2, T3>(
            this EntityManager entityManager,
            Entity prefab,
            LocalTransform transform,
            T1 component1,
            T2 component2,
            T3 component3)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
        {
            ComponentInitialization.ValidatePrefab(entityManager, prefab);
            ComponentInitialization.Require<LocalTransform>(entityManager, prefab);
            ComponentInitialization.Require<T1>(entityManager, prefab);
            ComponentInitialization.Require<T2>(entityManager, prefab);
            ComponentInitialization.Require<T3>(entityManager, prefab);
            var entity = entityManager.Instantiate(prefab);
            entityManager.SetComponentData(entity, transform);
            entityManager.SetComponentData(entity, component1);
            entityManager.SetComponentData(entity, component2);
            entityManager.SetComponentData(entity, component3);

            return entity;
        }

    }
}
