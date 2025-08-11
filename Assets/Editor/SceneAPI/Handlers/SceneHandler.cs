using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SceneAPI.Handlers
{
    public class SceneHandler
    {
        public string GetSceneHierarchy()
        {
            try
            {
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (!activeScene.IsValid())
                {
                    return JsonConvert.SerializeObject(new { error = "No active scene found" });
                }

                var sceneData = new
                {
                    sceneName = activeScene.name,
                    scenePath = activeScene.path,
                    rootObjects = GetRootObjectsData()
                };

                return JsonConvert.SerializeObject(sceneData, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { error = $"Error getting scene hierarchy: {ex.Message}" });
            }
        }

        public string OpenScene(HttpListenerContext context)
        {
            var data = JsonConvert.DeserializeObject<dynamic>(GetRequestBody(context));
            string scenePath = data.scenePath;

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(scenePath);
                return JsonConvert.SerializeObject(new { success = true, message = $"Scene opened: {scenePath}" });
            }

            return JsonConvert.SerializeObject(new { success = false, message = "Failed to open scene" });
        }

        public string GetBuildScenes()
        {
            var buildScenes = EditorBuildSettings.scenes.Select((scene, index) => new
            {
                index = index,
                path = scene.path,
                enabled = scene.enabled,
                guid = scene.guid.ToString()
            }).ToArray();

            return JsonConvert.SerializeObject(new { scenes = buildScenes }, Formatting.Indented);
        }

        public string AddSceneToBuild(HttpListenerContext context)
        {
            var data = JsonConvert.DeserializeObject<dynamic>(GetRequestBody(context));
            string scenePath = data.scenePath;

            if (!File.Exists(scenePath))
            {
                return JsonConvert.SerializeObject(new { success = false, message = "Scene file not found" });
            }

            var scenes = EditorBuildSettings.scenes.ToList();

            if (scenes.Any(s => s.path == scenePath))
            {
                return JsonConvert.SerializeObject(new { success = false, message = "Scene already in build settings" });
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            return JsonConvert.SerializeObject(new { success = true, message = $"Scene added to build: {scenePath}" });
        }

        public string RemoveSceneFromBuild(HttpListenerContext context)
        {
            var data = JsonConvert.DeserializeObject<dynamic>(GetRequestBody(context));
            string scenePath = data.scenePath;

            var scenes = EditorBuildSettings.scenes.ToList();
            var sceneToRemove = scenes.FirstOrDefault(s => s.path == scenePath);

            if (sceneToRemove.path == null)
            {
                return JsonConvert.SerializeObject(new { success = false, message = "Scene not found in build settings" });
            }

            scenes.Remove(sceneToRemove);
            EditorBuildSettings.scenes = scenes.ToArray();

            return JsonConvert.SerializeObject(new { success = true, message = $"Scene removed from build: {scenePath}" });
        }

        private List<object> GetRootObjectsData()
        {
            var rootObjects = new List<object>();

            try
            {
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (!activeScene.IsValid())
                {
                    return rootObjects;
                }

                GameObject[] sceneRootObjects = null;

                try
                {
                    sceneRootObjects = activeScene.GetRootGameObjects();
                }
                catch
                {
                    return rootObjects;
                }

                if (sceneRootObjects == null || sceneRootObjects.Length == 0)
                {
                    return rootObjects;
                }

                foreach (GameObject rootGO in sceneRootObjects)
                {
                    if (rootGO != null)
                    {
                        try
                        {
                            rootObjects.Add(GameObjectUtilities.GetGameObjectData(rootGO, ""));
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"Error processing GameObject {rootGO.name}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error getting root objects: {ex.Message}");
            }

            return rootObjects;
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