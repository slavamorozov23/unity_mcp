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
            string requestBody = context != null ? GetRequestBody(context) : "{}";
            return Execute(requestBody);
        }

        public static string Execute(string requestBody)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody ?? "{}");
                
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

                // Прямое получение типа по точному имени или полному имени
                Type type = Type.GetType(componentType);
                
                if (type == null)
                {
                    // Попробуем найти тип в загруженных сборках по полному имени
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = assembly.GetType(componentType);
                        if (type != null) break;
                    }
                }

                if (type == null)
                {
                    // fallback: поиск по короткому имени среди всех типов-компонентов
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        Type[] types;
                        try
                        {
                            types = assembly.GetTypes();
                        }
                        catch
                        {
                            continue;
                        }

                        foreach (var t in types)
                        {
                            if (typeof(Component).IsAssignableFrom(t) && !t.IsAbstract && t.Name == componentType)
                            {
                                type = t;
                                break;
                            }
                        }

                        if (type != null) break;
                    }
                }

                if (type == null)
                {
                    return JsonConvert.SerializeObject(new 
                    { 
                        success = false, 
                        error = $"Component type '{componentType}' not found in any loaded assembly" 
                    });
                }

                // Проверяем, есть ли уже такой компонент
                Component existingComponent = obj.GetComponent(type);
                bool hasExistingComponent = existingComponent != null;
                
                // Подсчитываем общее количество компонентов
                int componentCount = obj.GetComponents<Component>().Length;
                
                obj.AddComponent(type);
                
                var response = new 
                { 
                    success = true, 
                    message = $"Component {componentType} added to {objectPath}",
                    warning = componentCount > 1 ? $"Object now has {componentCount + 1} components" : null,
                    duplicateWarning = hasExistingComponent ? $"Component {componentType} was already present on this object" : null
                };
                
                return JsonConvert.SerializeObject(response);
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