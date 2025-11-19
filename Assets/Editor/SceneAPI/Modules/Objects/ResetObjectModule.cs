using System;
using System.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace SceneAPI.Modules
{
    public static class ResetObjectModule
    {
        public static string Execute(HttpListenerContext context)
        {
            try
            {
                string requestBody = GetRequestBody(context);
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);

                string objectPath = data?.path;
                if (string.IsNullOrEmpty(objectPath))
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "Object path is required" });
                }

                GameObject obj = GameObjectUtilities.FindGameObjectByPath(objectPath);
                if (obj == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "Object not found" });
                }

                // Сбрасываем базовые параметры Transform в дефолт
                if (obj.transform != null)
                {
                    obj.transform.localPosition = Vector3.zero;
                    obj.transform.localRotation = Quaternion.identity;
                    obj.transform.localScale = Vector3.one;
                }

                // Можно расширить: сбрасывать свойства стандартных компонентов (Renderer, Collider, etc.)
                return JsonConvert.SerializeObject(new { success = true, message = "Object reset to defaults" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = $"Error resetting object: {ex.Message}" });
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