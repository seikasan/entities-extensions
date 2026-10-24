using Unity.Collections;
using Unity.Mathematics;
using Unity.Physics;

namespace PhysicsQueries.Entities
{
    /// <summary>
    /// 方向と有限の必須距離を指定するRayクエリです。
    /// </summary>
    /// <remarks>
    /// 方向を正規化します。無効なRayではfalseを返し、Triggerには標準CastRayの動作を適用します。
    /// </remarks>
    public static class PhysicsRaycastExtensions
    {
        /// <summary>
        /// CollisionFilter.Defaultを使い、最も近いRayヒットを取得します。
        /// </summary>
        public static bool Raycast(
            this PhysicsWorldSingleton physics,
            float3 origin,
            float3 direction,
            out RaycastHit hit,
            float maxDistance) => physics.Raycast(
                origin,
                direction,
                out hit,
                maxDistance,
                CollisionFilter.Default);

        /// <summary>
        /// 指定フィルターで最も近いRayヒットを取得します。未接触時のヒットはdefaultです。
        /// </summary>
        public static bool Raycast(
            this PhysicsWorldSingleton physics,
            float3 origin,
            float3 direction,
            out RaycastHit hit,
            float maxDistance,
            CollisionFilter filter)
        {
            hit = default;

            if (!QueryInput.TryCast(
                origin,
                direction,
                maxDistance,
                out _,
                out var end))
            {
                return false;
            }

            return physics.CastRay(new RaycastInput
            {
                Start = origin,
                End = end,
                Filter = filter
            }, out hit);
        }

        /// <summary>
        /// CollisionFilter.Defaultを使い、呼び出し側のリストにすべてのRayヒットを追記します。
        /// </summary>
        /// <remarks>
        /// リストの初期化、並べ替え、重複除去は行いません。falseは新しいヒットがなかったことを示します。
        /// </remarks>
        public static bool RaycastAll(
            this PhysicsWorldSingleton physics,
            float3 origin,
            float3 direction,
            ref NativeList<RaycastHit> hits,
            float maxDistance) => physics.RaycastAll(
                origin,
                direction,
                ref hits,
                maxDistance,
                CollisionFilter.Default);

        /// <summary>
        /// 指定フィルターでRayヒットをすべて追記します。無効なRayではリストを変更しません。
        /// </summary>
        public static bool RaycastAll(
            this PhysicsWorldSingleton physics,
            float3 origin,
            float3 direction,
            ref NativeList<RaycastHit> hits,
            float maxDistance,
            CollisionFilter filter)
        {
            if (!QueryInput.TryCast(
                origin,
                direction,
                maxDistance,
                out _,
                out var end))
            {
                return false;
            }

            return physics.CastRay(new RaycastInput
            {
                Start = origin,
                End = end,
                Filter = filter
            }, ref hits);
        }
    }
}
