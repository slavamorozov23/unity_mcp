using System;
using System.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace SceneAPI.Modules
{
    public static class DeleteObjectModule
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
                    return JsonConvert.SerializeObject(new 
                    { 
                        success = false, 
                        error = "Object path is required" 
                    });
                }

                GameObject obj = GameObjectUtilities.FindGameObjectByPath(objectPath);
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                    return JsonConvert.SerializeObject(new 
                    { 
                        success = true, 
                        message = $"Object deleted: {objectPath}" 
                    });
                }

                return JsonConvert.SerializeObject(new 
                { 
                    success = false, 
                    error = "Object not found" 
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new 
                { 
                    success = false, 
                    error = $"Error deleting object: {ex.Message}" 
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