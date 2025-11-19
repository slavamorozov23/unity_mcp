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
                Scene activeScene = SceneManager.GetActiveScene();
                if (!activeScene.IsValid())
                {
                    return JsonConvert.SerializeObject(new { error = "No active scene found" });
                }

                object[] rootObjects = activeScene.GetRootGameObjects()
                    .Select(static go => GetGameObjectData(go))
                    .ToArray();

                var sceneData = new
                {
                    sceneName = activeScene.name,
                    scenePath = activeScene.path,
                    rootObjects,
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
            object[] children = new object[go.transform.childCount];
            for (int i = 0; i < go.transform.childCount; i++)
            {
                children[i] = GetGameObjectData(go.transform.GetChild(i).gameObject);
            }

            return new
            {
                go.name,
                path = GetGameObjectPath(go),
                active = go.activeInHierarchy,
                components = go.GetComponents<Component>()
                    .Where(static c => c != null)
                    .Select(static c => c.GetType().Name)
                    .ToArray(),
                children
            };
        }

        private static string GetGameObjectPath(GameObject go)
        {
            return go.transform.parent == null ? go.name : GetGameObjectPath(go.transform.parent.gameObject) + "/" + go.name;
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