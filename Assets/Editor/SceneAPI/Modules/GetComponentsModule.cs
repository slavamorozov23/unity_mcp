using System;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace SceneAPI.Modules
{
    public static class GetComponentsModule
    {
        public static string Execute(HttpListenerContext context)
        {
            try
            {
                string objectPath = context.Request.QueryString["path"];
                
                if (string.IsNullOrEmpty(objectPath))
                {
                    return JsonConvert.SerializeObject(new { error = "Object path is required" });
                }

                GameObject obj = GameObjectUtilities.FindGameObjectByPath(objectPath);
                if (obj == null)
                {
                    return JsonConvert.SerializeObject(new { error = "Object not found" });
                }

                var components = obj.GetComponents<Component>()
                    .Where(c => c != null)
                    .ToDictionary(
                        comp => comp.GetType().Name,
                        comp => ComponentUtilities.GetComponentProperties(comp)
                    );

                return JsonConvert.SerializeObject(new 
                { 
                    path = objectPath, 
                    components = components 
                }, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { error = $"Error getting components: {ex.Message}" });
            }
        }
    }
}