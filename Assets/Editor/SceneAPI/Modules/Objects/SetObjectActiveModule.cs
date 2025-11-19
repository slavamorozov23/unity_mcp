using System;
using System.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace SceneAPI.Modules
{
    public static class SetObjectActiveModule
    {
        public static string Execute(HttpListenerContext context)
        {
            try
            {
                string requestBody = GetRequestBody(context);
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);

                string objectPath = data?.path;
                bool? active = data?.active;

                if (string.IsNullOrEmpty(objectPath) || !active.HasValue)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "Object path and active state (true/false) are required"
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

                bool previousState = obj.activeInHierarchy;
                obj.SetActive(active.Value);

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    path = objectPath,
                    previousState = previousState,
                    newState = active.Value,
                    message = $"Object '{objectPath}' {(active.Value ? "activated" : "deactivated")}"
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = $"Error setting object active state: {ex.Message}"
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