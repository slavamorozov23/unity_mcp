using System;
using System.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace SceneAPI.Handlers
{
    public class GameObjectHandler
    {
        public string CreateObject(HttpListenerContext context)
        {
            var data = JsonConvert.DeserializeObject<dynamic>(GetRequestBody(context));
            string objectName = data.name ?? "GameObject";
            string parentPath = data.parentPath ?? "";

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

        public string DeleteObject(HttpListenerContext context)
        {
            var data = JsonConvert.DeserializeObject<dynamic>(GetRequestBody(context));
            string objectPath = data.path;

            GameObject obj = GameObjectUtilities.FindGameObjectByPath(objectPath);
            if (obj != null)
            {
                UnityEngine.Object.DestroyImmediate(obj);
                return JsonConvert.SerializeObject(new { success = true, message = $"Object deleted: {objectPath}" });
            }

            return JsonConvert.SerializeObject(new { success = false, message = "Object not found" });
        }

        private string GetRequestBody(HttpListenerContext context)
        {
            using (var reader = new System.IO.StreamReader(context.Request.InputStream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}