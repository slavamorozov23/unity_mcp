using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAgentBridge.Editor
{
    internal static class ScenePath
    {
        public static string For(GameObject gameObject)
        {
            if (gameObject == null || !gameObject.scene.IsValid())
                throw new InvalidOperationException("Object does not belong to a loaded scene.");

            var segments = new Stack<string>();
            var current = gameObject.transform;
            while (current != null)
            {
                segments.Push(EncodeSegment(current.name, SameNameIndex(current)));
                current = current.parent;
            }

            var sceneIndex = SameNameSceneIndex(gameObject.scene);
            return "/" + EncodeSegment(gameObject.scene.name, sceneIndex) + "/" + string.Join("/", segments.ToArray());
        }

        public static string ParentOf(GameObject gameObject)
        {
            return gameObject.transform.parent == null
                ? "/" + EncodeSegment(gameObject.scene.name, SameNameSceneIndex(gameObject.scene))
                : For(gameObject.transform.parent.gameObject);
        }

        public static GameObject ResolveObject(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path[0] != '/')
                throw new ArgumentException("Scene path must start with '/'.", "path");

            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
                throw new ArgumentException("Scene object path must contain a scene and an object.", "path");

            string sceneName;
            int? sceneNameIndex;
            DecodeResolvableSegment(segments[0], out sceneName, out sceneNameIndex);
            var matchingScenes = ContextScenes().Where(item => item.isLoaded && item.name == sceneName).ToArray();
            if (matchingScenes.Length == 0 || (sceneNameIndex.HasValue && sceneNameIndex.Value >= matchingScenes.Length))
                throw new InvalidOperationException("Loaded scene was not found: " + sceneName);
            if (!sceneNameIndex.HasValue && matchingScenes.Length != 1)
                throw new InvalidOperationException("Loaded scene name is ambiguous; use its index: " + sceneName);
            var scene = matchingScenes[sceneNameIndex ?? 0];

            Transform current = null;
            for (var segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
            {
                string objectName;
                int? sameNameIndex;
                DecodeResolvableSegment(segments[segmentIndex], out objectName, out sameNameIndex);

                IEnumerable<Transform> children = current == null
                    ? scene.GetRootGameObjects().Select(item => item.transform)
                    : Enumerable.Range(0, current.childCount).Select(current.GetChild);

                var matchingChildren = children.Where(item => item.name == objectName).ToArray();
                if (!sameNameIndex.HasValue && matchingChildren.Length > 1)
                    throw new InvalidOperationException("Scene path segment is ambiguous; use its index: " + segments[segmentIndex]);
                current = matchingChildren.ElementAtOrDefault(sameNameIndex ?? 0);
                if (current == null)
                    throw new InvalidOperationException("Scene path was not found: " + path);
            }

            return current.gameObject;
        }

        public static Transform ResolveParent(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path[0] != '/')
                throw new ArgumentException("Destination path must start with '/'.", "path");

            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 1)
            {
                string sceneName;
                int sceneIndex;
                DecodeSegment(segments[0], out sceneName, out sceneIndex);
                ResolveScene(sceneName, sceneIndex);
                return null;
            }

            return ResolveObject(path).transform;
        }

        public static Scene ResolveDestinationScene(string path)
        {
            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                throw new ArgumentException("Destination path must contain a scene.", "path");

            string sceneName;
            int sceneIndex;
            DecodeSegment(segments[0], out sceneName, out sceneIndex);
            return ResolveScene(sceneName, sceneIndex);
        }

        public static string DestinationObjectName(string path, out int sameNameIndex)
        {
            if (string.IsNullOrWhiteSpace(path) || path[0] != '/')
                throw new ArgumentException("Destination path must start with '/'.", "path");

            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
                throw new ArgumentException("Destination path must contain a scene and an object.", "path");

            string name;
            DecodeSegment(segments[segments.Length - 1], out name, out sameNameIndex);
            return name;
        }

        public static string DestinationParentPath(string path)
        {
            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
                throw new ArgumentException("Destination path must contain a scene and an object.", "path");
            return "/" + string.Join("/", segments.Take(segments.Length - 1).ToArray());
        }

        private static int SameNameIndex(Transform transform)
        {
            if (transform.parent == null)
            {
                var roots = transform.gameObject.scene.GetRootGameObjects();
                var index = 0;
                foreach (var root in roots)
                {
                    if (root.name != transform.name)
                        continue;
                    if (root.transform == transform)
                        return index;
                    index++;
                }
            }
            else
            {
                var index = 0;
                for (var childIndex = 0; childIndex < transform.parent.childCount; childIndex++)
                {
                    var child = transform.parent.GetChild(childIndex);
                    if (child.name != transform.name)
                        continue;
                    if (child == transform)
                        return index;
                    index++;
                }
            }

            throw new InvalidOperationException("Object is not present in its hierarchy.");
        }

        private static int SameNameSceneIndex(Scene scene)
        {
            var index = 0;
            foreach (var loaded in ContextScenes())
            {
                if (loaded.name != scene.name)
                    continue;
                if (loaded == scene)
                    return index;
                index++;
            }

            throw new InvalidOperationException("Scene is not loaded.");
        }

        private static Scene ResolveScene(string name, int sameNameIndex)
        {
            var index = 0;
            foreach (var scene in ContextScenes())
            {
                if (!scene.isLoaded || scene.name != name)
                    continue;
                if (index == sameNameIndex)
                    return scene;
                index++;
            }

            throw new InvalidOperationException("Loaded scene was not found: " + name + "[" + sameNameIndex + "]");
        }

        internal static IEnumerable<Scene> ContextScenes()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                yield return prefabStage.scene;
                yield break;
            }
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded)
                    yield return scene;
            }
        }

        private static string EncodeSegment(string name, int sameNameIndex)
        {
            return Uri.EscapeDataString(name) + "[" + sameNameIndex + "]";
        }

        private static void DecodeSegment(string segment, out string name, out int sameNameIndex)
        {
            var bracket = segment.LastIndexOf('[');
            if (bracket <= 0 || !segment.EndsWith("]", StringComparison.Ordinal))
                throw new ArgumentException("Path segment must end with a same-name index: " + segment);

            name = Uri.UnescapeDataString(segment.Substring(0, bracket));
            if (!int.TryParse(segment.Substring(bracket + 1, segment.Length - bracket - 2), out sameNameIndex) || sameNameIndex < 0)
                throw new ArgumentException("Invalid same-name index in path segment: " + segment);
        }

        private static void DecodeResolvableSegment(string segment, out string name, out int? sameNameIndex)
        {
            var bracket = segment.LastIndexOf('[');
            if (bracket <= 0 || !segment.EndsWith("]", StringComparison.Ordinal))
            {
                name = Uri.UnescapeDataString(segment);
                sameNameIndex = null;
                return;
            }
            int parsed;
            name = Uri.UnescapeDataString(segment.Substring(0, bracket));
            if (!int.TryParse(segment.Substring(bracket + 1, segment.Length - bracket - 2), out parsed) || parsed < 0)
                throw new ArgumentException("Invalid same-name index in path segment: " + segment);
            sameNameIndex = parsed;
        }
    }
}
