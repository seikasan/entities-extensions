using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace InputSystem.Entities.SourceGenerator
{
    [Generator]
    public sealed class InputActionsGenerator : ISourceGenerator
    {
        public const string AssemblyName = "InputSystem.Entities.Generated";
        private static readonly DiagnosticDescriptor InvalidMetadata = new DiagnosticDescriptor(
            "ISE001",
            "Invalid input metadata",
            "{0}",
            "InputSystem.Entities",
            DiagnosticSeverity.Error,
            true);
        private static readonly DiagnosticDescriptor InvalidDefinition = new DiagnosticDescriptor(
            "ISE002",
            "Input definition cannot be generated",
            "{0}",
            "InputSystem.Entities",
            DiagnosticSeverity.Error,
            true);
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.Compilation.AssemblyName != AssemblyName)
            {
                return;
            }

            foreach (var file in context.AdditionalFiles.Where(f => f.Path.EndsWith(".InputSystem.Entities.SourceGenerator.additionalfile", StringComparison.Ordinal)))
            {
                try
                {
                    var assets = Parse(file.GetText(context.CancellationToken)?.ToString() ?? "");
                    var namespaces = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var asset in assets)
                    {
                        string ns = asset.Primary ? "InputSystem.Entities" : "InputSystem.Entities.Generated." + Identifier(asset.Name);

                        if (!namespaces.Add(ns))
                        {
                            throw new ArgumentException("Multiple assets generate namespace " + ns + ". Give the assets distinct names.");
                        }

                        ValidateNames(asset);
                    }

                    foreach (var asset in assets)
                    {
                        context.AddSource("InputActions_" + asset.Guid + ".g.cs", SourceText.From(Generate(asset), Encoding.UTF8));
                    }
                }
                catch (ArgumentException ex)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidDefinition,
                        Location.None,
                        ex.Message));
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidMetadata,
                        Location.None,
                        "Regenerate input metadata: " + ex.Message));
                }
            }
        }

        private sealed class Asset
        {
            public string Guid, Name;
            public bool Primary;
            public readonly List<Map> Maps = new List<Map>();
        }

        private sealed class Map
        {
            public string Name, Id;
            public readonly List<Action> Actions = new List<Action>();
        }

        private sealed class Action
        {
            public string Name, Id, Kind;
        }

        private static List<Asset> Parse(string text)
        {
            var result = new List<Asset>();
            Asset asset = null;
            Map map = null;
            var lines = text.Replace("\r", "").Split('\n');

            if (lines.Length == 0 ||
                lines[0] != "InputSystem.Entities\t1")
            {
                throw new FormatException("Unsupported metadata version.");
            }

            foreach (var line in lines.Skip(1))
            {
                if (line.Length == 0)
                {
                    continue;
                }

                var parts = line.Split('\t');
                switch (parts[0])
                {
                    case "asset":
                        asset = new Asset
                        {
                            Guid = Guid.Parse(parts[1]).ToString("N"),
                            Name = Decode(parts[2]),
                            Primary = parts[3] == "1"
                        };
                        result.Add(asset);
                        map = null;
                        break;
                    case "map":
                        map = new Map
                        {
                            Id = Guid.Parse(parts[1]).ToString("N"),
                            Name = Decode(parts[2])
                        };
                        asset.Maps.Add(map);
                        break;
                    case "action":
                        map.Actions.Add(new Action
                        {
                            Id = Guid.Parse(parts[1]).ToString("N"),
                            Name = Decode(parts[2]),
                            Kind = parts[3]
                        });
                        break;
                    default:
                        throw new FormatException("Unknown metadata record.");
                }
            }

            return result;
        }

        private static string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
        private static string Identifier(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "@_";
            }

            var result = new StringBuilder("@");
            if (!SyntaxFacts.IsIdentifierStartCharacter(name[0]))
            {
                result.Append('_');
            }

            foreach (char c in name)
            {
                result.Append(SyntaxFacts.IsIdentifierPartCharacter(c) ? c : '_');
            }

            return result.ToString();
        }

        private static void ValidateNames(Asset asset)
        {
            var maps = new HashSet<string>(StringComparer.Ordinal)
            {
                "@InputActionAsset",
                "@FindAction",
                "@id"
            };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < asset.Maps.Count; i++)
            {
                var map = asset.Maps[i];

                if (!maps.Add(Identifier(map.Name)))
                {
                    throw new ArgumentException(asset.Name + ": map name conflicts after C# conversion: " + map.Name);
                }

                // 内部の入れ子型名がMapのプロパティ名と衝突しないようにします。
                if (asset.Maps.Any(m => Identifier(m.Name) == "@ActionMap" + i))
                {
                    throw new ArgumentException(asset.Name + ": reserved map name ActionMap" + i);
                }

                var actions = new HashSet<string>(StringComparer.Ordinal)
                {
                    "@ActionMap" + i,
                    "@id"
                };
                foreach (var action in map.Actions)
                {
                    if (!actions.Add(Identifier(action.Name)))
                    {
                        throw new ArgumentException(asset.Name + "/" + map.Name + ": action name conflicts after C# conversion: " + action.Name);
                    }

                    if (!ids.Add(action.Id))
                    {
                        throw new ArgumentException(asset.Name + ": duplicate action ID " + action.Id);
                    }

                    if (!new[]
                    {
                        "Float",
                        "Vector2",
                        "Vector3",
                        "Vector4",
                        "Quaternion",
                        "Integer"
                    }.Contains(action.Kind))
                    {
                        throw new ArgumentException(asset.Name + "/" + map.Name + "/" + action.Name + ": unsupported or unspecified value type. Set a supported Expected Control Type in the Input Actions Editor.");
                    }
                }
            }
        }

        private static string Hash(string hex, string indentation = "                ")
        {
            // Bakerと同じ文字列解析を使い、Burst側にはuint定数を生成します。
            // Unity.Entities.Hash128は8文字ごとにニブルの順序を反転して読み取ります。
            var values = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                uint value = 0;
                for (int j = 0; j < 8; j++)
                {
                    value |= Convert.ToUInt32(hex[i * 8 + j].ToString(), 16) << (j * 4);
                }

                values.Add("0x" + value.ToString("x8") + "u");
            }

            return "new global::Unity.Entities.Hash128(\n" + indentation + string.Join(",\n" + indentation, values) + ")";
        }

        private static string Generate(Asset asset)
        {
            string ns = asset.Primary ? "InputSystem.Entities" : "InputSystem.Entities.Generated." + Identifier(asset.Name);
            var b = new StringBuilder("// <auto-generated/>\nnamespace " + ns + "\n{\n");
            b.AppendLine("    public struct InputActionAsset : global::Unity.Entities.IComponentData");
            b.AppendLine("    {");
            b.AppendLine("        public global::Unity.Entities.Hash128 id { get; internal set; }");
            for (int i = 0; i < asset.Maps.Count; i++)
            {
                var map = asset.Maps[i];
                b.AppendLine("        public ActionMap" + i + " " + Identifier(map.Name) + ";");
                b.AppendLine("        public struct ActionMap" + i + "\n        {");
                b.AppendLine("            public global::Unity.Entities.Hash128 id => " + Hash(map.Id) + ";");
                foreach (var action in map.Actions)
                {
                    b.AppendLine("            public global::InputSystem.Entities.InputAction " + Identifier(action.Name) + ";");
                }

                b.AppendLine("        }");
            }

            b.AppendLine("        public global::InputSystem.Entities.InputAction FindAction(global::InputSystem.Entities.InputActionReference reference)");
            b.AppendLine("        {\n            if (reference.AssetId != id)\n            {\n                return default;\n            }\n");
            foreach (var map in asset.Maps)
            {
                foreach (var action in map.Actions)
                {
                    b.AppendLine("            if (reference.ActionId == " + Hash(action.Id) + ")\n            {\n                return " + Identifier(map.Name) + "." + Identifier(action.Name) + ";\n            }\n");
                }
            }

            b.AppendLine("            return default;\n        }\n    }");
            b.AppendLine("    [global::Unity.Entities.WorldSystemFilter(global::Unity.Entities.WorldSystemFilterFlags.LocalSimulation | global::Unity.Entities.WorldSystemFilterFlags.ClientSimulation)]");
            b.AppendLine("    [global::Unity.Entities.UpdateInGroup(typeof(global::InputSystem.Entities.InputCollectSystemGroup))]");
            b.AppendLine("    public sealed partial class InputCollectSystem : global::InputSystem.Entities.InputCollectSystem<InputActionAsset>\n    {");
            b.AppendLine("        protected override string AssetGuid => \"" + asset.Guid + "\";");
            int index = 0;
            foreach (var map in asset.Maps)
            {
                foreach (var action in map.Actions)
                {
                    b.AppendLine("        private global::UnityEngine.InputSystem.InputAction _action" + index++ + ";");
                }
            }

            b.AppendLine("        protected override void Initialize(global::UnityEngine.InputSystem.InputActionAsset asset)\n        {");
            index = 0;
            foreach (var map in asset.Maps)
            {
                foreach (var action in map.Actions)
                {
                    b.AppendLine("            _action" + index++ + " = asset.FindAction(\"" + Guid.Parse(action.Id).ToString("D") + "\", true);");
                }
            }

            b.AppendLine("        }\n        protected override InputActionAsset ReadActions()\n        {\n            var result = new InputActionAsset\n            {\n                id = " + Hash(asset.Guid, "                    ") + "\n            };");
            index = 0;
            foreach (var map in asset.Maps)
            {
                foreach (var action in map.Actions)
                {
                    b.AppendLine("            result." + Identifier(map.Name) + "." + Identifier(action.Name) + " = global::InputSystem.Entities.InputAction.Capture(\n                _action" + index++ + ",\n                global::InputSystem.Entities.InputValueType." + action.Kind + ",\n                " + Hash(action.Id, "                    ") + ");");
                }
            }

            b.AppendLine("\n            return result;\n        }\n    }\n}");

            return b.ToString();
        }
    }
}
