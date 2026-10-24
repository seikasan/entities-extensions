using Unity.Entities;
using UnityEngine;

namespace PhysicsEvents.Entities
{
    /// <summary>
    /// 6種類の物理イベントBufferの有効状態と予約容量を設定します。
    /// </summary>
    public sealed class PhysicsEventReceiverAuthoring : MonoBehaviour
    {
        [SerializeField]
        private bool _triggerEnter = true;
        [SerializeField, Min(0)]
        private int _triggerEnterCapacity = 4;
        [SerializeField]
        private bool _triggerStay = false;
        [SerializeField, Min(0)]
        private int _triggerStayCapacity = 0;
        [SerializeField]
        private bool _triggerExit = false;
        [SerializeField, Min(0)]
        private int _triggerExitCapacity = 0;
        [SerializeField]
        private bool _collisionEnter = false;
        [SerializeField, Min(0)]
        private int _collisionEnterCapacity = 0;
        [SerializeField]
        private bool _collisionStay = false;
        [SerializeField, Min(0)]
        private int _collisionStayCapacity = 0;
        [SerializeField]
        private bool _collisionExit = false;
        [SerializeField, Min(0)]
        private int _collisionExitCapacity = 0;
        private sealed class Baker : Baker<PhysicsEventReceiverAuthoring>
        {
            public override void Bake(PhysicsEventReceiverAuthoring authoring)
            {
                if (!authoring._triggerEnter &&
                    !authoring._triggerStay &&
                    !authoring._triggerExit &&
                    !authoring._collisionEnter &&
                    !authoring._collisionStay &&
                    !authoring._collisionExit)
                {
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.None);
                if (authoring._triggerEnter)
                {
                    AddBuffer<TriggerEnter>(entity);
                }

                if (authoring._triggerStay)
                {
                    AddBuffer<TriggerStay>(entity);
                }

                if (authoring._triggerExit)
                {
                    AddBuffer<TriggerExit>(entity);
                }

                if (authoring._collisionEnter)
                {
                    AddBuffer<CollisionEnter>(entity);
                }

                if (authoring._collisionStay)
                {
                    AddBuffer<CollisionStay>(entity);
                }

                if (authoring._collisionExit)
                {
                    AddBuffer<CollisionExit>(entity);
                }

                AddComponent(
                    entity,
                    new PhysicsEventBufferCapacity
                    {
                        TriggerEnter = authoring._triggerEnter ? Mathf.Max(0, authoring._triggerEnterCapacity) : 0,
                        TriggerStay = authoring._triggerStay ? Mathf.Max(0, authoring._triggerStayCapacity) : 0,
                        TriggerExit = authoring._triggerExit ? Mathf.Max(0, authoring._triggerExitCapacity) : 0,
                        CollisionEnter = authoring._collisionEnter ? Mathf.Max(0, authoring._collisionEnterCapacity) : 0,
                        CollisionStay = authoring._collisionStay ? Mathf.Max(0, authoring._collisionStayCapacity) : 0,
                        CollisionExit = authoring._collisionExit ? Mathf.Max(0, authoring._collisionExitCapacity) : 0
                    });
            }
        }
    }
}
