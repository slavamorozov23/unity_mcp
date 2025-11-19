using System;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace SceneAPI.Modules
{
    public static class SaveObjectAsPrefabModule
    {
        public static string Execute(HttpListenerContext context)
        {
            try
            {
                string requestBody = GetRequestBody(context);
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);
                
                string objectPath = data?.path;
                string prefabName = data?.prefabName ?? "";
                string targetFolder = data?.targetFolder ?? "Assets/Prefabs";

                if (string.IsNullOrEmpty(objectPath))
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "Object path is required" });
                }

                GameObject obj = GameObjectUtilities.FindGameObjectByPath(objectPath);
                if (obj == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "Object not found" });
                }

                if (!AssetDatabase.IsValidFolder(targetFolder))
                {
                    // Пытаемся создать папку, если ее нет
                    string parent = Path.GetDirectoryName(targetFolder).Replace("\\", "/");
                    string folderName = Path.GetFileName(targetFolder);
                    if (!AssetDatabase.IsValidFolder(parent))
                    {
                        return JsonConvert.SerializeObject(new { success = false, error = $"Target folder does not exist and cannot be created: {targetFolder}" });
                    }
                    AssetDatabase.CreateFolder(parent, folderName);
                }

                string saveName = string.IsNullOrEmpty(prefabName) ? obj.name : prefabName;
                string assetPath = $"{targetFolder}/{saveName}.prefab";

                // Сохраняем как префаб
                PrefabUtility.SaveAsPrefabAsset(obj, assetPath, out bool success);

                if (!success)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "Failed to save object as prefab" });
                }

                AssetDatabase.Refresh();
                return JsonConvert.SerializeObject(new { success = true, prefabPath = assetPath, message = $"Prefab saved: {assetPath}" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = $"Error saving object as prefab: {ex.Message}" });
            }
        }

        private static string GetRequestBody(HttpListenerContext context)
        {
            using (var reader = new System.IO.StreamReader(context.Request.InputStream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}