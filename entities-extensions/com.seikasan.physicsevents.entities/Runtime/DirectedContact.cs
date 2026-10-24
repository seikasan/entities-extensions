using System;
using Unity.Entities;

namespace PhysicsEvents.Entities
{
    internal struct DirectedContact : IComparable<DirectedContact>
    {
        public Entity Self;
        public Entity Other;
        public int CompareTo(DirectedContact other)
        {
            int self = Compare(Self, other.Self);

            return self != 0 ? self : Compare(Other, other.Other);
        }

        internal static int Compare(Entity left, Entity right)
        {
            int index = left.Index.CompareTo(right.Index);

            return index != 0 ? index : left.Version.CompareTo(right.Version);
        }
    }
}
