using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace GameObjectBridge.Entities
{
    internal static class BridgeTransformSynchronization
    {
        internal static bool TryPose(
            float4x4 matrix,
            out float3 position,
            out quaternion rotation,
            out float scale)
        {
            position = matrix.c3.xyz;
            rotation = quaternion.identity;
            scale = 1;

            if (!math.all(math.isfinite(matrix.c0)) ||
                !math.all(math.isfinite(matrix.c1)) ||
                !math.all(math.isfinite(matrix.c2)) ||
                !math.all(math.isfinite(matrix.c3)))
            {
                return false;
            }

            var lengths = new float3(
                math.length(matrix.c0.xyz),
                math.length(matrix.c1.xyz),
                math.length(matrix.c2.xyz));
            scale = lengths.x;

            if (!math.all(math.isfinite(lengths)) ||
                math.cmin(lengths) < 1e-6f ||
                math.cmax(lengths) - math.cmin(lengths) > 1e-4f * math.cmax(lengths))
            {
                return false;
            }

            var x = matrix.c0.xyz / lengths.x;
            var y = matrix.c1.xyz / lengths.y;
            var z = matrix.c2.xyz / lengths.z;

            if (math.abs(math.dot(x, y)) > 1e-4f ||
                math.abs(math.dot(x, z)) > 1e-4f ||
                math.abs(math.dot(y, z)) > 1e-4f ||
                math.dot(math.cross(x, y), z) < 0.9999f)
            {
                return false;
            }

            rotation = math.normalize(new quaternion(new float3x3(
                x,
                y,
                z)));

            return true;
        }

        internal static bool ToGameObject(EntityManager manager, EntityGameObjectBridge bridge)
        {
            if (!manager.HasComponent<LocalToWorld>(bridge.Entity) ||
                manager.HasComponent<Disabled>(bridge.Entity))
            {
                return false;
            }

            return ToGameObject(
                bridge,
                manager.GetComponentData<LocalToWorld>(bridge.Entity).Value,
                manager.HasComponent<Parent>(bridge.Entity));
        }

        internal static bool ToGameObject(
            EntityGameObjectBridge bridge,
            float4x4 matrix,
            bool hasParent)
        {
            var target = bridge.transform;

            if (target.parent != null &&
                !TryPose(
                    (float4x4)target.parent.localToWorldMatrix,
                    out _,
                    out _,
                    out _))
            {
                return false;
            }

            if (bridge.SynchronizeScale &&
                (target.parent != null ||
                    hasParent))
            {
                return false;
            }

            if (!TryPose(
                matrix,
                out var position,
                out var rotation,
                out var scale))
            {
                return false;
            }

            target.SetPositionAndRotation(position, rotation);
            if (bridge.SynchronizeScale)
            {
                target.localScale = new Vector3(
                    scale,
                    scale,
                    scale);
            }

            return true;
        }

        internal static bool ToEntity(
            EntityManager manager,
            EntityGameObjectBridge bridge,
            ref ComponentLookup<LocalTransform> transforms,
            ref ComponentLookup<Parent> parents,
            ref ComponentLookup<PostTransformMatrix> postTransforms)
        {
            var entity = bridge.Entity;

            if (!manager.HasComponent<LocalTransform>(entity) ||
                manager.HasComponent<Disabled>(entity) ||
                postTransforms.HasComponent(entity))
            {
                return false;
            }

            var source = bridge.transform;

            if (source.parent != null &&
                !TryPose(
                    (float4x4)source.parent.localToWorldMatrix,
                    out _,
                    out _,
                    out _))
            {
                return false;
            }

            float3 position = source.position;
            quaternion rotation = source.rotation;

            if (!math.all(math.isfinite(position)) ||
                !math.all(math.isfinite(rotation.value)))
            {
                return false;
            }

            var value = manager.GetComponentData<LocalTransform>(entity);

            if (!math.isfinite(value.Scale) ||
                value.Scale <= 0)
            {
                return false;
            }

            if (parents.HasComponent(entity))
            {
                if (bridge.SynchronizeScale)
                {
                    return false;
                }

                var parent = parents[entity].Value;

                if (!transforms.HasComponent(parent))
                {
                    return false;
                }

                TransformHelpers.ComputeWorldTransformMatrix(
                    parent,
                    out var parentWorld,
                    ref transforms,
                    ref parents,
                    ref postTransforms);

                if (!TryPose(
                    parentWorld,
                    out _,
                    out var parentRotation,
                    out _))
                {
                    return false;
                }

                position = math.transform(math.inverse(parentWorld), position);
                rotation = math.mul(math.inverse(parentRotation), rotation);
            }

            if (bridge.SynchronizeScale)
            {
                if (source.parent != null ||
                    !TryPose(
                        (float4x4)source.localToWorldMatrix,
                        out _,
                        out _,
                        out var scale))
                {
                    return false;
                }

                value.Scale = scale;
            }

            value.Position = position;
            value.Rotation = math.normalize(rotation);
            manager.SetComponentData(entity, value);

            return true;
        }
    }
}
