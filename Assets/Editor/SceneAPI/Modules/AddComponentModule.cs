using System;
using System.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace SceneAPI.Modules
{
    public static class AddComponentModule
    {
        public static string Execute(HttpListenerContext context)
        {
            try
            {
                string requestBody = GetRequestBody(context);
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);
                
                string objectPath = data?.path;
                string componentType = data?.componentType;
                
                if (string.IsNullOrEmpty(objectPath) || string.IsNullOrEmpty(componentType))
                {
                    return JsonConvert.SerializeObject(new 
                    { 
                        success = false, 
                        error = "Object path and component type are required" 
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

                Type type = Type.GetType($"UnityEngine.{componentType}, UnityEngine") ??
                           Type.GetType($"{componentType}, Assembly-CSharp");

                if (type == null)
                {
                    return JsonConvert.SerializeObject(new 
                    { 
                        success = false, 
                        error = "Component type not found" 
                    });
                }

                obj.AddComponent(type);
                
                return JsonConvert.SerializeObject(new 
                { 
                    success = true, 
                    message = $"Component {componentType} added to {objectPath}" 
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new 
                { 
                    success = false, 
                    error = $"Error adding component: {ex.Message}" 
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