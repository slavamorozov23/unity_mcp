using System;
using System.Net;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace SceneAPI.Modules
{
    public static class CreateObjectFromPrefabModule
    {
        public static string Execute(HttpListenerContext context)
        {
            try
            {
                string requestBody = GetRequestBody(context);
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);
                
                string prefabName = data?.prefabName;
                string objectName = data?.objectName ?? "";
                string parentPath = data?.parentPath ?? "";
                
                if (string.IsNullOrEmpty(prefabName))
                {
                    return JsonConvert.SerializeObject(new 
                    { 
                        success = false, 
                        error = "prefabName is required" 
                    });
                }

                // Ищем префаб по имени в стандартной папке
                string prefabPath = $"Assets/Prefabs/{prefabName}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    // Попробуем найти в других местах
                    string[] guids = AssetDatabase.FindAssets($"{prefabName} t:Prefab");
                    if (guids.Length > 0)
                    {
                        prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    }
                }
                
                if (prefab == null)
                {
                    return JsonConvert.SerializeObject(new 
                    { 
                        success = false, 
                        error = $"Prefab '{prefabName}' not found" 
                    });
                }

                // Создаем экземпляр префаба
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                {
                    return JsonConvert.SerializeObject(new 
                    { 
                        success = false, 
                        error = "Failed to instantiate prefab" 
                    });
                }

                // Задаем имя объекта, если указано
                if (!string.IsNullOrEmpty(objectName))
                {
                    instance.name = objectName;
                }

                // Устанавливаем родителя, если указан
                if (!string.IsNullOrEmpty(parentPath))
                {
                    GameObject parent = GameObjectUtilities.FindGameObjectByPath(parentPath);
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
                    message = $"Object created from prefab: {instance.name}"
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new 
                { 
                    success = false, 
                    error = $"Error creating object from prefab: {ex.Message}" 
                });
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