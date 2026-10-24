using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Entities.Authoring.Editor
{
    internal static class AuthoringMetadataReader
    {
        internal static IEnumerable<AutoAuthoringMetadataAttribute> Read()
            => AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(a => a.GetCustomAttributes(typeof(AutoAuthoringMetadataAttribute), false).Cast<AutoAuthoringMetadataAttribute>())
                .OrderBy(m => m.AssemblyName, StringComparer.Ordinal)
                .ThenBy(m => m.ComponentTypeName, StringComparer.Ordinal);

        internal static string PhysicalPath(string assetPath)
        {
            var package = PackageInfo.FindForAssetPath(assetPath);

            if (package != null)
            {
                return Path.GetFullPath(Path.Combine(package.resolvedPath, assetPath.Substring(("Packages/" + package.name + "/").Length)));
            }

            return Path.GetFullPath(assetPath);
        }

        internal static string ResolveSource(AutoAuthoringMetadataAttribute metadata)
        {
            string source = metadata.SourcePath.Replace('\\', '/');

            if (Path.IsPathRooted(source))
            {
                string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/') + "/";
                string relative = source.StartsWith(project, StringComparison.OrdinalIgnoreCase)
                    ? source[project.Length..]
                    : null;

                if (relative != null &&
                    (relative.StartsWith("Assets/", StringComparison.Ordinal) ||
                        relative.StartsWith("Packages/", StringComparison.Ordinal)))
                {
                    source = relative;
                }
                else
                {
                    var package = PackageInfo.GetAllRegisteredPackages()
                        .FirstOrDefault(p => source.StartsWith(p.resolvedPath.Replace('\\', '/') + "/", StringComparison.OrdinalIgnoreCase));

                    if (package == null)
                    {
                        throw new UnauthorizedAccessException("AEA006 " + metadata.ComponentTypeName + ": Source is outside Assets or registered packages. Move it to a writable source folder.");
                    }

                    source = "Packages/" + package.name + source.Substring(package.resolvedPath.Length);
                }
            }

            if (!source.StartsWith("Assets/", StringComparison.Ordinal) &&
                !source.StartsWith("Packages/", StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("AEA006 " + metadata.ComponentTypeName + ": Cannot locate declaration source '" + source + "'. Use a physical component file.");
            }

            if (!File.Exists(PhysicalPath(source)))
            {
                throw new FileNotFoundException(
                    "AEA006 " + metadata.ComponentTypeName + ": Declaration source was not found. Recompile after restoring or moving the source.",
                    source);
            }

            ValidateAssembly(source, metadata.AssemblyName);

            return source;
        }

        internal static string Owner(AutoAuthoringMetadataAttribute metadata, string source)
        {
            string guid = AssetDatabase.AssetPathToGUID(source);

            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidOperationException("AEA004 " + metadata.ComponentTypeName + ": Source asset has no GUID yet. Wait for its import, then recompile.");
            }

            string shortName = metadata.ComponentTypeName[(metadata.ComponentTypeName.LastIndexOf('.') + 1)..];

            return metadata.AssemblyName + ":" + guid + ":" + shortName;
        }

        internal static string Destination(
            AutoAuthoringMetadataAttribute metadata,
            string source,
            string owner)
        {
            string shortName = metadata.AuthoringTypeName.Substring(metadata.AuthoringTypeName.LastIndexOf('.') + 1);
            string encoded = AuthoringFileWriter.EncodeOwner(owner);
            var candidates = AssetDatabase.FindAssets(shortName + " t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p) == shortName &&
                            AuthoringFileWriter.ReadOwner(PhysicalPath(p)) == encoded)
                .ToArray();

            if (candidates.Length > 1)
            {
                throw new InvalidOperationException("AEA003 " + metadata.ComponentTypeName + ": Multiple owned authoring files exist. Keep one file and resolve duplicate scripts manually.");
            }

            string target = candidates.Length == 1
                ? candidates[0]
                : Path.GetDirectoryName(source).Replace('\\', '/') + "/" + shortName + ".cs";
            ValidateAssembly(target, metadata.AssemblyName);

            return target;
        }

        internal static bool IsWritable(string source)
        {
            var info = PackageInfo.FindForAssetPath(source);

            return (info == null ||
                info.source == PackageSource.Embedded ||
                info.source == PackageSource.Local) &&
                (!File.Exists(PhysicalPath(source)) ||
                    (File.GetAttributes(PhysicalPath(source)) & FileAttributes.ReadOnly) == 0);
        }

        private static void ValidateAssembly(string assetPath, string expected)
        {
            string actual = CompilationPipeline.GetAssemblyNameFromScriptPath(assetPath);

            if (actual != null)
            {
                actual = Path.GetFileName(actual);
            }

            if (actual != null &&
                actual.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                actual = actual[..^4];
            }

            if (actual != null &&
                actual != expected)
            {
                throw new InvalidOperationException("AEA003 " + assetPath + ": Authoring must stay in component assembly '" + expected + "'. Move the source/asmdef together.");
            }
        }
    }
}
