using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAgentBridge.Editor
{
    internal static class SceneService
    {
        public static SceneObjectData[] GetTree()
        {
            var result = new List<SceneObjectData>();
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                AddHierarchy(prefabStage.prefabContentsRoot, 0, result);
                return result.ToArray();
            }
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                    continue;

                foreach (var root in scene.GetRootGameObjects())
                    AddHierarchy(root, 0, result);
            }

            return result.ToArray();
        }

        public static SceneObjectData GetObjectInfo(string path)
        {
            var gameObject = ScenePath.ResolveObject(path);
            EditorPresentationService.ShowSceneObject(gameObject);
            return Describe(gameObject, Depth(gameObject.transform));
        }

        public static SceneObjectData GetObjectInfo(BridgeRequest request)
        {
            var gameObject = ScenePath.ResolveObject(request.path);
            if (string.IsNullOrWhiteSpace(request.componentType))
                EditorPresentationService.ShowSceneObject(gameObject);
            else
                EditorPresentationService.ShowComponent(ComponentService.ResolveAttachedComponent(
                    gameObject, request.componentType, request.componentIndex));
            return Describe(gameObject, Depth(gameObject.transform));
        }

        public static SceneAssetData[] ListSceneAssets()
        {
            if (AssetDatabase.IsValidFolder("Assets/Scenes"))
                EditorPresentationService.ShowAssetFolder("Assets/Scenes");
            return AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new SceneAssetData
                {
                    name = Path.GetFileNameWithoutExtension(path),
                    assetPath = path
                })
                .ToArray();
        }

        public static SceneAssetData OpenScene(string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath))
                throw new ArgumentException("Scene name or path is required.", "nameOrPath");
            var matches = ListSceneAssets()
                .Where(item => string.Equals(item.assetPath, nameOrPath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.name, nameOrPath, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException(matches.Length == 0 ? "Scene was not found in Assets/Scenes: " + nameOrPath : "Scene name is ambiguous: " + nameOrPath);
            EditorPresentationService.ShowAssetPath(matches[0].assetPath);
            ScenePersistenceService.SaveBeforeTransition();
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                StageUtility.GoToMainStage();
            EditorSceneManager.OpenScene(matches[0].assetPath, OpenSceneMode.Single);
            return matches[0];
        }

        public static SceneObjectData Describe(GameObject gameObject, int depth)
        {
            return Describe(gameObject, depth, true);
        }

        private static SceneObjectData Describe(GameObject gameObject, int depth, bool includeValues)
        {
            var componentData = DescribeComponents(gameObject, includeValues);
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
            var prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
            if (prefabStage != null && (gameObject == prefabStage.prefabContentsRoot || gameObject.transform.IsChildOf(prefabStage.prefabContentsRoot.transform)))
                prefabAssetPath = prefabStage.assetPath;

            return new SceneObjectData
            {
                path = ScenePath.For(gameObject),
                parentPath = ScenePath.ParentOf(gameObject),
                name = gameObject.name,
                scene = gameObject.scene.name,
                depth = depth,
                activeSelf = gameObject.activeSelf,
                activeInHierarchy = gameObject.activeInHierarchy,
                visual = HasRenderableGeometry(gameObject),
                tag = gameObject.tag,
                layer = gameObject.layer,
                worldPosition = gameObject.transform.position,
                prefabAssetPath = prefabAssetPath,
                prefabInstanceRootPath = instanceRoot == null ? string.Empty : ScenePath.For(instanceRoot),
                components = componentData
            };
        }

        private static bool HasRenderableGeometry(GameObject gameObject)
        {
            if (gameObject.GetComponentsInChildren<Renderer>(false).Any(renderer => renderer.enabled))
                return true;
            if (gameObject.GetComponentsInChildren<CanvasRenderer>(false).Any(renderer => renderer.gameObject.activeInHierarchy))
                return true;
            return gameObject.GetComponentsInChildren<Terrain>(false).Any(terrain => terrain.enabled);
        }

        internal static ComponentData[] DescribeComponents(GameObject gameObject, bool includeValues)
        {
            var components = gameObject.GetComponents<Component>();
            var componentData = new ComponentData[components.Length];
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    componentData[i] = new ComponentData
                    {
                        type = "Missing Script",
                        assemblyQualifiedType = string.Empty,
                        json = string.Empty,
                        references = Array.Empty<SerializedPropertyData>(),
                        warnings = Array.Empty<InspectorWarningData>(),
                        actions = Array.Empty<InspectorActionData>()
                    };
                    continue;
                }

                var type = component.GetType();
                componentData[i] = new ComponentData
                {
                    type = type.FullName,
                    assemblyQualifiedType = includeValues ? type.AssemblyQualifiedName : string.Empty,
                    json = includeValues ? EditorJsonUtility.ToJson(component, false) : string.Empty,
                    references = includeValues ? ComponentService.DescribeReferences(component) : Array.Empty<SerializedPropertyData>(),
                    warnings = includeValues ? InspectorDiagnosticService.Warnings(component) : Array.Empty<InspectorWarningData>(),
                    actions = includeValues ? InspectorDiagnosticService.Actions(component) : Array.Empty<InspectorActionData>()
                };
            }

            return componentData;
        }

        private static void AddHierarchy(GameObject gameObject, int depth, ICollection<SceneObjectData> result)
        {
            result.Add(Describe(gameObject, depth, false));
            for (var i = 0; i < gameObject.transform.childCount; i++)
                AddHierarchy(gameObject.transform.GetChild(i).gameObject, depth + 1, result);
        }

        private static int Depth(Transform transform)
        {
            var depth = 0;
            while (transform.parent != null)
            {
                depth++;
                transform = transform.parent;
            }

            return depth;
        }
    }
}
