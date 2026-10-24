using System;
using System.Diagnostics;

namespace Entities.Authoring
{
    // GeneratorからEditorへ生成ソースを渡します。Editor側でフィールド型を再推測しません。
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class AutoAuthoringMetadataAttribute : Attribute
    {
        public string ComponentTypeName { get; }
        public string SourcePath { get; }
        public string AssemblyName { get; }
        public string AuthoringTypeName { get; }
        public string AuthoringSource { get; }

        public AutoAuthoringMetadataAttribute(
            string componentTypeName,
            string sourcePath,
            string assemblyName,
            string authoringTypeName,
            string authoringSource)
        {
            ComponentTypeName = componentTypeName;
            SourcePath = sourcePath;
            AssemblyName = assemblyName;
            AuthoringTypeName = authoringTypeName;
            AuthoringSource = authoringSource;
        }
    }
}
