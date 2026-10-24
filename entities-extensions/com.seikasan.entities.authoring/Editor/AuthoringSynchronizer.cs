using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Entities.Authoring.Editor
{
    [InitializeOnLoad]
    internal static class AuthoringSynchronizer
    {
        private static bool _queued;
        static AuthoringSynchronizer() => Schedule();
        private static void Schedule()
        {
            if (_queued) return;

            _queued = true;
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            _queued = false;

            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                Schedule();
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            SynchronizeAll();
        }

        internal static int SynchronizeAll()
        {
            int changed = 0;
            foreach (var metadata in AuthoringMetadataReader.Read())
            {
                try
                {
                    string source = AuthoringMetadataReader.ResolveSource(metadata);
                    string owner = AuthoringMetadataReader.Owner(metadata, source);
                    string target = AuthoringMetadataReader.Destination(
                        metadata,
                        source,
                        owner);
                    if (AuthoringFileWriter.Write(
                        AuthoringMetadataReader.PhysicalPath(target),
                        owner,
                        metadata.AuthoringSource,
                        AuthoringMetadataReader.IsWritable(source) &&
                            AuthoringMetadataReader.IsWritable(target)))
                    {
                        changed++;
                    }
                }
                catch (Exception exception)
                    when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    Debug.LogError("Entities Authoring: " + metadata.ComponentTypeName + ": " + exception.Message);
                }
            }

            if (changed != 0)
            {
                Debug.Log("Entities Authoring: synchronized " + changed + " authoring file(s).");
                AssetDatabase.Refresh();
            }

            return changed;
        }
    }
}
