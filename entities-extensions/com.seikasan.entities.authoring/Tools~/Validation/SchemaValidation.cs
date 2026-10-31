#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Entities.Authoring.Validation
{
    // 検証手順で独立したプロジェクトにコピーして使用します。パッケージのRuntimeには含めません。
    public static class SchemaValidation
    {
        private const string Folder = "Assets/EntitiesAuthoringSchema";
        private const string PrefabPath = Folder + "/Preserved.prefab";
        private const string Baseline = Folder + "/Baseline.txt";
        private static Type Component => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Entities.Authoring.Tests.Schema").GetTypes().Single(t => t.Name == "SchemaProbe");
        private static Type Authoring => Component.Assembly.GetType(Component.Namespace + ".SchemaProbeAuthoring", true);

        private static void Require(bool value, string message)
        {
            if (!value)
            {
                throw new InvalidOperationException(message);
            }
        }

        public static void CreateBaseline()
        {
            var go = new GameObject("Persistent generated authoring");
            try
            {
                var authoring = (MonoBehaviour)go.AddComponent(Authoring);
                var serialized = new SerializedObject(authoring);
                serialized.FindProperty("Value").intValue = 17;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                string script = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(authoring));
                Require(!string.IsNullOrEmpty(script), "Physical MonoScript association is required.");
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                File.WriteAllText(Baseline, AssetDatabase.AssetPathToGUID(script));
                BakeAndCheck(go, 17);
                Debug.Log("AEA_SCHEMA_BASELINE_OK guid=" + File.ReadAllText(Baseline));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            EditorApplication.Exit(0);
        }

        public static void VerifyChangedSchema()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var authoring = (MonoBehaviour)prefab.GetComponent(Authoring);
            Require(authoring != null, "Prefab lost its authoring reference after schema/namespace change.");
            var serialized = new SerializedObject(authoring);
            Require(serialized.FindProperty("Obsolete") == null, "Deleted field was not removed.");
            Require(serialized.FindProperty("Added") != null, "Added field was not synchronized.");
            Require(
                Authoring.GetField("Value", BindingFlags.Instance | BindingFlags.NonPublic).FieldType == typeof(float),
                "Changed field type was not synchronized.");
            string script = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(authoring));
            Require(AssetDatabase.AssetPathToGUID(script) == File.ReadAllText(Baseline), "Generated MonoScript GUID changed.");
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            try
            {
                var data = new SerializedObject(instance.GetComponent(Authoring));
                data.FindProperty("Value").floatValue = 42;
                data.FindProperty("Added").intValue = 3;
                data.ApplyModifiedPropertiesWithoutUndo();
                BakeAndCheck(instance, 42);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            Debug.Log("AEA_SCHEMA_CHANGE_OK namespace=" + Component.Namespace + " guid=" + File.ReadAllText(Baseline));
            EditorApplication.Exit(0);
        }

        public static void VerifyOrphan()
        {
            string path = AssetDatabase.GUIDToAssetPath(File.ReadAllText(Baseline));
            Require(File.Exists(path), "Authoring was deleted with its component.");
            var authoringType = AssetDatabase.LoadAssetAtPath<MonoScript>(path).GetClass();
            Require(authoringType != null, "Orphaned authoring no longer has a MonoScript class.");
            Require(authoringType.GetNestedType("Baker", BindingFlags.NonPublic) == null, "Deleted component still has a Baker.");
            Require(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent(authoringType) != null,
                "Prefab reference was destroyed.");
            Debug.Log("AEA_SCHEMA_ORPHAN_OK guid=" + File.ReadAllText(Baseline));
            EditorApplication.Exit(0);
        }

        private static void BakeAndCheck(GameObject go, float expected)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Entities.Authoring.Tests.Editor").GetType("Entities.Authoring.Tests.Editor.BakingSession", true);
            var session = (IDisposable)Activator.CreateInstance(type, true);
            try
            {
                type.GetMethod("Bake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, new object[] { new[] { go } });
                var value = type.GetMethod("Value", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(Component).Invoke(session, null);
                Require(
                    Convert.ToSingle(Component.GetField("Value").GetValue(value)) == expected,
                    "Generated Baker wrote the wrong field value.");
            }
            finally
            {
                session.Dispose();
            }
        }
    }
}
#endif
