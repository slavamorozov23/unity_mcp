using System;
using System.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace SceneAPI.Modules
{
    public static class ModifyComponentModule
    {
        public static string Execute(HttpListenerContext context)
        {
            try
            {
                string requestBody = GetRequestBody(context);
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);
                
                string objectPath = data?.path;
                string componentType = data?.componentType;
                var properties = data?.properties;
                
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

                Component component = obj.GetComponent(componentType);
                if (component == null)
                {
                    return JsonConvert.SerializeObject(new 
                    { 
                        success = false, 
                        error = $"Component {componentType} not found on object" 
                    });
                }

                ComponentUtilities.ModifyComponentProperties(component, properties);
                bool success = true;
                
                return JsonConvert.SerializeObject(new 
                { 
                    success = success, 
                    message = success ? $"Component {componentType} modified on {objectPath}" : "Failed to modify component"
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new 
                { 
                    success = false, 
                    error = $"Error modifying component: {ex.Message}" 
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