using System;
using System.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace SceneAPI.Modules
{
    public static class MoveObjectModule
    {
        public static string Execute(HttpListenerContext context)
        {
            try
            {
                string requestBody = GetRequestBody(context);
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);
                
                string sourcePath = data?.sourcePath;
                string targetPath = data?.targetPath;

                if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(targetPath))
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "sourcePath and targetPath are required" });
                }

                GameObject obj = GameObjectUtilities.FindGameObjectByPath(sourcePath);
                if (obj == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "Source object not found" });
                }

                // Разбираем целевой путь на родителя и имя
                string targetParentPath = "";
                string newName = targetPath;
                
                if (targetPath.Contains("/"))
                {
                    int lastSlash = targetPath.LastIndexOf('/');
                    targetParentPath = targetPath.Substring(0, lastSlash);
                    newName = targetPath.Substring(lastSlash + 1);
                }

                // Устанавливаем нового родителя
                if (!string.IsNullOrEmpty(targetParentPath))
                {
                    GameObject newParent = GameObjectUtilities.FindGameObjectByPath(targetParentPath);
                    if (newParent == null)
                    {
                        return JsonConvert.SerializeObject(new { success = false, error = "Target parent not found" });
                    }
                    obj.transform.SetParent(newParent.transform);
                }
                else
                {
                    // Перемещаем в корень сцены
                    obj.transform.SetParent(null);
                }

                // Устанавливаем новое имя
                obj.name = newName;

                string newPath = targetPath;
                return JsonConvert.SerializeObject(new { success = true, path = newPath, message = "Object moved" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = $"Error moving object: {ex.Message}" });
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