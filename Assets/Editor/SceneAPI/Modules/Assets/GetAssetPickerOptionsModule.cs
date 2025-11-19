using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

namespace SceneAPI.Modules
{
    public class GetAssetPickerOptionsModule
    {
        [Serializable]
        public class PickerOptionsRequest
        {
            public string assetPath;
            public string propertyName;
            public int maxResults = 10;
        }

        [Serializable]
        public class PickerOption
        {
            public string name;
            public string path;
            public string type;
            public string guid;
            public bool isBuiltIn;
            public string description;
        }

        public string Execute(string requestBody)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<PickerOptionsRequest>(requestBody);

                if (string.IsNullOrEmpty(request.assetPath))
                {
                    var errorResponse = new
                    {
                        success = false,
                        error = "Asset path is required"
                    };
                    return JsonConvert.SerializeObject(errorResponse, Formatting.Indented);
                }

                if (string.IsNullOrEmpty(request.propertyName))
                {
                    var errorResponse = new
                    {
                        success = false,
                        error = "Property name is required"
                    };
                    return JsonConvert.SerializeObject(errorResponse, Formatting.Indented);
                }

                // Убеждаемся, что путь начинается с Assets/
                if (!request.assetPath.StartsWith("Assets/"))
                {
                    request.assetPath = "Assets/" + request.assetPath.TrimStart('/');
                }

                // Проверяем существование ассета
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(request.assetPath);
                if (!asset)
                {
                    var notFoundResponse = new
                    {
                        success = false,
                        error = $"Asset not found at path: {request.assetPath}"
                    };
                    return JsonConvert.SerializeObject(notFoundResponse, Formatting.Indented);
                }

                var options = GetPickerOptions(asset, request.propertyName, request.maxResults);

                var response = new
                {
                    success = true,
                    assetPath = request.assetPath,
                    propertyName = request.propertyName,
                    options = options,
                    totalFound = options.Count,
                    maxResults = request.maxResults
                };

