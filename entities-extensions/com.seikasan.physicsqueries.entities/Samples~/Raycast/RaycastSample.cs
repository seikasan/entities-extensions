using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using Collider = Unity.Physics.Collider;
using BoxCollider = Unity.Physics.BoxCollider;

namespace PhysicsQueries.Entities.Samples
{
    /// <summary>
    /// 空のGameObjectに追加して再生します。Prefab、シーンのCollider、Inspector参照は不要です。
    /// </summary>
    public sealed class RaycastSample : MonoBehaviour
    {
        void Start() => Debug.Log("Physics Queries sample: " + (RaycastSampleRunner.RunOnce() ? "PASS (11 query kinds)" : "FAIL"), this);
    }

    public static class RaycastSampleRunner
    {
        public static bool RunOnce()
        {
            using var world = new World("Raycast sample");
            var collider = BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Size = new float3(2),
                Orientation = quaternion.identity,
                BevelRadius = 0
            });
            var physicsWorld = new PhysicsWorld(
                1,
                0,
                0);
            try
            {
                var target = world.EntityManager.CreateEntity();
                var bodies = physicsWorld.Bodies;
                bodies[0] = new RigidBody
                {
                    Collider = collider,
                    Entity = target,
                    Scale = 1,
                    WorldFromBody = new RigidTransform(quaternion.identity, new float3(
                        0,
                        0,
                        5))
                };
                physicsWorld.CollisionWorld.BuildBroadphase(
                    ref physicsWorld,
                    0,
                    float3.zero);
                using var results = new NativeArray<QuerySampleResult>(1, Allocator.TempJob);
                new RaycastSampleJob
                {
                    Physics = new PhysicsWorldSingleton
                    {
                        PhysicsWorld = physicsWorld
                    },
                    Results = results
                }.Run();

                return results[0].Mask == 2047 &&
                    results[0].Target == target &&
                    math.abs(results[0].Fraction - 0.2f) < 1e-5f;
            }
            finally
            {
                physicsWorld.Dispose();
                collider.Dispose();
            }
        }
    }

    public struct QuerySampleResult
    {
        public int Mask;
        public Entity Target;
        public float Fraction;
    }

    [BurstCompile]
    public struct RaycastSampleJob : IJob
    {
        [ReadOnly]
        public PhysicsWorldSingleton Physics;
        public NativeArray<QuerySampleResult> Results;
        public void Execute()
        {
            var result = new QuerySampleResult();
            var direction = new float3(
                0,
                0,
                4);
            var center = new float3(
                0,
                0,
                5);
            if (Physics.Raycast(
                float3.zero,
                direction,
                out var hit,
                20))
            {
                result.Mask |= 1;
                result.Target = hit.Entity;
                result.Fraction = hit.Fraction;
            }

            var rays = new NativeList<Unity.Physics.RaycastHit>(8, Allocator.Temp);
            var overlaps = new NativeList<DistanceHit>(8, Allocator.Temp);
            if (Physics.RaycastAll(
                float3.zero,
                direction,
                ref rays,
                20))
            {
                result.Mask |= 2;
            }

            if (Physics.SphereCast(
                float3.zero,
                0.5f,
                direction,
                out _,
                20))
            {
                result.Mask |= 4;
            }

            if (Physics.BoxCast(
                float3.zero,
                new float3(0.5f),
                direction,
                out _,
                20))
            {
                result.Mask |= 8;
            }

            if (Physics.CapsuleCast(
                float3.zero,
                math.up(),
                0.5f,
                direction,
                out _,
                20))
            {
                result.Mask |= 16;
            }

            if (Physics.CheckSphere(center, 1))
            {
                result.Mask |= 32;
            }

            if (Physics.CheckBox(center, new float3(1)))
            {
                result.Mask |= 64;
            }

            if (Physics.CheckCapsule(
                center,
                center + math.up(),
                1))
            {
                result.Mask |= 128;
            }

            if (Physics.OverlapSphere(
                center,
                1,
                ref overlaps))
            {
                result.Mask |= 256;
            }

            if (Physics.OverlapBox(
                center,
                new float3(1),
                ref overlaps))
            {
                result.Mask |= 512;
            }

            if (Physics.OverlapCapsule(
                center,
                center + math.up(),
                1,
                ref overlaps))
            {
                result.Mask |= 1024;
            }

            Results[0] = result;
            rays.Dispose();
            overlaps.Dispose();
        }
    }
}
