using System;
using System.IO;
using UnityEditor;

namespace UnityAgentBridge.Editor
{
    [InitializeOnLoad]
    internal static class AssetRefreshService
    {
        static AssetRefreshService()
        {
            EditorApplication.update += ProcessScheduled;
        }

        public static string Schedule(string requestId)
        {
            var directory = Path.Combine(BridgePaths.RuntimeRoot, "Refresh");
            Directory.CreateDirectory(directory);
            var pending = Path.Combine(directory, requestId + ".pending");
            File.WriteAllText(pending, "scheduled");
            return pending;
        }

        private static void ProcessScheduled()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            var directory = Path.Combine(BridgePaths.RuntimeRoot, "Refresh");
            if (!Directory.Exists(directory))
                return;
            foreach (var pending in Directory.GetFiles(directory, "*.pending"))
            {
                var state = File.ReadAllText(pending);
                if (state == "running")
                {
                    File.WriteAllText(pending, "complete");
                    continue;
                }
                if (state != "acknowledged")
                    continue;
                try
                {
                    File.WriteAllText(pending, "running");
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    if (File.Exists(pending))
                        File.WriteAllText(pending, "complete");
                }
                catch (Exception exception)
                {
                    File.WriteAllText(pending, "error:" + exception.Message);
                    UnityEngine.Debug.LogException(exception);
                }
                break;
            }
        }
    }
}
