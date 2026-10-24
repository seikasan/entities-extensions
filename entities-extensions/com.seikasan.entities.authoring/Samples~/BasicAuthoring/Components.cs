using Entities.Authoring;
using Unity.Collections;
using Unity.Entities;

namespace Entities.Authoring.Samples
{
    [AutoAuthoring(TransformUsageFlags.Dynamic)]
    public struct MoveSpeed : IComponentData
    {
        public float Value;
    }

    [AutoAuthoring]
    public struct Spawner : IComponentData
    {
        public Entity Prefab;
        public float Interval;
        public FixedString64Bytes DisplayName;
        public FixedList64Bytes<int> SpawnPattern;
    }

    // Inspectorのリストで各敵のパラメータを設定し、DynamicBufferとしてBakingします。
    [AutoAuthoring]
    public struct WaveEnemy : IBufferElementData
    {
        public Entity Prefab;
        public float SpawnDelay;
        public FixedString64Bytes Label;
    }

    [AutoAuthoring]
    public struct PlayerTag : IComponentData, IEnableableComponent
    {
    }
}