                return JsonConvert.SerializeObject(response, Formatting.Indented);
            }
            catch (Exception ex)
            {
                var errorResponse = new
                {
                    success = false,
                    error = ex.Message
                };

                return JsonConvert.SerializeObject(errorResponse, Formatting.Indented);
            }
        }

        private List<PickerOption> GetPickerOptions(UnityEngine.Object asset, string propertyName, int maxResults)
        {
            var options = new List<PickerOption>();
            
            // Возвращаем полный список всех ассетов проекта
            options.AddRange(GetAllProjectAssets());
            
            // Добавляем встроенные ресурсы Unity
            options.AddRange(GetAllBuiltInResources());
            
            return options;
        }

        private List<PickerOption> GetAllProjectAssets()
        {
            var options = new List<PickerOption>();
            
            try
            {
                // Получаем все ассеты в проекте
                var guids = AssetDatabase.FindAssets("");
                
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var assetObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    
                    if (assetObj != null)
                    {
                        options.Add(new PickerOption
                        {
                            name = assetObj.name,
                            path = path,
                            type = assetObj.GetType().Name,
                            guid = guid,
                            isBuiltIn = false,
                            description = $"{assetObj.GetType().Name} asset"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error getting all project assets: {ex.Message}");
            }
            
            return options;
        }

        private List<PickerOption> FindAssetsByType(Type targetType, int maxResults)
        {
            var options = new List<PickerOption>();

            try
            {
                // Ищем ассеты через AssetDatabase
                var guids = AssetDatabase.FindAssets($"t:{targetType.Name}");

                foreach (var guid in guids.Take(maxResults))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var assetObj = AssetDatabase.LoadAssetAtPath(path, targetType);

                    if (assetObj != null)
                    {
                        options.Add(new PickerOption
                        {
                            name = assetObj.name,
                            path = path,
                            type = targetType.Name,
                            guid = guid,
                            isBuiltIn = false,
                            description = $"{targetType.Name} asset"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error finding assets of type {targetType.Name}: {ex.Message}");
            }

            return options;
        }

        private List<PickerOption> GetAllBuiltInResources()
        {
            var options = new List<PickerOption>();

            try
            {
                // Динамически получаем встроенные ресурсы Unity
                options.AddRange(GetBuiltInResourcesByType<Material>("Material"));
                options.AddRange(GetBuiltInResourcesByType<Texture2D>("Texture2D"));
                options.AddRange(GetBuiltInResourcesByType<Mesh>("Mesh"));
                options.AddRange(GetBuiltInResourcesByType<Shader>("Shader"));
                options.AddRange(GetBuiltInResourcesByType<Font>("Font"));
                options.AddRange(GetBuiltInResourcesByType<Sprite>("Sprite"));
                options.AddRange(GetBuiltInResourcesByType<AudioClip>("AudioClip"));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error getting built-in resources: {ex.Message}");
            }

            return options;
        }
        
        private List<PickerOption> GetBuiltInResourcesByType<T>(string typeName) where T : UnityEngine.Object
        {
            var options = new List<PickerOption>();
            
            try
            {
                // Получаем встроенные ресурсы через Resources.FindObjectsOfTypeAll
                var builtInResources = Resources.FindObjectsOfTypeAll<T>()
                    .Where(resource => AssetDatabase.GetAssetPath(resource) == "" || 
                                     AssetDatabase.GetAssetPath(resource).StartsWith("Resources/unity_builtin_extra") ||
                                     AssetDatabase.GetAssetPath(resource).StartsWith("Library/unity default resources"))
                    .ToList();
                
                foreach (var resource in builtInResources)
                {
                    if (resource != null && !string.IsNullOrEmpty(resource.name))
                    {
                        options.Add(new PickerOption
                        {
                            name = resource.name,
                            path = "Built-in",
                            type = typeName,
                            guid = "",
                            isBuiltIn = true,
                            description = $"Built-in {typeName}: {resource.name}"
                        });
                    }
                }
                
                // Добавляем известные встроенные ресурсы если они не найдены автоматически
                if (typeof(T) == typeof(Material))
                {
                    AddFallbackMaterials(options);
                }
                else if (typeof(T) == typeof(Mesh))
                {
                    AddFallbackMeshes(options);
                }
                else if (typeof(T) == typeof(Shader))
                {
                    AddFallbackShaders(options);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error getting built-in resources of type {typeName}: {ex.Message}");
            }
            
            return options;
        }
        
        private void AddFallbackMaterials(List<PickerOption> options)
        {
            var fallbackMaterials = new[] { "Default-Material", "Default-Diffuse", "Default-Skybox" };
            foreach (var materialName in fallbackMaterials)
            {
                if (!options.Any(o => o.name == materialName))
                {
                    options.Add(new PickerOption
                    {
                        name = materialName,
                        path = "Built-in",
                        type = "Material",
                        isBuiltIn = true,
                        description = $"Built-in Material: {materialName}"
                    });
                }
            }
        }
        
        private void AddFallbackMeshes(List<PickerOption> options)
        {
            var fallbackMeshes = new[] { "Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad" };
            foreach (var meshName in fallbackMeshes)
            {
                if (!options.Any(o => o.name == meshName))
                {
                    options.Add(new PickerOption
                    {
                        name = meshName,
                        path = "Built-in",
                        type = "Mesh",
                        isBuiltIn = true,
                        description = $"Built-in Mesh: {meshName}"
                    });
                }
            }
        }
        
        private void AddFallbackShaders(List<PickerOption> options)
        {
            var fallbackShaders = new[] { "Standard", "Unlit/Color", "Unlit/Texture", "Sprites/Default" };
            foreach (var shaderName in fallbackShaders)
            {
                if (!options.Any(o => o.name == shaderName))
                {
                    options.Add(new PickerOption
                    {
                        name = shaderName,
                        path = "Built-in",
                        type = "Shader",
                        isBuiltIn = true,
                        description = $"Built-in Shader: {shaderName}"
                    });
                }
            }
        }

        private List<PickerOption> GetGenericOptions(string propertyName, int maxResults)
        {
            var options = new List<PickerOption>();

            // Возвращаем общие варианты на основе имени свойства
            var lowerName = propertyName.ToLower();

            if (lowerName.Contains("color"))
            {
                options.AddRange(new[]
                {
                    new PickerOption { name = "Red", path = "Built-in", type = "Color", isBuiltIn = true, description = "Red Color" },
                    new PickerOption { name = "Green", path = "Built-in", type = "Color", isBuiltIn = true, description = "Green Color" },
                    new PickerOption { name = "Blue", path = "Built-in", type = "Color", isBuiltIn = true, description = "Blue Color" },
                    new PickerOption { name = "White", path = "Built-in", type = "Color", isBuiltIn = true, description = "White Color" },
                    new PickerOption { name = "Black", path = "Built-in", type = "Color", isBuiltIn = true, description = "Black Color" }
                });
            }
            else if (lowerName.Contains("layer"))
            {
                options.AddRange(new[]
                {
                    new PickerOption { name = "Default", path = "Built-in", type = "Layer", isBuiltIn = true, description = "Default Layer (0)" },
                    new PickerOption { name = "TransparentFX", path = "Built-in", type = "Layer", isBuiltIn = true, description = "TransparentFX Layer (1)" },
                    new PickerOption { name = "Ignore Raycast", path = "Built-in", type = "Layer", isBuiltIn = true, description = "Ignore Raycast Layer (2)" },
                    new PickerOption { name = "Water", path = "Built-in", type = "Layer", isBuiltIn = true, description = "Water Layer (4)" },
                    new PickerOption { name = "UI", path = "Built-in", type = "Layer", isBuiltIn = true, description = "UI Layer (5)" }
                });
            }
            else if (lowerName.Contains("tag"))
            {
                options.AddRange(new[]
                {
                    new PickerOption { name = "Untagged", path = "Built-in", type = "Tag", isBuiltIn = true, description = "Untagged" },
                    new PickerOption { name = "Respawn", path = "Built-in", type = "Tag", isBuiltIn = true, description = "Respawn Tag" },
                    new PickerOption { name = "Finish", path = "Built-in", type = "Tag", isBuiltIn = true, description = "Finish Tag" },
                    new PickerOption { name = "EditorOnly", path = "Built-in", type = "Tag", isBuiltIn = true, description = "EditorOnly Tag" },
                    new PickerOption { name = "MainCamera", path = "Built-in", type = "Tag", isBuiltIn = true, description = "MainCamera Tag" },
                    new PickerOption { name = "Player", path = "Built-in", type = "Tag", isBuiltIn = true, description = "Player Tag" },
                    new PickerOption { name = "GameController", path = "Built-in", type = "Tag", isBuiltIn = true, description = "GameController Tag" }
                });
            }

            return options.Take(maxResults).ToList();
        }
    }
}