using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using UnityEditor;

namespace SceneAPI.Modules
{
    public static class GetPrefabsListModule
    {
        public static string Execute(HttpListenerContext context)
        {
            try
            {
                // Стандартное место хранения префабов
                string prefabsFolder = "Assets/Prefabs";
                if (!AssetDatabase.IsValidFolder(prefabsFolder))
                {
                    return JsonConvert.SerializeObject(new { success = true, prefabs = new string[0], message = "Prefabs folder not found" });
                }

                List<string> prefabs = new List<string>();
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabsFolder });
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    prefabs.Add(path);
                }

                return JsonConvert.SerializeObject(new { success = true, prefabs = prefabs });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = $"Error getting prefabs list: {ex.Message}" });
            }
        }
    }
}