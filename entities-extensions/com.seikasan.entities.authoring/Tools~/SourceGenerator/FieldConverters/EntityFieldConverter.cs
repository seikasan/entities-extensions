using Microsoft.CodeAnalysis;

namespace Entities.Authoring.Generator
{
    internal static class EntityFieldConverter
    {
        internal static bool IsEntity(ITypeSymbol type) => type.ToDisplayString() == "Unity.Entities.Entity";
        internal static FieldModel Convert(IFieldSymbol field, int flags, string source = null)
        {
            if (!IsEntity(field.Type))
            {
                return null;
            }

            source = source ?? "authoring." + Code.Identifier(field.Name);

            return new FieldModel
            {
                Symbol = field,
                AuthoringType = "global::UnityEngine.GameObject",
                Value = source + " == null ? global::Unity.Entities.Entity.Null : GetEntity(" + source + ", (global::Unity.Entities.TransformUsageFlags)" + flags + ")"
            };
        }
    }
}
