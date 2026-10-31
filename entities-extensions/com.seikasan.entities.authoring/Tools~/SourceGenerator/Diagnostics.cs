using Microsoft.CodeAnalysis;

namespace Entities.Authoring.Generator
{
    internal static class Diagnostics
    {
        internal static readonly DiagnosticDescriptor InvalidComponent = Make("AEA001", "Invalid auto-authoring component");
        internal static readonly DiagnosticDescriptor UnsupportedField = Make("AEA002", "Unsupported auto-authoring field");
        internal static readonly DiagnosticDescriptor NameCollision = Make("AEA003", "Authoring name collision");
        internal static readonly DiagnosticDescriptor OutOfSync = Make(
            "AEA004",
            "Authoring synchronization required",
            DiagnosticSeverity.Info);
        internal static readonly DiagnosticDescriptor NotSerializable = Make("AEA005", "Field cannot be serialized by this package");
        internal static readonly DiagnosticDescriptor ReadOnly = Make("AEA006", "Authoring destination is not writable");
        internal static readonly DiagnosticDescriptor InvalidAttribute = Make("AEA007", "Invalid auto-authoring declaration");
        private static DiagnosticDescriptor Make(
            string id,
            string title,
            DiagnosticSeverity severity = DiagnosticSeverity.Error) => new DiagnosticDescriptor(
                id,
                title,
                "{0}",
                "Entities.Authoring",
                severity,
                true);
    }
}
