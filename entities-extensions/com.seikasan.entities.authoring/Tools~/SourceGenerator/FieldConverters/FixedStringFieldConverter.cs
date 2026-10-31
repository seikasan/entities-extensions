using Microsoft.CodeAnalysis;

namespace Entities.Authoring.Generator
{
    internal static class FixedStringFieldConverter
    {
        internal static bool HasSize(string name, string prefix) => name == prefix + "32Bytes" ||
            name == prefix + "64Bytes" ||
            name == prefix + "128Bytes" ||
            name == prefix + "512Bytes" ||
            name == prefix + "4096Bytes";
        internal static FieldModel Convert(
            IFieldSymbol field,
            string local,
            string component,
            string source = null)
        {
            if (field.Type.ContainingNamespace?.ToDisplayString() != "Unity.Collections" ||
                !HasSize(field.Type.Name, "FixedString"))
            {
                return null;
            }

            string type = Code.Type(field.Type);
            source = (source ?? "authoring." + Code.Identifier(field.Name)) + " ?? global::System.String.Empty";
            string error = Code.Literal("AEA008 " + component + "." + field.Name + ": UTF-8 byte count exceeds FixedString capacity. Shorten the text; it will not be truncated.");

            return new FieldModel
            {
                Symbol = field,
                AuthoringType = "string",
                Initializer = " = global::System.String.Empty",
                Value = local,
                Preparation = "var " + local + " = new " + type + "();\n\n" + "if (global::System.Text.Encoding.UTF8.GetByteCount(" + source + ") > " + local + ".Capacity)\n" + "{\n    global::UnityEngine.Debug.LogError(" + error + ", authoring);\n\n    return;\n}\n\n" + local + " = new " + type + "(" + source + ");\n"
            };
        }
    }
}
