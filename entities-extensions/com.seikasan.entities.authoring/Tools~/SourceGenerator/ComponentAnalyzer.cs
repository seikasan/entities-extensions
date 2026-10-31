using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Entities.Authoring.Generator
{
    internal static class ComponentAnalyzer
    {
        internal static ComponentModel Analyze(
            INamedTypeSymbol type,
            Compilation compilation,
            Action<Diagnostic> report)
        {
            bool HasInterface(string name) => type.AllInterfaces.Any(i => i.ToDisplayString() == name);
            void Error(
                DiagnosticDescriptor descriptor,
                ISymbol symbol,
                string message) => report(Diagnostic.Create(
                    descriptor,
                    symbol.Locations.FirstOrDefault(),
                    Code.MetadataName(type) + ": " + message));

            bool isBuffer = HasInterface("Unity.Entities.IBufferElementData");
            bool isComponent = HasInterface("Unity.Entities.IComponentData");
            if (type.TypeKind != TypeKind.Struct ||
                !type.IsUnmanagedType ||
                (!isComponent && !isBuffer) ||
                HasInterface("Unity.Entities.ICleanupComponentData") ||
                (isComponent && isBuffer))
            {
                Error(
                    Diagnostics.InvalidComponent,
                    type,
                    "[AutoAuthoring]にはunmanagedなIComponentDataまたはIBufferElementDataのstructが必要です。Managed型とCleanup型には対応していません。");

                return null;
            }

            for (var parent = type; parent != null; parent = parent.ContainingType)
            {
                if (parent.Arity != 0 ||
                    parent.DeclaredAccessibility == Accessibility.Private ||
                    parent.DeclaredAccessibility == Accessibility.Protected ||
                    parent.DeclaredAccessibility == Accessibility.ProtectedAndInternal)
                {
                    Error(
                        Diagnostics.InvalidComponent,
                        type,
                        "Generic or inaccessible component declarations cannot be authored. Use a non-generic public/internal type.");

                    return null;
                }
            }

            var attribute = type.GetAttributes().First(a => a.AttributeClass?.ToDisplayString() == "Entities.Authoring.AutoAuthoringAttribute");
            var syntax = attribute.ApplicationSyntaxReference;
            string metadataName = Code.MetadataName(type);
            string ns = type.ContainingNamespace.IsGlobalNamespace ? "" : metadataName.Substring(0, metadataName.LastIndexOf('.'));
            var model = new ComponentModel
            {
                Symbol = type,
                ComponentType = Code.Type(type),
                MetadataName = metadataName,
                Namespace = ns,
                AuthoringName = type.Name + "Authoring",
                SourcePath = syntax?.SyntaxTree.FilePath ?? "",
                AssemblyName = compilation.AssemblyName,
                IsBuffer = isBuffer
            };
            model.AuthoringType = (ns.Length == 0 ? "" : ns + ".") + model.AuthoringName;

            if (!ReadFlags(
                attribute,
                0,
                out model.Flags))
            {
                Error(
                    Diagnostics.InvalidAttribute,
                    type,
                    "Unknown TransformUsageFlags bits. Use a defined combination of TransformUsageFlags.");

                return null;
            }

            if (model.SourcePath.Length == 0)
            {
                Error(
                    Diagnostics.ReadOnly,
                    type,
                    "No physical source file is available. Place the component in a writable Assets or embedded/local Package source file.");

                return null;
            }

            if (type.GetMembers().OfType<IFieldSymbol>().Any(f => !f.IsStatic &&
                f.IsImplicitlyDeclared &&
                f.AssociatedSymbol is IPropertySymbol))
            {
                Error(
                    Diagnostics.InvalidAttribute,
                    type,
                    "Auto-properties have hidden state. Replace auto-properties with public fields for authoring.");

                return null;
            }

            if (isBuffer)
            {
                model.BufferElementName = "__EntitiesAuthoringBufferElement";
                while (type.GetMembers(model.BufferElementName).Length != 0 ||
                       model.BufferElementName == model.AuthoringName ||
                       model.BufferElementName == "Baker")
                {
                    model.BufferElementName += "_";
                }

                model.BufferListName = "__EntitiesAuthoringBufferElements";
                while (type.GetMembers(model.BufferListName).Length != 0 ||
                       model.BufferListName == model.BufferElementName ||
                       model.BufferListName == model.AuthoringName)
                {
                    model.BufferListName += "_";
                }
            }

            bool valid = true;
            foreach (var field in type.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsStatic &&
                !f.IsImplicitlyDeclared))
            {
                if (field.IsReadOnly ||
                    field.DeclaredAccessibility != Accessibility.Public ||
                    field.IsFixedSizeBuffer)
                {
                    Error(
                        Diagnostics.UnsupportedField,
                        field,
                        "Field '" + field.Name + "' must be a writable public field; readonly/private/fixed fields cannot be copied. Change its declaration.");
                    valid = false;
                    continue;
                }

                if (field.Name == "Baker" ||
                    field.Name == model.AuthoringName)
                {
                    Error(
                        Diagnostics.NameCollision,
                        field,
                        "Field '" + field.Name + "' conflicts with the required authoring/Baker type name. Rename this field.");
                    valid = false;
                    continue;
                }

                int referenceFlags = 2;
                var referenceAttribute = field.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Entities.Authoring.EntityReferenceFlagsAttribute");
                if (referenceAttribute != null &&
                    (!EntityFieldConverter.IsEntity(field.Type) ||
                        !ReadFlags(
                            referenceAttribute,
                            2,
                            out referenceFlags)))
                {
                    Error(
                        Diagnostics.InvalidAttribute,
                        field,
                        "Field '" + field.Name + "': [EntityReferenceFlags] requires an Entity field and valid TransformUsageFlags. Fix or remove the attribute.");
                    valid = false;
                    continue;
                }

                string local = "__aeaValue" + model.Fields.Count;
                string source = isBuffer ? "__aeaItem." + Code.Identifier(field.Name) : null;
                var converted = DirectFieldConverter.Convert(field, source) ??
                    EntityFieldConverter.Convert(field, referenceFlags, source) ??
                    FixedStringFieldConverter.Convert(field, local, metadataName, source) ??
                    FixedListFieldConverter.Convert(field, local, metadataName, source);
                if (converted == null)
                {
                    bool unsupported = field.Type is INamedTypeSymbol named &&
                        named.IsGenericType ||
                        field.Type.TypeKind == TypeKind.Pointer;
                    Error(
                        unsupported ? Diagnostics.UnsupportedField : Diagnostics.NotSerializable,
                        field,
                        "Field '" + field.Name + "' of type '" + field.Type + "' is not supported by the verified serialization/conversion rules. Use a supported field type or a handwritten Baker.");
                    valid = false;
                    continue;
                }

                model.Fields.Add(converted);
            }

            if (isBuffer && model.Fields.Count == 0)
            {
                Error(
                    Diagnostics.InvalidComponent,
                    type,
                    "IBufferElementDataには少なくとも1つのデータフィールドが必要です。");
                valid = false;
            }

            if (!valid)
            {
                return null;
            }

            if (HasInterface("Unity.Entities.IEnableableComponent"))
            {
                model.EnabledField = "__EntitiesAuthoringEnabled";
                while (type.GetMembers(model.EnabledField).Length != 0 ||
                       model.EnabledField == model.BufferListName ||
                       model.EnabledField == model.BufferElementName)
                {
                    model.EnabledField += "_";
                }
            }

            return model;
        }

        private static bool ReadFlags(
            AttributeData attribute,
            int fallback,
            out int value)
        {
            value = fallback;

            if (attribute.ConstructorArguments.Length == 0)
            {
                return true;
            }

            var constant = attribute.ConstructorArguments[0];

            if (constant.Kind == TypedConstantKind.Error ||
                constant.Value == null)
            {
                return false;
            }

            value = Convert.ToInt32(constant.Value);

            return value >= 0 &&
                (value & ~31) == 0;
        }
    }
}
