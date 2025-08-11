using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SceneAPI
{
    public static class GameObjectUtilities
    {
        public static object GetGameObjectData(GameObject go, string parentPath)
        {
            if (go == null) return null;

            string currentPath = string.IsNullOrEmpty(parentPath) ? go.name : $"{parentPath}/{go.name}";
            var children = new List<object>();

            Transform transform = go.transform;
            if (transform != null)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child != null && child.gameObject != null)
                    {
                        children.Add(GetGameObjectData(child.gameObject, currentPath));
                    }
                }
            }

            return new
            {
                name = go.name,
                path = currentPath,
                instanceId = go.GetInstanceID(),
                active = go.activeInHierarchy,
                tag = go.tag,
                layer = go.layer,
                position = transform != null ? new { x = transform.position.x, y = transform.position.y, z = transform.position.z } : new { x = 0f, y = 0f, z = 0f },
                rotation = transform != null ? new { x = transform.rotation.x, y = transform.rotation.y, z = transform.rotation.z, w = transform.rotation.w } : new { x = 0f, y = 0f, z = 0f, w = 1f },
                scale = transform != null ? new { x = transform.localScale.x, y = transform.localScale.y, z = transform.localScale.z } : new { x = 1f, y = 1f, z = 1f },
                components = go.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name).ToArray(),
                children = children
            };
        }

        public static GameObject FindGameObjectByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string[] pathParts = path.Split('/');
            GameObject current = null;

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid()) return null;

            GameObject[] rootObjects = null;
            try
            {
                rootObjects = activeScene.GetRootGameObjects();
            }
            catch
            {
                return null;
            }

            if (rootObjects == null || rootObjects.Length == 0) return null;

            foreach (GameObject rootGO in rootObjects)
            {
                if (rootGO != null && rootGO.name == pathParts[0])
                {
                    current = rootGO;
                    break;
                }
            }

            if (current == null) return null;

            for (int i = 1; i < pathParts.Length; i++)
            {
                Transform child = current.transform.Find(pathParts[i]);
                if (child == null) return null;
                current = child.gameObject;
            }

            return current;
        }
    }
}