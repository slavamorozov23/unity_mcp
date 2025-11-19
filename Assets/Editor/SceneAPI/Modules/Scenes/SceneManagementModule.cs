using System;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SceneAPI.Modules
{
    public static class SceneManagementModule
    {
        public static string OpenScene(HttpListenerContext context)
        {
            try
            {
                string requestBody = GetRequestBody(context);
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);
                
                string scenePath = data?.scenePath;
                
                if (string.IsNullOrEmpty(scenePath))
                {
                    return JsonConvert.SerializeObject(new 
                    { 
                        success = false, 
                        error = "Scene path is required" 
                    });
                }

                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(scenePath);
                    return JsonConvert.SerializeObject(new 
                    { 
                        success = true, 
                        message = $"Scene opened: {scenePath}" 
                    });
                }

                return JsonConvert.SerializeObject(new 
                { 
                    success = false, 
                    error = "Failed to save current scene or user cancelled" 
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new 
                { 
                    success = false, 
                    error = $"Error opening scene: {ex.Message}" 
                });
            }
        }

        public static string GetBuildScenes()
        {
            try
            {
                var buildScenes = EditorBuildSettings.scenes
                    .Select(scene => new
                    {
                        path = scene.path,
                        enabled = scene.enabled,
                        guid = scene.guid.ToString()
                    })
                    .ToArray();

                return JsonConvert.SerializeObject(new 
                { 
                    scenes = buildScenes,
                    totalCount = buildScenes.Length
                }, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new 
                { 
                    error = $"Error getting build scenes: {ex.Message}" 
                });
            }
        }

        public static string AddSceneToBuild(HttpListenerContext context)
        {
            try
            {
                string requestBody = GetRequestBody(context);
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);
                
                string scenePath = data?.scenePath;
                
                if (string.IsNullOrEmpty(scenePath))
                {
                    return JsonConvert.SerializeObject(new 
                    { 
                        success = false, 
                        error = "Scene path is required" 
                    });
                }

                var scenes = EditorBuildSettings.scenes.ToList();
                if (!scenes.Any(s => s.path == scenePath))
                {
                    scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                    EditorBuildSettings.scenes = scenes.ToArray();
                    
                    return JsonConvert.SerializeObject(new 
                    { 
                        success = true, 
                        message = $"Scene added to build: {scenePath}" 
                    });
                }

                return JsonConvert.SerializeObject(new 
                { 
                    success = false, 
                    error = "Scene already in build settings" 
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new 
                { 
                    success = false, 
                    error = $"Error adding scene to build: {ex.Message}" 
                });
            }
        }

        public static string RemoveSceneFromBuild(HttpListenerContext context)
        {
            try
            {
                string requestBody = GetRequestBody(context);
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);
                
                string scenePath = data?.scenePath;
                
                if (string.IsNullOrEmpty(scenePath))
                {
                    return JsonConvert.SerializeObject(new 
                    { 
                        success = false, 
                        error = "Scene path is required" 
                    });
                }

                var scenes = EditorBuildSettings.scenes.ToList();
                var sceneToRemove = scenes.FirstOrDefault(s => s.path == scenePath);
                
                if (sceneToRemove != null)
                {
                    scenes.Remove(sceneToRemove);
                    EditorBuildSettings.scenes = scenes.ToArray();
                    
                    return JsonConvert.SerializeObject(new 
                    { 
                        success = true, 
                        message = $"Scene removed from build: {scenePath}" 
                    });
                }

                return JsonConvert.SerializeObject(new 
                { 
                    success = false, 
                    error = "Scene not found in build settings" 
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new 
                { 
                    success = false, 
                    error = $"Error removing scene from build: {ex.Message}" 
                });
            }
        }

        private static string GetRequestBody(HttpListenerContext context)
        {
            using (var reader = new StreamReader(context.Request.InputStream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}