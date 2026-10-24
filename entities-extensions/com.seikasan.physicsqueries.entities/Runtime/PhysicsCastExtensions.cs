using Unity.Mathematics;
using Unity.Physics;

namespace PhysicsQueries.Entities
{
    /// <summary>
    /// 方向、outヒット、有限の最大距離の順で指定し、最も近い形状Castのヒットを取得します。
    /// </summary>
    /// <remarks>
    /// 方向を正規化し、無効なRayではfalseを返します。形状の扱いはUnity Physicsの規則に従います。
    /// </remarks>
    public static class PhysicsCastExtensions
    {
        /// <summary>
        /// CollisionFilter.Defaultを使ってSphereCastします。
        /// </summary>
        public static bool SphereCast(
            this PhysicsWorldSingleton physics,
            float3 origin,
            float radius,
            float3 direction,
            out ColliderCastHit hit,
            float maxDistance,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.SphereCast(
                origin,
                radius,
                direction,
                out hit,
                maxDistance,
                CollisionFilter.Default,
                queryInteraction);

        /// <summary>
        /// フィルターとTriggerの扱いを指定してSphereCastします。
        /// </summary>
        public static bool SphereCast(
            this PhysicsWorldSingleton physics,
            float3 origin,
            float radius,
            float3 direction,
            out ColliderCastHit hit,
            float maxDistance,
            CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
        {
            hit = default;

            if (!QueryInput.TryCast(
                origin,
                direction,
                maxDistance,
                out var unit,
                out _))
            {
                return false;
            }

            return physics.SphereCast(
                origin,
                radius,
                unit,
                maxDistance,
                out hit,
                filter,
                queryInteraction);
        }

        /// <summary>
        /// CollisionFilter.Defaultを使い、軸に平行なBoxCastを行います。
        /// </summary>
        public static bool BoxCast(
            this PhysicsWorldSingleton physics,
            float3 center,
            float3 halfExtents,
            float3 direction,
            out ColliderCastHit hit,
            float maxDistance,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.BoxCast(
                center,
                halfExtents,
                direction,
                out hit,
                maxDistance,
                quaternion.identity,
                CollisionFilter.Default,
                queryInteraction);

        /// <summary>
        /// 指定フィルターで、軸に平行なBoxCastを行います。
        /// </summary>
        public static bool BoxCast(
            this PhysicsWorldSingleton physics,
            float3 center,
            float3 halfExtents,
            float3 direction,
            out ColliderCastHit hit,
            float maxDistance,
            CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.BoxCast(
                center,
                halfExtents,
                direction,
                out hit,
                maxDistance,
                quaternion.identity,
                filter,
                queryInteraction);

        /// <summary>
        /// CollisionFilter.Defaultを使い、向きを指定してBoxCastします。
        /// </summary>
        public static bool BoxCast(
            this PhysicsWorldSingleton physics,
            float3 center,
            float3 halfExtents,
            float3 direction,
            out ColliderCastHit hit,
            float maxDistance,
            quaternion orientation,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.BoxCast(
                center,
                halfExtents,
                direction,
                out hit,
                maxDistance,
                orientation,
                CollisionFilter.Default,
                queryInteraction);

        /// <summary>
        /// 向き、フィルター、Triggerの扱いを指定してBoxCastします。
        /// </summary>
        public static bool BoxCast(
            this PhysicsWorldSingleton physics,
            float3 center,
            float3 halfExtents,
            float3 direction,
            out ColliderCastHit hit,
            float maxDistance,
            quaternion orientation,
            CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
        {
            hit = default;

            if (!QueryInput.TryCast(
                center,
                direction,
                maxDistance,
                out var unit,
                out _))
            {
                return false;
            }

            return physics.BoxCast(
                center,
                orientation,
                halfExtents,
                unit,
                maxDistance,
                out hit,
                filter,
                queryInteraction);
        }

        /// <summary>
        /// CollisionFilter.DefaultでCapsuleCastします。両端の座標は各球の中心です。
        /// </summary>
        public static bool CapsuleCast(
            this PhysicsWorldSingleton physics,
            float3 point1,
            float3 point2,
            float radius,
            float3 direction,
            out ColliderCastHit hit,
            float maxDistance,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.CapsuleCast(
                point1,
                point2,
                radius,
                direction,
                out hit,
                maxDistance,
                CollisionFilter.Default,
                queryInteraction);

        /// <summary>
        /// フィルターとTriggerの扱いを指定してCapsuleCastします。
        /// </summary>
        public static bool CapsuleCast(
            this PhysicsWorldSingleton physics,
            float3 point1,
            float3 point2,
            float radius,
            float3 direction,
            out ColliderCastHit hit,
            float maxDistance,
            CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
        {
            hit = default;

            if (!QueryInput.TryCast(
                point1,
                direction,
                maxDistance,
                out var unit,
                out _) ||
                !math.all(math.isfinite(point2)) ||
                !math.all(math.isfinite(point2 + unit * maxDistance)))
            {
                return false;
            }

            return physics.CapsuleCast(
                point1,
                point2,
                radius,
                unit,
                maxDistance,
                out hit,
                filter,
                queryInteraction);
        }
    }
}
