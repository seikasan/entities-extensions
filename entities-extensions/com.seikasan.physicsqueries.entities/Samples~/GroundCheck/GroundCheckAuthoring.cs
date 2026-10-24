using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using BoxCollider = Unity.Physics.BoxCollider;

namespace PhysicsQueries.Entities.Samples
{
    /// <summary>
    /// SubScene内の座標(0, 0.6, 0)に配置します。検出用Entityと原点の静的な地面BoxをBakeします。
    /// </summary>
    public sealed class GroundCheckAuthoring : MonoBehaviour
    {
        sealed class GroundCheckBaker : Baker<GroundCheckAuthoring>
        {
            public override void Bake(GroundCheckAuthoring authoring)
            {
                var probe = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(probe, new Grounded());
                var ground = CreateAdditionalEntity(TransformUsageFlags.None);
                var collider = BoxCollider.Create(new BoxGeometry
                {
                    Center = float3.zero,
                    Size = new float3(
                        10,
                        1,
                        10),
                    Orientation = quaternion.identity,
                    BevelRadius = 0
                });
                AddBlobAsset(ref collider, out _);
                AddComponent(ground, new PhysicsCollider { Value = collider });
                AddSharedComponent(ground, new PhysicsWorldIndex());
                AddComponent(ground, LocalTransform.Identity);
                AddComponent(ground, new LocalToWorld { Value = float4x4.identity });
            }
        }
    }
}
