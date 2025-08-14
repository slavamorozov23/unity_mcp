using System;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneAPI.Modules
{
    public static class GetHierarchyModule
    {
        public static string Execute()
        {
            try
            {
                var activeScene = SceneManager.GetActiveScene();
                if (!activeScene.IsValid())
                {
                    return JsonConvert.SerializeObject(new { error = "No active scene found" });
                }

                var rootObjects = activeScene.GetRootGameObjects()
                    .Select(go => GetGameObjectData(go))
                    .ToArray();

                var sceneData = new
                {
                    sceneName = activeScene.name,
                    scenePath = activeScene.path,
                    rootObjects = rootObjects,
                    totalObjects = CountTotalObjects(rootObjects)
                };

                return JsonConvert.SerializeObject(sceneData, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { error = $"Error getting scene hierarchy: {ex.Message}" });
            }
        }

        private static object GetGameObjectData(GameObject go)
        {
            var children = new object[go.transform.childCount];
            for (int i = 0; i < go.transform.childCount; i++)
            {
                children[i] = GetGameObjectData(go.transform.GetChild(i).gameObject);
            }

            return new
            {
                name = go.name,
                path = GetGameObjectPath(go),
                active = go.activeInHierarchy,
                components = go.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .ToArray(),
                children = children
            };
        }

        private static string GetGameObjectPath(GameObject go)
        {
            if (go.transform.parent == null)
                return go.name;
            return GetGameObjectPath(go.transform.parent.gameObject) + "/" + go.name;
        }

        private static int CountTotalObjects(object[] rootObjects)
        {
            int count = rootObjects.Length;
            foreach (dynamic obj in rootObjects)
            {
                if (obj.children != null)
                {
                    count += CountTotalObjects(obj.children);
                }
            }
            return count;
        }
    }
}