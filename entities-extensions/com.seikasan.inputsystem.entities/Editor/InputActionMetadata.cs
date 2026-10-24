using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using ManagedAsset = UnityEngine.InputSystem.InputActionAsset;
using UnityInputSystem = UnityEngine.InputSystem.InputSystem;

namespace InputSystem.Entities.Editor
{
    [InitializeOnLoad]
    public static class InputActionMetadata
    {
        public const string OutputDirectory = "Assets/InputSystem.Entities.Generated";
        public const string MetadataPath = OutputDirectory + "/Actions.InputSystem.Entities.SourceGenerator.additionalfile";
        public const string RegistryPath = OutputDirectory + "/Resources/" + InputActionAssetRegistry.ResourceName + ".asset";
        private static bool _queued;

        static InputActionMetadata()
        {
            EditorApplication.projectChanged += QueueRegeneration;
            UnityInputSystem.onActionsChange += QueueRegeneration;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    QueueRegeneration();
                }
            };
            QueueRegeneration();
        }

        public static void QueueRegeneration()
        {
            if (_queued ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            _queued = true;
            EditorApplication.delayCall += RegenerateWhenReady;
        }

        private static void RegenerateWhenReady()
        {
            _queued = false;

            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                QueueRegeneration();
                return;
            }

            try
            {
                Regenerate();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Input System for Entities/Regenerate")]
        public static void Regenerate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            string metadata = CreateMetadata(out var entries);
            Directory.CreateDirectory(OutputDirectory + "/Resources");
            bool changed = WriteIfChanged(
                OutputDirectory + "/InputSystem.Entities.Generated.asmdef",
                "{\n  \"name\": \"InputSystem.Entities.Generated\",\n  \"rootNamespace\": \"InputSystem.Entities\",\n  \"references\": [\"InputSystem.Entities\", \"Unity.Entities\", \"Unity.InputSystem\", \"Unity.Mathematics\", \"Unity.Collections\"],\n  \"autoReferenced\": true\n}\n");
            changed |= WriteIfChanged(OutputDirectory + "/GeneratedAssembly.cs", "// Input System for EntitiesのSource Generator用アセンブリ識別ファイル。\n");
            changed |= WriteIfChanged(MetadataPath, metadata);

            if (changed)
            {
                AssetDatabase.Refresh();
            }

            var registry = AssetDatabase.LoadAssetAtPath<InputActionAssetRegistry>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<InputActionAssetRegistry>();
                registry.Assets = entries;
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }
            else if (!registry.Assets.Select(e => e.Guid).SequenceEqual(entries.Select(e => e.Guid)) ||
                !registry.Assets.Select(e => e.Asset).SequenceEqual(entries.Select(e => e.Asset)))
            {
                registry.Assets = entries;
                EditorUtility.SetDirty(registry);
                AssetDatabase.SaveAssetIfDirty(registry);
            }
        }

        public static string CreateMetadata(out InputActionAssetRegistry.Entry[] entries)
        {
            var primary = UnityInputSystem.actions;
            entries = AssetDatabase.FindAssets("t:InputActionAsset").Select(guid => new InputActionAssetRegistry.Entry
                {
                    Guid = guid,
                    Asset = AssetDatabase.LoadAssetAtPath<ManagedAsset>(AssetDatabase.GUIDToAssetPath(guid))
                }).Where(entry => entry.Asset != null &&
                                  (entry.Asset == primary ||
                                   IsProjectAsset(AssetDatabase.GetAssetPath(entry.Asset))))
                .OrderBy(entry => entry.Guid, StringComparer.Ordinal).ToArray();

            var output = new StringBuilder("InputSystem.Entities\t1\n");
            foreach (var entry in entries)
            {
                output.Append("asset\t").Append(entry.Guid).Append('\t').Append(Encode(entry.Asset.name)).Append('\t').Append(entry.Asset == primary ? "1" : "0").Append('\n');
                foreach (var map in entry.Asset.actionMaps)
                {
                    output.Append("map\t").Append(map.id.ToString("N")).Append('\t').Append(Encode(map.name)).Append('\n');
                    foreach (var action in map.actions)
                    {
                        string kind = GetValueKind(action);
                        output.Append("action\t").Append(action.id.ToString("N")).Append('\t').Append(Encode(action.name)).Append('\t').Append(kind).Append('\n');
                    }
                }
            }

            return output.ToString();
        }

        private static bool IsProjectAsset(string path)
        {
            if (path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return true;
            }

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);

            return package != null &&
                (package.source == UnityEditor.PackageManager.PackageSource.Embedded ||
                    package.source == UnityEditor.PackageManager.PackageSource.Local);
        }

        private static string GetValueKind(UnityEngine.InputSystem.InputAction action)
        {
            if (string.IsNullOrEmpty(action.expectedControlType) &&
                action.type == UnityEngine.InputSystem.InputActionType.Button)
            {
                return "Float";
            }

            var layout = string.IsNullOrEmpty(action.expectedControlType) ? null : UnityInputSystem.LoadLayout(action.expectedControlType);
            Type type = null;
            for (var controlType = layout?.type; controlType != null; controlType = controlType.BaseType)
            {
                if (!controlType.IsGenericType ||
                    controlType.GetGenericTypeDefinition() != typeof(UnityEngine.InputSystem.InputControl<>))
                {
                    continue;
                }

                type = controlType.GetGenericArguments()[0];
                break;
            }

            if (type == typeof(float))
            {
                return "Float";
            }

            if (type == typeof(int))
            {
                return "Integer";
            }

            if (type == typeof(Vector2))
            {
                return "Vector2";
            }

            if (type == typeof(Vector3))
            {
                return "Vector3";
            }

            if (type == typeof(Vector4))
            {
                return "Vector4";
            }

            if (type == typeof(Quaternion))
            {
                return "Quaternion";
            }

            return "Unsupported";
        }

        private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        private static bool WriteIfChanged(string path, string text)
        {
            if (File.Exists(path) &&
                File.ReadAllText(path) == text)
            {
                return false;
            }

            File.WriteAllText(
                path,
                text,
                new UTF8Encoding(false));

            return true;
        }
    }

    internal sealed class InputActionMetadataPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported,
            string[] deleted,
            string[] moved,
            string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(path => path.EndsWith(".inputactions", StringComparison.OrdinalIgnoreCase)))
            {
                InputActionMetadata.QueueRegeneration();
            }
        }
    }

    internal sealed class InputActionMetadataBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            string expected = InputActionMetadata.CreateMetadata(out _);

            if (!File.Exists(InputActionMetadata.MetadataPath) ||
                File.ReadAllText(InputActionMetadata.MetadataPath) != expected ||
                AssetDatabase.LoadAssetAtPath<InputActionAssetRegistry>(InputActionMetadata.RegistryPath) == null)
            {
                InputActionMetadata.Regenerate();

                throw new BuildFailedException("Input definitions changed. Wait for Input System for Entities generation and compilation, then build again.");
            }
        }
    }
}
