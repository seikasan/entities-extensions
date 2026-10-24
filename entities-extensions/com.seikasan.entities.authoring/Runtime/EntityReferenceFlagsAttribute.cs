using System;
using Unity.Entities;

namespace Entities.Authoring
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class EntityReferenceFlagsAttribute : Attribute
    {
        public TransformUsageFlags TransformUsageFlags { get; }

        public EntityReferenceFlagsAttribute(TransformUsageFlags transformUsageFlags) => TransformUsageFlags = transformUsageFlags;
    }
}
