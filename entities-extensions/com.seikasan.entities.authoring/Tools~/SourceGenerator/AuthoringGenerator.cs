using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Entities.Authoring.Generator
{
    [Generator]
    public sealed class AuthoringGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var components = context.SyntaxProvider.ForAttributeWithMetadataName(
                "Entities.Authoring.AutoAuthoringAttribute",
                (node, _) => node is TypeDeclarationSyntax,
                (attribute, _) => (INamedTypeSymbol)attribute.TargetSymbol).Collect();
            context.RegisterSourceOutput(context.CompilationProvider.Combine(components), (output, input) =>
            {
                var compilation = input.Left;
                var models = new List<ComponentModel>();
                foreach (var symbol in input.Right.Distinct(SymbolEqualityComparer.Default))
                {
                    var model = ComponentAnalyzer.Analyze(
                        (INamedTypeSymbol)symbol,
                        compilation,
                        output.ReportDiagnostic);
                    if (model != null)
                    {
                        models.Add(model);
                    }
                }

                foreach (var group in models.GroupBy(m => m.AuthoringType))
                {
                    if (group.Count() > 1)
                    {
                        foreach (var duplicate in group)
                        {
                            output.ReportDiagnostic(Diagnostic.Create(
                                Diagnostics.NameCollision,
                                duplicate.Symbol.Locations.FirstOrDefault(),
                                duplicate.MetadataName + ": Multiple components require " + duplicate.AuthoringType + ". Rename a component or change its namespace."));
                        }

                        continue;
                    }

                    var model = group.Single();
                    var authoring = compilation.GetTypeByMetadataName(model.AuthoringType);
                    if (authoring != null &&
                        (!SymbolEqualityComparer.Default.Equals(authoring.ContainingAssembly, compilation.Assembly) ||
                            !BakerEmitter.IsOwned(authoring) ||
                            authoring.GetTypeMembers("Baker").Length != 0 ||
                            authoring.TypeKind != TypeKind.Class ||
                            authoring.BaseType?.ToDisplayString() != "UnityEngine.MonoBehaviour"))
                    {
                        output.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.NameCollision,
                            model.Symbol.Locations.FirstOrDefault(),
                            model.MetadataName + ": " + model.AuthoringType + " is already declared by handwritten code or another assembly. Rename or remove the conflicting class; it will not be overwritten."));
                        continue;
                    }

                    string hint = "AEA." + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(model.MetadataName)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
                    output.AddSource(hint + ".Metadata.g.cs", MetadataEmitter.Emit(model));
                    if (authoring == null)
                    {
                        continue;
                    }

                    if (!BakerEmitter.Matches(authoring, model))
                    {
                        output.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.OutOfSync,
                            model.Symbol.Locations.FirstOrDefault(),
                            model.MetadataName + ": Authoring fields are awaiting Editor synchronization. The Baker is withheld until the complete schema matches."));
                        continue;
                    }

                    output.AddSource(hint + ".Baker.g.cs", BakerEmitter.Emit(model));
                }
            });
        }
    }
}
