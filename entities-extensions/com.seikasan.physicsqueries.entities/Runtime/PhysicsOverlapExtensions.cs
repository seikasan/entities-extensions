using Unity.Collections;
using Unity.Mathematics;
using Unity.Physics;

namespace PhysicsQueries.Entities
{
    /// <summary>
    /// OverlapのDistanceHitを呼び出し側のリストに追記します。初期化や並べ替えは行いません。
    /// </summary>
    public static class PhysicsOverlapExtensions
    {
        /// <summary>
        /// CollisionFilter.Defaultを使ってSphereのOverlapを取得します。
        /// </summary>
        public static bool OverlapSphere(
            this PhysicsWorldSingleton physics,
            float3 position,
            float radius,
            ref NativeList<DistanceHit> hits,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.OverlapSphere(
                position,
                radius,
                ref hits,
                CollisionFilter.Default,
                queryInteraction);

        /// <summary>
        /// CollisionFilter.Defaultを使ってCapsuleのOverlapを取得します。
        /// </summary>
        public static bool OverlapCapsule(
            this PhysicsWorldSingleton physics,
            float3 point1,
            float3 point2,
            float radius,
            ref NativeList<DistanceHit> hits,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.OverlapCapsule(
                point1,
                point2,
                radius,
                ref hits,
                CollisionFilter.Default,
                queryInteraction);

        /// <summary>
        /// CollisionFilter.Defaultを使い、軸に平行なBoxのOverlapを取得します。
        /// </summary>
        public static bool OverlapBox(
            this PhysicsWorldSingleton physics,
            float3 center,
            float3 halfExtents,
            ref NativeList<DistanceHit> hits,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.OverlapBox(
                center,
                quaternion.identity,
                halfExtents,
                ref hits,
                CollisionFilter.Default,
                queryInteraction);

        /// <summary>
        /// 指定フィルターで、軸に平行なBoxのOverlapを取得します。
        /// </summary>
        public static bool OverlapBox(
            this PhysicsWorldSingleton physics,
            float3 center,
            float3 halfExtents,
            ref NativeList<DistanceHit> hits,
            CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.OverlapBox(
                center,
                quaternion.identity,
                halfExtents,
                ref hits,
                filter,
                queryInteraction);

        /// <summary>
        /// CollisionFilter.Defaultを使い、向きを指定してBoxのOverlapを取得します。
        /// </summary>
        public static bool OverlapBox(
            this PhysicsWorldSingleton physics,
            float3 center,
            float3 halfExtents,
            ref NativeList<DistanceHit> hits,
            quaternion orientation,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.OverlapBox(
                center,
                orientation,
                halfExtents,
                ref hits,
                CollisionFilter.Default,
                queryInteraction);

        /// <summary>
        /// 指定フィルターと向きでBoxのOverlapを取得します。
        /// </summary>
        public static bool OverlapBox(
            this PhysicsWorldSingleton physics,
            float3 center,
            float3 halfExtents,
            ref NativeList<DistanceHit> hits,
            quaternion orientation,
            CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.OverlapBox(
                center,
                orientation,
                halfExtents,
                ref hits,
                filter,
                queryInteraction);
    }
}
