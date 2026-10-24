using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace PhysicsQueries.Entities.Samples
{
    public struct Grounded : IComponentData
    {
        public bool Value;
    }

    [BurstCompile]
    public partial struct GroundCheckJob : IJobEntity
    {
        [ReadOnly]
        public PhysicsWorldSingleton Physics;
        public void Execute(ref Grounded grounded, in LocalTransform transform) => grounded.Value = Physics.Raycast(
            transform.Position,
            new float3(
                0,
                -1,
                0),
            out _,
            0.2f);
    }

    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [BurstCompile]
    public partial struct GroundCheckSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<Grounded>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 物理の依存関係をEntitiesに追跡させるためSystemAPIで取得し、Jobのスケジュール時にも引き継ぎます。
            var physics = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            state.Dependency = new GroundCheckJob
            {
                Physics = physics
            }.ScheduleParallel(state.Dependency);
        }
    }
}
