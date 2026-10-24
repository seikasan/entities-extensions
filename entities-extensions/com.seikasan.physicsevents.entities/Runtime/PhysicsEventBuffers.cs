using Unity.Entities;

namespace PhysicsEvents.Entities
{
    /// <summary>
    /// 固定更新中のTriggerEnterを保持します。有効なBufferだけが購読します。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct TriggerEnter : IBufferElementData, IEnableableComponent
    {
        public Entity Other;
    }

    /// <summary>
    /// 固定更新中のTriggerStayを保持します。有効なBufferだけが購読します。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct TriggerStay : IBufferElementData, IEnableableComponent
    {
        public Entity Other;
    }

    /// <summary>
    /// 固定更新中のTriggerExitを保持します。有効なBufferだけが購読します。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct TriggerExit : IBufferElementData, IEnableableComponent
    {
        public Entity Other;
    }

    /// <summary>
    /// 固定更新中のCollisionEnterを保持します。有効なBufferだけが購読します。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct CollisionEnter : IBufferElementData, IEnableableComponent
    {
        public Entity Other;
    }

    /// <summary>
    /// 固定更新中のCollisionStayを保持します。有効なBufferだけが購読します。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct CollisionStay : IBufferElementData, IEnableableComponent
    {
        public Entity Other;
    }

    /// <summary>
    /// 固定更新中のCollisionExitを保持します。有効なBufferだけが購読します。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct CollisionExit : IBufferElementData, IEnableableComponent
    {
        public Entity Other;
    }

    /// <summary>
    /// 物理更新前に確保する要素数です。未指定のBufferは追加しません。
    /// </summary>
    public struct PhysicsEventBufferCapacity : IComponentData
    {
        public int TriggerEnter;
        public int TriggerStay;
        public int TriggerExit;
        public int CollisionEnter;
        public int CollisionStay;
        public int CollisionExit;
#if PHYSICS_EVENTS_PROFILING
        /// <summary>
        /// 初期容量設定による確保回数です。実体の生成後に増加します。
        /// </summary>
        public long ReservationCount;
#endif
    }
}
