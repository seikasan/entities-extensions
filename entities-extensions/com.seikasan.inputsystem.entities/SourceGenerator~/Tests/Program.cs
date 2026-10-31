using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using InputSystem.Entities.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

internal static class Program
{
    private sealed class TextFile : AdditionalText
    {
        private readonly string _text;
        public TextFile(string text)
        {
            _text = text;
        }

        public override string Path => "Actions.InputSystem.Entities.SourceGenerator.additionalfile";

        public override SourceText GetText(System.Threading.CancellationToken cancellationToken = default) => SourceText.From(_text);
    }

    private static string Enc(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    private static string Metadata(
        string map,
        string action,
        string kind = "Vector2",
        bool primary = true) => "InputSystem.Entities\t1\nasset\t0123456789abcdef0123456789abcdef\t" + Enc("Flight Controls") + "\t" + (primary ? "1" : "0") + "\nmap\tfedcba9876543210fedcba9876543210\t" + Enc(map) + "\naction\t11111111222222223333333344444444\t" + Enc(action) + "\t" + kind + "\n";
    private static GeneratorDriverRunResult Run(string metadata, string assembly = InputActionsGenerator.AssemblyName)
    {
        var compilation = CSharpCompilation.Create(assembly, new[] { CSharpSyntaxTree.ParseText("internal class Anchor {}") });
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new InputActionsGenerator() }, new[] { new TextFile(metadata) });

        return driver.RunGenerators(compilation).GetRunResult();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }

        Console.WriteLine("PASS " + message);
    }

    public static void Main()
    {
        var result = Run(Metadata("Flight", "Thrust"));
        string code = result.GeneratedTrees.Single().ToString();
        Check(
            result.Diagnostics.Length == 0 &&
                code.Contains("@Flight") &&
                code.Contains("@Thrust") &&
                !code.Contains("Gameplay"),
            "Arbitrary map and action names");
        Check(code.Contains("0x76543210u") &&
            code.Contains("0xfedcba98u"), "GUID constants match Unity hash nibble order");
        Check(
            CSharpSyntaxTree.ParseText(code).GetDiagnostics().All(d => d.Severity != DiagnosticSeverity.Error),
            "Generated C# syntax is valid");
        Check(Run(Metadata("class", "照準-方向")).Diagnostics.Length == 0, "Keywords, Unicode and identifier conversion");
        Check(Run(Metadata("Flight", "Thrust"), "Other.Assembly").GeneratedTrees.Length == 0, "Generation is scoped to one assembly");
        Check(
            Run(Metadata(
                "Flight",
                "Thrust",
                "Pose")).Diagnostics.Any(d => d.Id == "ISE002"),
            "Unsupported values produce an actionable error");
        Check(
            Run(Metadata("Flight", "Thrust") + "action\t55555555666666667777777788888888\t" + Enc("Thrust") + "\tFloat\n").Diagnostics.Any(d => d.Id == "ISE002"),
            "Duplicate C# member names are rejected");
        Check(Run("bad metadata").Diagnostics.Any(d => d.Id == "ISE001"), "Malformed metadata is rejected");
        Check(
            Run(Metadata(
                "Flight",
                "Thrust",
                primary: false)).GeneratedTrees.Single().ToString().Contains("namespace InputSystem.Entities.Generated.@Flight_Controls"),
            "Secondary assets have their own namespace");
        Check(Run(Metadata("Flight", "Thrust")).GeneratedTrees.Single().ToString() == code, "Generation is deterministic");
        var empty = Run("InputSystem.Entities\t1\nasset\t0123456789abcdef0123456789abcdef\t" + Enc("Empty") + "\t0\n");
        Check(
            empty.Diagnostics.Length == 0 &&
                empty.GeneratedTrees.Single().ToString().Contains("id { get; internal set; }"),
            "Empty assets retain stored component data");
        if (Environment.GetCommandLineArgs().Length > 1)
        {
            File.WriteAllText(Environment.GetCommandLineArgs()[1], code);
        }
    }
}
