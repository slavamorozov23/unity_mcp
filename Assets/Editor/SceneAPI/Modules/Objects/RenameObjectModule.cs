using System;
using System.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace SceneAPI.Modules
{
    public static class RenameObjectModule
    {
        public static string Execute(HttpListenerContext context)
        {
            try
            {
                string requestBody = GetRequestBody(context);
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);

                string objectPath = data?.path;
                string newName = data?.newName;

                if (string.IsNullOrEmpty(objectPath) || string.IsNullOrEmpty(newName))
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "Object path and new name are required"
                    });
                }

                GameObject obj = GameObjectUtilities.FindGameObjectByPath(objectPath);
                if (obj == null)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "Object not found"
                    });
                }

                string oldName = obj.name;
                obj.name = newName;

                // Вычисляем новый путь
                string newPath = objectPath;
                if (objectPath.Contains("/"))
                {
                    string parentPath = objectPath.Substring(0, objectPath.LastIndexOf('/'));
                    newPath = $"{parentPath}/{newName}";
                }
                else
                {
                    newPath = newName;
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    oldPath = objectPath,
                    newPath = newPath,
                    oldName = oldName,
                    newName = newName,
                    message = $"Object renamed from '{oldName}' to '{newName}'"
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = $"Error renaming object: {ex.Message}"
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