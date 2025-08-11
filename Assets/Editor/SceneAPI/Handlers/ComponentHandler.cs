using System;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace SceneAPI.Handlers
{
    public class ComponentHandler
    {
        public string GetObjectComponents(HttpListenerContext context)
        {
            string objectPath = context.Request.QueryString["path"];

            GameObject obj = GameObjectUtilities.FindGameObjectByPath(objectPath);
            if (obj == null)
            {
                return JsonConvert.SerializeObject(new { error = "Object not found" });
            }

            var components = obj.GetComponents<Component>().Where(c => c != null).Select(comp => new
            {
                name = comp.GetType().Name,
                type = comp.GetType().FullName,
                properties = ComponentUtilities.GetComponentProperties(comp)
            }).ToArray();

            return JsonConvert.SerializeObject(new { path = objectPath, components = components }, Formatting.Indented);
        }

        public string AddComponent(HttpListenerContext context)
        {
            var data = JsonConvert.DeserializeObject<dynamic>(GetRequestBody(context));
            string objectPath = data.path;
            string componentType = data.componentType;

            GameObject obj = GameObjectUtilities.FindGameObjectByPath(objectPath);
            if (obj == null)
            {
                return JsonConvert.SerializeObject(new { success = false, message = "Object not found" });
            }

            Type type = Type.GetType($"UnityEngine.{componentType}, UnityEngine") ??
                       Type.GetType($"{componentType}, Assembly-CSharp");

            if (type == null)
            {
                return JsonConvert.SerializeObject(new { success = false, message = "Component type not found" });
            }

            if (obj.GetComponent(type) != null)
            {
                return JsonConvert.SerializeObject(new { success = false, message = $"Component {componentType} already exists on object" });
            }

            try
            {
                Component comp = obj.AddComponent(type);
                return JsonConvert.SerializeObject(new { success = true, message = $"Component added: {componentType}" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, message = $"Failed to add component: {ex.Message}" });
            }
        }

        public string ModifyComponent(HttpListenerContext context)
        {
            var data = JsonConvert.DeserializeObject<dynamic>(GetRequestBody(context));
            string objectPath = data.path;
            string componentType = data.componentType;
            var properties = data.properties;

            GameObject obj = GameObjectUtilities.FindGameObjectByPath(objectPath);
            if (obj == null)
            {
                return JsonConvert.SerializeObject(new { success = false, message = "Object not found" });
            }

            Component comp = obj.GetComponent(componentType);
            if (comp == null)
            {
                return JsonConvert.SerializeObject(new { success = false, message = "Component not found" });
            }

            try
            {
                ComponentUtilities.ModifyComponentProperties(comp, properties);
                return JsonConvert.SerializeObject(new { success = true, message = "Component modified" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, message = $"Failed to modify component: {ex.Message}" });
            }
        }

        public string RemoveComponent(HttpListenerContext context)
        {
            var data = JsonConvert.DeserializeObject<dynamic>(GetRequestBody(context));
            string objectPath = data.path;
            string componentType = data.componentType;

            GameObject obj = GameObjectUtilities.FindGameObjectByPath(objectPath);
            if (obj == null)
            {
                return JsonConvert.SerializeObject(new { success = false, message = "Object not found" });
            }

            Component comp = obj.GetComponent(componentType);
            if (comp == null)
            {
                return JsonConvert.SerializeObject(new { success = false, message = "Component not found" });
            }

            UnityEngine.Object.DestroyImmediate(comp);
            return JsonConvert.SerializeObject(new { success = true, message = $"Component removed: {componentType}" });
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