using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace GameObjectBridge.Entities.VfxGraph.Editor
{
    /// <summary>
    /// 導入済みVFX Graph 17.3のテンプレートとEditorモデルからサンプルGraphを作成します。
    /// </summary>
    /// <remarks>
    /// VFX GraphのAuthoringモデルはinternalのため、この任意のEditor補助機能内だけでReflectionを使用します。
    /// </remarks>
    public static class BridgeVfxGraphFactory
    {
        [MenuItem("Assets/Create/GameObject Bridge/VFX Sample Graph")]
        static void CreateFromMenu()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/GameObjectBridgeSample.vfx");
            Selection.activeObject = Create(path);
        }

        public static VisualEffectAsset Create(string path)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".vfx", StringComparison.Ordinal))
            {
                throw new ArgumentException("Assetsフォルダー内の.vfxファイルのパスを指定してください。", nameof(path));
            }

            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                throw new ArgumentException("指定したパスにはすでにアセットが存在します。", nameof(path));
            }

            const string template = "Packages/com.unity.visualeffectgraph/Editor/Templates/02_Simple_Loop.vfx";

            if (!AssetDatabase.CopyAsset(template, path))
            {
                throw new InvalidOperationException("インストール済みのVFX Graph 17.3のサンプルテンプレートが見つかりません。");
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            var assembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .Single(a => a.GetName().Name == "Unity.VisualEffectGraph.Editor");
            var objectExtensions = assembly.GetType("UnityEditor.VFX.VisualEffectObjectExtensions", true);
            var resource = objectExtensions.GetMethod("GetResource").Invoke(null, new object[] { asset });
            var resourceExtensions = assembly.GetType("UnityEditor.VFX.VisualEffectResourceExtensions", true);
            var graph = resourceExtensions.GetMethod("GetOrCreateGraph").Invoke(null, new[] { resource });
            var parameterType = assembly.GetType("UnityEditor.VFX.VFXParameter", true);

            AddParameter(
                graph,
                parameterType,
                "BridgeFloat",
                typeof(float),
                10f);
            AddParameter(
                graph,
                parameterType,
                "BridgeVector",
                typeof(Vector3),
                Vector3.zero);
            AddParameter(
                graph,
                parameterType,
                "BridgeBool",
                typeof(bool),
                false);

            graph.GetType().GetMethod("UpdateSubAssets").Invoke(graph, null);
            resource.GetType().GetMethod("WriteAsset").Invoke(resource, null);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            return AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
        }

        static void AddParameter(
            object graph,
            Type parameterType,
            string name,
            Type type,
            object value)
        {
            var parameter = ScriptableObject.CreateInstance(parameterType);
            parameterType.GetMethod("Init").Invoke(parameter, new object[] { type });
            var setting = parameterType.GetMethod("SetSettingValue");
            setting.Invoke(parameter, new object[]
            {
                "m_ExposedName",
                name
            });
            setting.Invoke(parameter, new object[]
            {
                "m_Exposed",
                true
            });
            parameterType.GetProperty("value").SetValue(parameter, value);
            graph.GetType().GetMethod("AddChild").Invoke(graph, new object[]
            {
                parameter,
                -1,
                true
            });
        }
    }
}
