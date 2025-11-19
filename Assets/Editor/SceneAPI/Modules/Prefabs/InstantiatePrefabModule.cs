using System;
using System.Net;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace SceneAPI.Modules
{
    public static class InstantiatePrefabModule
    {
        public static string Execute(HttpListenerContext context)
        {
            try
            {
                string requestBody = GetRequestBody(context);
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);

                string prefabPath = data?.prefabPath;
                string parentPath = data?.parentPath ?? "";
                string nameOverride = data?.name ?? "";

                if (string.IsNullOrEmpty(prefabPath))
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "prefabPath is required" });
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = $"Prefab not found at path: {prefabPath}" });
                }

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "Failed to instantiate prefab" });
                }

                if (!string.IsNullOrEmpty(nameOverride))
                {
                    instance.name = nameOverride;
                }

                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parent = GameObjectUtilities.FindGameObjectByPath(parentPath);
                    if (parent != null)
                    {
                        instance.transform.SetParent(parent.transform);
                    }
                }

                string fullPath = string.IsNullOrEmpty(parentPath) ? instance.name : $"{parentPath}/{instance.name}";

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    path = fullPath,
                    instanceId = instance.GetInstanceID(),
                    message = $"Prefab instantiated: {instance.name}"
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = $"Error instantiating prefab: {ex.Message}" });
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