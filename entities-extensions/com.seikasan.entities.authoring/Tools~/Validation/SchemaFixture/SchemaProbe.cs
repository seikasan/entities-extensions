using Entities.Authoring;
using Unity.Entities;

namespace Entities.Authoring.Tests.Schema.First
{
    [AutoAuthoring(TransformUsageFlags.Dynamic)]
    public struct SchemaProbe : IComponentData
    {
        public int Value;
        public bool Obsolete;
    }
}
