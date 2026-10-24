using System;
using Unity.Entities;

namespace Entities.Authoring
{
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
    public sealed class AutoAuthoringAttribute : Attribute
    {
        public TransformUsageFlags TransformUsageFlags { get; }

        public AutoAuthoringAttribute(TransformUsageFlags transformUsageFlags = TransformUsageFlags.None) => TransformUsageFlags = transformUsageFlags;
    }
}
