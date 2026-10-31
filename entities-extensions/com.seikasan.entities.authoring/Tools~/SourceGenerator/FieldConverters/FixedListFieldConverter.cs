using Microsoft.CodeAnalysis;

namespace Entities.Authoring.Generator
{
    internal static class FixedListFieldConverter
    {
        internal static FieldModel Convert(
            IFieldSymbol field,
            string local,
            string component,
            string source = null)
        {
            var type = field.Type as INamedTypeSymbol;

            if (type == null ||
                type.ContainingNamespace.ToDisplayString() != "Unity.Collections" ||
                type.Arity != 1 ||
                !FixedStringFieldConverter.HasSize(type.Name, "FixedList") ||
                !DirectFieldConverter.IsScalar(type.TypeArguments[0]))
            {
                return null;
            }

            source = source ?? "authoring." + Code.Identifier(field.Name);
            string listType = "global::System.Collections.Generic.List<" + Code.Type(type.TypeArguments[0]) + ">";
            string error = Code.Literal("AEA008 " + component + "." + field.Name + ": list count exceeds FixedList capacity. Remove elements; none will be discarded silently.");

            return new FieldModel
            {
                Symbol = field,
                AuthoringType = listType,
                Initializer = " = new " + listType + "()",
                Value = local,
                Preparation = "var " + local + " = new " + Code.Type(type) + "();\n\n" + "if (" + source + " != null)\n{\n" + "    if (" + source + ".Count > " + local + ".Capacity)\n" + "    {\n        global::UnityEngine.Debug.LogError(" + error + ", authoring);\n\n        return;\n    }\n\n" + "    for (int i = 0; i < " + source + ".Count; i++)\n    {\n        " + local + ".Add(" + source + "[i]);\n    }\n}\n"
            };
        }
    }
}
