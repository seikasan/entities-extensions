using Instantiation.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.Entities
{
    /// <summary>
    /// Entityの並列生成と、それに続く既存Componentの初期化を記録します。
    /// </summary>
    /// <remarks>
    /// 呼び出し内の全コマンドに指定sortKeyを使用します。Unity標準の決定的なソートキーを指定してください。
    /// 返すEntityは同じECB内だけで使用できる遅延参照です。必要なComponentの不足はPlayback時に失敗します。
    /// PrefabがParentを持つ場合、Transformにはローカル座標を使用します。
    /// </remarks>
    public static class ParallelWriterExtensions
    {
        /// <summary>
        /// 指定位置、無回転、Scale 1でEntityの生成を記録します。
        /// </summary>
        /// <param name = "ecb">並列記録用のParallelWriter。</param>
        /// <param name = "sortKey">この呼び出し内の全コマンドに使用するUnity標準ECBのソートキー。</param>
        /// <param name = "prefab">LocalTransformを持つ生成元Entity。</param>
        /// <param name = "position">初期ローカル位置。</param>
        /// <returns>このECBに属する遅延Entity参照。</returns>
        public static Entity Instantiate(
            this EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            Entity prefab,
            float3 position) => ecb.Instantiate(
                sortKey,
                prefab,
                LocalTransform.FromPosition(position));

        /// <summary>
        /// 指定位置・回転とScale 1でEntityの生成を記録します。
        /// </summary>
        /// <param name = "ecb">並列記録用のParallelWriter。</param>
        /// <param name = "sortKey">この呼び出し内の全コマンドに使用するUnity標準ECBのソートキー。</param>
        /// <param name = "prefab">LocalTransformを持つ生成元Entity。</param>
        /// <param name = "position">初期ローカル位置。</param>
        /// <param name = "rotation">初期ローカル回転。</param>
        /// <returns>このECBに属する遅延Entity参照。</returns>
        public static Entity Instantiate(
            this EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            Entity prefab,
            float3 position,
            quaternion rotation) => ecb.Instantiate(
                sortKey,
                prefab,
                LocalTransform.FromPositionRotation(position, rotation));

        /// <summary>
        /// 指定位置・回転・一様ScaleでEntityの生成を記録します。
        /// </summary>
        /// <param name = "ecb">並列記録用のParallelWriter。</param>
        /// <param name = "sortKey">この呼び出し内の全コマンドに使用するUnity標準ECBのソートキー。</param>
        /// <param name = "prefab">LocalTransformを持つ生成元Entity。</param>
        /// <param name = "position">初期ローカル位置。</param>
        /// <param name = "rotation">初期ローカル回転。</param>
        /// <param name = "scale">初期一様Scale。</param>
        /// <returns>このECBに属する遅延Entity参照。</returns>
        public static Entity Instantiate(
            this EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            Entity prefab,
            float3 position,
            quaternion rotation,
            float scale) => ecb.Instantiate(
                sortKey,
                prefab,
                LocalTransform.FromPositionRotationScale(
                    position,
                    rotation,
                    scale));

        /// <summary>
        /// Entityの生成と、既存LocalTransform全体の上書きを記録します。
        /// </summary>
        /// <param name = "ecb">並列記録用のParallelWriter。</param>
        /// <param name = "sortKey">この呼び出し内の全コマンドに使用するUnity標準ECBのソートキー。</param>
        /// <param name = "prefab">LocalTransformを持つ生成元Entity。</param>
        /// <param name = "transform">初期化するLocalTransform全体。</param>
        /// <returns>このECBに属する遅延Entity参照。</returns>
        public static Entity Instantiate(
            this EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            Entity prefab,
            LocalTransform transform)
        {
            var entity = ecb.Instantiate(sortKey, prefab);
            ecb.SetComponent(
                sortKey,
                entity,
                transform);

            return entity;
        }

        /// <summary>
        /// Entityの生成と既存Component 1個の初期化を記録します。それ以外の値は引き継ぎます。
        /// </summary>
        /// <typeparam name = "T">データを持つunmanagedなComponent。サイズ0のTagはコマンド記録前に拒否します。</typeparam>
        /// <param name = "ecb">並列記録用のParallelWriter。</param>
        /// <param name = "sortKey">この呼び出し内の全コマンドに使用するUnity標準ECBのソートキー。</param>
        /// <param name = "prefab">Tを持つ生成元Entity。</param>
        /// <param name = "component">Componentの初期値。</param>
        /// <returns>このECBに属する遅延Entity参照。</returns>
        public static Entity Instantiate<T>(
            this EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            Entity prefab,
            T component)
            where T : unmanaged, IComponentData
        {
            ComponentInitialization.Validate<T>();
            var entity = ecb.Instantiate(sortKey, prefab);
            ecb.SetComponent(
                sortKey,
                entity,
                component);

            return entity;
        }

        /// <summary>
        /// Entityの生成、LocalTransform、既存Component 1個の初期化をこの順で記録します。
        /// </summary>
        /// <typeparam name = "T">データを持つunmanagedなComponent。サイズ0のTagはコマンド記録前に拒否します。</typeparam>
        /// <param name = "ecb">並列記録用のParallelWriter。</param>
        /// <param name = "sortKey">この呼び出し内の全コマンドに使用するUnity標準ECBのソートキー。</param>
        /// <param name = "prefab">LocalTransformとTを持つ生成元Entity。</param>
        /// <param name = "transform">初期化するLocalTransform全体。</param>
        /// <param name = "component">Componentの初期値。TがLocalTransformの場合はこちらの値を優先します。</param>
        /// <returns>このECBに属する遅延Entity参照。</returns>
        public static Entity Instantiate<T>(
            this EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            Entity prefab,
            LocalTransform transform,
            T component)
            where T : unmanaged, IComponentData
        {
            ComponentInitialization.Validate<T>();
            var entity = ecb.Instantiate(
                sortKey,
                prefab,
                transform);
            ecb.SetComponent(
                sortKey,
                entity,
                component);

            return entity;
        }
        /// <summary>
        /// 既存の2個のComponentの初期化をまとめて記録します。
        /// </summary>
        /// <remarks>
        /// Tagは記録前に拒否し、Componentの不足はPlayback時に検出されます。
        /// 同じ型を複数回指定した場合は最後の値が優先されます。
        /// </remarks>
        public static Entity Instantiate<T1, T2>(
            this EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            Entity prefab,
            T1 component1,
            T2 component2)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
        {
            ComponentInitialization.Validate<T1>();
            ComponentInitialization.Validate<T2>();
            var entity = ecb.Instantiate(sortKey, prefab);
            ecb.SetComponent(sortKey, entity, component1);
            ecb.SetComponent(sortKey, entity, component2);

            return entity;
        }

        /// <summary>
        /// LocalTransformと2個のComponentの初期化をまとめて記録します。
        /// </summary>
        /// <remarks>
        /// Tagは記録前に拒否し、Componentの不足はPlayback時に検出されます。
        /// 同じ型を複数回指定した場合は最後の値が優先されます。
        /// </remarks>
        public static Entity Instantiate<T1, T2>(
            this EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            Entity prefab,
            LocalTransform transform,
            T1 component1,
            T2 component2)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
        {
            ComponentInitialization.Validate<T1>();
            ComponentInitialization.Validate<T2>();
            var entity = ecb.Instantiate(sortKey, prefab, transform);
            ecb.SetComponent(sortKey, entity, component1);
            ecb.SetComponent(sortKey, entity, component2);

            return entity;
        }

        /// <summary>
        /// 既存の3個のComponentの初期化をまとめて記録します。
        /// </summary>
        /// <remarks>
        /// Tagは記録前に拒否し、Componentの不足はPlayback時に検出されます。
        /// 同じ型を複数回指定した場合は最後の値が優先されます。
        /// </remarks>
        public static Entity Instantiate<T1, T2, T3>(
            this EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            Entity prefab,
            T1 component1,
            T2 component2,
            T3 component3)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
        {
            ComponentInitialization.Validate<T1>();
            ComponentInitialization.Validate<T2>();
            ComponentInitialization.Validate<T3>();
            var entity = ecb.Instantiate(sortKey, prefab);
            ecb.SetComponent(sortKey, entity, component1);
            ecb.SetComponent(sortKey, entity, component2);
            ecb.SetComponent(sortKey, entity, component3);

            return entity;
        }

        /// <summary>
        /// LocalTransformと3個のComponentの初期化をまとめて記録します。
        /// </summary>
        /// <remarks>
        /// Tagは記録前に拒否し、Componentの不足はPlayback時に検出されます。
        /// 同じ型を複数回指定した場合は最後の値が優先されます。
        /// </remarks>
        public static Entity Instantiate<T1, T2, T3>(
            this EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            Entity prefab,
            LocalTransform transform,
            T1 component1,
            T2 component2,
            T3 component3)
            where T1 : unmanaged, IComponentData
            where T2 : unmanaged, IComponentData
            where T3 : unmanaged, IComponentData
        {
            ComponentInitialization.Validate<T1>();
            ComponentInitialization.Validate<T2>();
            ComponentInitialization.Validate<T3>();
            var entity = ecb.Instantiate(sortKey, prefab, transform);
            ecb.SetComponent(sortKey, entity, component1);
            ecb.SetComponent(sortKey, entity, component2);
            ecb.SetComponent(sortKey, entity, component3);

            return entity;
        }

    }
}
