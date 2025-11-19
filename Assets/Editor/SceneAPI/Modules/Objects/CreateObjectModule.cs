using System;
using System.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace SceneAPI.Modules
{
    public static class CreateObjectModule
    {
        public static string Execute(HttpListenerContext context)
        {
            try
            {
                string requestBody = GetRequestBody(context);
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);
                
                string objectName = data?.name ?? "GameObject";
                string parentPath = data?.parentPath ?? "";

                GameObject newObj = new GameObject(objectName);

                if (!string.IsNullOrEmpty(parentPath))
                {
                    GameObject parent = GameObjectUtilities.FindGameObjectByPath(parentPath);
                    if (parent != null)
                    {
                        newObj.transform.SetParent(parent.transform);
                    }
                }

                string fullPath = string.IsNullOrEmpty(parentPath) ? objectName : $"{parentPath}/{objectName}";

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    path = fullPath,
                    instanceId = newObj.GetInstanceID(),
                    message = $"Object created: {objectName}"
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new 
                { 
                    success = false, 
                    error = $"Error creating object: {ex.Message}" 
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