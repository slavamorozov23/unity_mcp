using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace SceneAPI.Modules
{
    public static class ObjectPickerModule
    {
        public static string Execute(HttpListenerContext context)
        {
            try
            {
                string objectPath = context.Request.QueryString["path"] ?? "";
                string componentName = context.Request.QueryString["component"] ?? "";
                string parameterName = context.Request.QueryString["parameter"] ?? "";

                var result = new List<object>();

                // Получаем все объекты сцены
                GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();

                // Если указан путь к объекту, начинаем поиск от него
                GameObject startObject = null;
                if (!string.IsNullOrEmpty(objectPath))
                {
                    startObject = GameObjectUtilities.FindGameObjectByPath(objectPath);
                }

                // Фильтруем объекты по критериям
                var filteredObjects = allObjects.AsEnumerable();

                if (startObject != null)
                {
                    // Ищем дочерние объекты
                    filteredObjects = GetChildrenRecursive(startObject);
                }

                if (!string.IsNullOrEmpty(componentName))
                {
                    // Фильтруем по наличию компонента
                    filteredObjects = filteredObjects.Where(obj =>
                        obj.GetComponent(componentName) != null);
                }

                // Берем первые 10 объектов
                var selectedObjects = filteredObjects.Take(10);

                foreach (var obj in selectedObjects)
                {
                    var objInfo = new
                    {
                        name = obj.name,
                        path = GameObjectUtilities.GetGameObjectPath(obj),
                        type = "GameObject",
                        components = obj.GetComponents<Component>()
                            .Where(c => c != null)
                            .Select(c => c.GetType().Name)
                            .ToArray()
                    };

                    result.Add(objInfo);
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    count = result.Count,
                    objects = result
                }, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = $"Error getting object picker options: {ex.Message}"
                });
            }
        }

        private static IEnumerable<GameObject> GetChildrenRecursive(GameObject parent)
        {
            var children = new List<GameObject> { parent };

            for (int i = 0; i < parent.transform.childCount; i++)
            {
                var child = parent.transform.GetChild(i).gameObject;
                children.AddRange(GetChildrenRecursive(child));
            }

            return children;
        }
    }
}