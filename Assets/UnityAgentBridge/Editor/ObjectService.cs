using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityAgentBridge.Editor
{
    internal static class ObjectService
    {
        public static void Delete(string path)
        {
            var gameObject = ScenePath.ResolveObject(path);
            EditorPresentationService.ShowSceneObject(gameObject);
            var scene = gameObject.scene;
            Undo.DestroyObjectImmediate(gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static SceneObjectData Duplicate(string path)
        {
            var source = ScenePath.ResolveObject(path);
            var copy = UnityEngine.Object.Instantiate(source, source.transform.parent);
            copy.name = source.name;
            if (source.transform.parent == null && copy.scene != source.scene)
                SceneManager.MoveGameObjectToScene(copy, source.scene);
            Undo.RegisterCreatedObjectUndo(copy, "Unity Agent Bridge: Duplicate Object");
            EditorSceneManager.MarkSceneDirty(copy.scene);
            EditorPresentationService.ShowSceneObject(copy);
            return SceneService.GetObjectInfo(ScenePath.For(copy));
        }

        public static PrefabData[] ListPrefabs(string folderPath = null)
        {
            var folder = string.IsNullOrWhiteSpace(folderPath) ? BridgePaths.StandardPrefabFolder : folderPath.Trim().Replace('\\', '/').TrimEnd('/');
            if (!string.Equals(folder, "Assets", StringComparison.Ordinal) && !folder.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException("Prefab folder must be inside Assets: " + folder);
            if (!AssetDatabase.IsValidFolder(folder))
                return Array.Empty<PrefabData>();
            EditorPresentationService.ShowAssetFolder(folder);

            return AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new PrefabData
                {
                    name = Path.GetFileNameWithoutExtension(path),
                    assetPath = path
                })
                .ToArray();
        }

        public static SceneObjectData Move(string path, string destinationPath, int siblingIndex = -1)
        {
            var gameObject = ScenePath.ResolveObject(path);
            var destinationScene = ScenePath.ResolveDestinationScene(destinationPath);
            var newParent = ScenePath.ResolveParent(destinationPath);
            if (newParent != null && (newParent == gameObject.transform || newParent.IsChildOf(gameObject.transform)))
                throw new InvalidOperationException("An object cannot be moved below itself or one of its descendants.");

            Undo.SetTransformParent(gameObject.transform, newParent, "Unity Agent Bridge: Move Object");
            if (newParent == null && gameObject.scene != destinationScene)
                SceneManager.MoveGameObjectToScene(gameObject, destinationScene);
            if (siblingIndex >= 0)
            {
                var siblingCount = newParent == null
                    ? gameObject.scene.rootCount
                    : newParent.childCount;
                if (siblingIndex >= siblingCount)
                    throw new ArgumentOutOfRangeException("siblingIndex", "Sibling index must be between 0 and " + (siblingCount - 1) + ".");
                gameObject.transform.SetSiblingIndex(siblingIndex);
            }
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            return SceneService.GetObjectInfo(ScenePath.For(gameObject));
        }

        public static SceneObjectData Rename(string path, string destinationPath)
        {
            var gameObject = ScenePath.ResolveObject(path);
            var nameOnly = !string.IsNullOrWhiteSpace(destinationPath) && destinationPath.IndexOf('/') < 0;
            int requestedIndex = -1;
            var newName = nameOnly
                ? destinationPath
                : ScenePath.DestinationObjectName(destinationPath, out requestedIndex);
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New object name cannot be empty.", "newName");

            var parentPath = ScenePath.ParentOf(gameObject);
            if (!nameOnly && !string.Equals(parentPath, ScenePath.DestinationParentPath(destinationPath), StringComparison.Ordinal))
                throw new ArgumentException("Rename destination must keep the current parent. Use object-move separately.", "destinationPath");

            System.Collections.Generic.IEnumerable<Transform> siblings;
            if (gameObject.transform.parent == null)
                siblings = gameObject.scene.GetRootGameObjects().Select(item => item.transform);
            else
                siblings = Enumerable.Range(0, gameObject.transform.parent.childCount).Select(gameObject.transform.parent.GetChild);
            var resultingIndex = siblings.TakeWhile(item => item != gameObject.transform).Count(item => item.name == newName);
            if (!nameOnly && resultingIndex != requestedIndex)
                throw new ArgumentException("Destination path has the wrong same-name index. Expected [" + resultingIndex + "].", "destinationPath");

            destinationPath = parentPath + "/" + Uri.EscapeDataString(newName) + "[" + resultingIndex + "]";

            Undo.RecordObject(gameObject, "Unity Agent Bridge: Rename Object");
            gameObject.name = newName;
            EditorUtility.SetDirty(gameObject);
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
                PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            var actualPath = ScenePath.For(gameObject);
            if (!string.Equals(actualPath, destinationPath, StringComparison.Ordinal))
                throw new InvalidOperationException("Renamed object path does not match the requested destination: " + actualPath);
            return SceneService.GetObjectInfo(actualPath);
        }

        public static SceneObjectData SetActive(string path, bool active)
        {
            var gameObject = ScenePath.ResolveObject(path);
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                Undo.RecordObject(gameObject, "Unity Agent Bridge: Set Active");
            gameObject.SetActive(active);
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.SetDirty(gameObject);
                if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
            EditorPresentationService.ShowSceneObject(gameObject);
            return SceneService.GetObjectInfo(ScenePath.For(gameObject));
        }

        public static string SetTag(string path, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || tag.Any(char.IsControl))
                throw new ArgumentException("Tag must be a non-empty name without control characters.", "tag");

            if (path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                    AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    throw new InvalidOperationException("Prefab root was not found in Assets: " + path);

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    EnsureTagExists(tag);
                    root.tag = tag;
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                        throw new InvalidOperationException("Unity could not save the prefab tag: " + path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                EditorPresentationService.ShowAssetPath(path);
                return tag;
            }

            var gameObject = ScenePath.ResolveObject(path);
            EnsureTagExists(tag);
            Undo.RecordObject(gameObject, "Unity Agent Bridge: Set Tag");
            gameObject.tag = tag;
            EditorUtility.SetDirty(gameObject);
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
                PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            EditorPresentationService.ShowSceneObject(gameObject);
            return gameObject.tag;
        }

        private static void EnsureTagExists(string tag)
        {
            if (InternalEditorUtility.tags.Contains(tag))
                return;
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
                throw new InvalidOperationException("Unity TagManager asset was not found.");
            var tagManager = new SerializedObject(assets[0]);
            var tags = tagManager.FindProperty("tags");
            if (tags == null || !tags.isArray)
                throw new InvalidOperationException("Unity TagManager does not expose the tags array.");
            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            if (!tagManager.ApplyModifiedPropertiesWithoutUndo())
                throw new InvalidOperationException("Unity did not create the tag: " + tag);
            AssetDatabase.SaveAssets();
        }
    }
}
