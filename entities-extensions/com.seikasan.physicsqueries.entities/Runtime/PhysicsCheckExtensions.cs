using Unity.Mathematics;
using Unity.Physics;

namespace PhysicsQueries.Entities
{
    /// <summary>
    /// いずれかの接触を判定します。SphereとCapsuleの明示的フィルター付きAPIはUnity Physicsが提供します。
    /// </summary>
    public static class PhysicsCheckExtensions
    {
        /// <summary>
        /// CollisionFilter.Defaultを使ってSphereの接触を判定します。
        /// </summary>
        public static bool CheckSphere(
            this PhysicsWorldSingleton physics,
            float3 position,
            float radius,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.CheckSphere(
                position,
                radius,
                CollisionFilter.Default,
                queryInteraction);

        /// <summary>
        /// CollisionFilter.Defaultを使ってCapsuleの接触を判定します。
        /// </summary>
        public static bool CheckCapsule(
            this PhysicsWorldSingleton physics,
            float3 point1,
            float3 point2,
            float radius,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.CheckCapsule(
                point1,
                point2,
                radius,
                CollisionFilter.Default,
                queryInteraction);

        /// <summary>
        /// CollisionFilter.Defaultを使い、軸に平行なBoxの接触を判定します。
        /// </summary>
        public static bool CheckBox(
            this PhysicsWorldSingleton physics,
            float3 center,
            float3 halfExtents,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.CheckBox(
                center,
                quaternion.identity,
                halfExtents,
                CollisionFilter.Default,
                queryInteraction);

        /// <summary>
        /// 指定フィルターで、軸に平行なBoxの接触を判定します。
        /// </summary>
        public static bool CheckBox(
            this PhysicsWorldSingleton physics,
            float3 center,
            float3 halfExtents,
            CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.CheckBox(
                center,
                quaternion.identity,
                halfExtents,
                filter,
                queryInteraction);

        /// <summary>
        /// CollisionFilter.Defaultを使い、向きを指定してBoxの接触を判定します。
        /// </summary>
        public static bool CheckBox(
            this PhysicsWorldSingleton physics,
            float3 center,
            float3 halfExtents,
            quaternion orientation,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.CheckBox(
                center,
                orientation,
                halfExtents,
                CollisionFilter.Default,
                queryInteraction);

        /// <summary>
        /// 指定フィルターと向きでBoxの接触を判定します。
        /// </summary>
        public static bool CheckBox(
            this PhysicsWorldSingleton physics,
            float3 center,
            float3 halfExtents,
            quaternion orientation,
            CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default) => physics.CheckBox(
                center,
                orientation,
                halfExtents,
                filter,
                queryInteraction);
    }
}
