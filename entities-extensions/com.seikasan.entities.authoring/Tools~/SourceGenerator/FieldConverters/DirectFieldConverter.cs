using Microsoft.CodeAnalysis;

namespace Entities.Authoring.Generator
{
    internal static class DirectFieldConverter
    {
        internal static bool IsScalar(ITypeSymbol type)
        {
            if (type.TypeKind == TypeKind.Enum)
            {
                var underlying = ((INamedTypeSymbol)type).EnumUnderlyingType.SpecialType;

                return underlying != SpecialType.System_Int64 &&
                    underlying != SpecialType.System_UInt64;
            }

            switch (type.SpecialType)
            {
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Boolean:
                    return true;
                default:
                    return false;
            }
        }

        internal static FieldModel Convert(IFieldSymbol field, string source = null)
        {
            var type = field.Type;
            bool math = type.ContainingNamespace?.ToDisplayString() == "Unity.Mathematics" &&
                (type.Name == "float2" ||
                    type.Name == "float3" ||
                    type.Name == "float4" ||
                    type.Name == "quaternion");

            if (!IsScalar(type) &&
                !math)
            {
                return null;
            }

            return new FieldModel
            {
                Symbol = field,
                AuthoringType = Code.Type(type),
                Value = source ?? "authoring." + Code.Identifier(field.Name)
            };
        }
    }
}
