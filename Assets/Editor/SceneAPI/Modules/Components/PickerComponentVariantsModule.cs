using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace SceneAPI.Modules
{
    public static class PickerComponentVariantsModule
    {
        public static string Execute(HttpListenerContext context)
        {
            try
            {
                // Возвращаем полный список всех доступных компонентов
                var result = new List<object>();
                
                // Получаем все доступные типы компонентов без фильтрации
                var componentTypes = GetAvailableComponentTypes();
                
                foreach (var type in componentTypes)
                {
                    var componentInfo = new
                    {
                        name = type.Name,
                        fullName = type.FullName,
                        assembly = type.Assembly.GetName().Name,
                        isUnityComponent = type.Namespace?.StartsWith("UnityEngine") == true,
                        description = GetComponentDescription(type)
                    };
                    
                    result.Add(componentInfo);
                }
                
                return JsonConvert.SerializeObject(new 
                { 
                    success = true,
                    count = result.Count,
                    components = result 
                }, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new 
                { 
                    success = false, 
                    error = $"Error getting component variants: {ex.Message}" 
                });
            }
        }
        
        private static IEnumerable<Type> GetAvailableComponentTypes()
        {
            var componentTypes = new List<Type>();
            
            // Получаем типы из UnityEngine
            var unityTypes = typeof(Component).Assembly.GetTypes()
                .Where(type => typeof(Component).IsAssignableFrom(type) && 
                              !type.IsAbstract && 
                              type.IsPublic);
            componentTypes.AddRange(unityTypes);
            
            // Получаем типы из Assembly-CSharp (пользовательские скрипты)
            try
            {
                var assemblyCSharp = System.Reflection.Assembly.Load("Assembly-CSharp");
                var customTypes = assemblyCSharp.GetTypes()
                    .Where(type => typeof(Component).IsAssignableFrom(type) && 
                                  !type.IsAbstract && 
                                  type.IsPublic);
                componentTypes.AddRange(customTypes);
            }
            catch
            {
                // Assembly-CSharp может не существовать
            }
            
            return componentTypes.Distinct();
        }
        
        private static string GetComponentDescription(Type type)
        {
            // Простое описание на основе имени типа
            if (type.Name.Contains("Renderer"))
                return "Rendering component";
            if (type.Name.Contains("Collider"))
                return "Physics collision component";
            if (type.Name.Contains("Rigidbody"))
                return "Physics body component";
            if (type.Name.Contains("Light"))
                return "Lighting component";
            if (type.Name.Contains("Camera"))
                return "Camera component";
            if (type.Name.Contains("Audio"))
                return "Audio component";
            if (type.Name.Contains("UI") || type.Name.Contains("Canvas"))
                return "UI component";
            
            return "Component";
        }
    }
}