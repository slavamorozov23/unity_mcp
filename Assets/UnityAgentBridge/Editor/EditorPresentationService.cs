using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityAgentBridge.Editor
{
    internal static class EditorPresentationService
    {
        private const BindingFlags InstanceMethod = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticMethod = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static void ShowSceneObject(GameObject target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            var hierarchyType = RequireEditorType("UnityEditor.SceneHierarchyWindow");
            var hierarchy = EditorWindow.GetWindow(hierarchyType);
            var setExpanded = RequireMethod(hierarchyType, "SetExpanded", InstanceMethod, typeof(int), typeof(bool));
            var ancestors = new Stack<Transform>();
            for (var current = target.transform.parent; current != null; current = current.parent)
                ancestors.Push(current);
            while (ancestors.Count > 0)
                setExpanded.Invoke(hierarchy, new object[] { ancestors.Pop().gameObject.GetInstanceID(), true });

            Selection.activeGameObject = target;
            RequireMethod(hierarchyType, "FrameObject", InstanceMethod, typeof(int), typeof(bool))
                .Invoke(hierarchy, new object[] { target.GetInstanceID(), true });
            hierarchy.Repaint();

            var sceneView = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            sceneView.Show();
            sceneView.Focus();
            var bounds = InternalEditorUtility.CalculateSelectionBounds(false, true);
            if (bounds.size.sqrMagnitude < 0.0001f)
                bounds = new Bounds(target.transform.position, Vector3.one * 2f);
            sceneView.Frame(bounds, true);
            sceneView.Repaint();
        }

        public static void ShowComponentOwner(GameObject target)
        {
            ShowSceneObject(target);
            var inspector = EditorWindow.GetWindow(RequireEditorType("UnityEditor.InspectorWindow"));
            inspector.Focus();
            inspector.Repaint();
        }

        public static void ShowComponent(Component component)
        {
            if (component == null)
                throw new ArgumentNullException("component");

            ShowSceneObject(component.gameObject);
            InternalEditorUtility.SetIsInspectorExpanded(component, true);
            var inspector = EditorWindow.GetWindow(RequireEditorType("UnityEditor.InspectorWindow"));
            inspector.Focus();
            inspector.Repaint();
            EditorApplication.delayCall += delegate { ScrollToComponent(inspector, component); };
        }

        public static void ShowInspectorObject(UnityEngine.Object target)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            ActiveEditorTracker.sharedTracker.isLocked = false;
            Selection.activeObject = target;
            var inspector = EditorWindow.GetWindow(RequireEditorType("UnityEditor.InspectorWindow"));
            inspector.Repaint();
        }

        public static void ShowAsset(UnityEngine.Object asset)
        {
            if (asset == null)
                throw new ArgumentNullException("asset");
            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
                throw new InvalidOperationException("Object is not an asset: " + asset.name);
            ShowAssetPath(assetPath, asset);
        }

        public static void ShowAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("Asset path is required.", "assetPath");
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null && !AssetDatabase.IsValidFolder(assetPath))
                throw new FileNotFoundException("Asset was not found: " + assetPath);
            ShowAssetPath(assetPath, asset);
        }

        public static void ShowAssetFolder(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
                throw new DirectoryNotFoundException("Asset folder was not found: " + folderPath);
            ShowProjectBrowser(folderPath, null);
        }

        private static void ShowAssetPath(string assetPath, UnityEngine.Object asset)
        {
            var folderPath = AssetDatabase.IsValidFolder(assetPath)
                ? assetPath
                : Path.GetDirectoryName(assetPath).Replace('\\', '/');
            ShowProjectBrowser(folderPath, asset);
        }

        private static void ShowProjectBrowser(string folderPath, UnityEngine.Object asset)
        {
            var browserType = RequireEditorType("UnityEditor.ProjectBrowser");
            var browser = EditorWindow.GetWindow(browserType);
            browser.Show();
            browser.Focus();
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            else
            {
                var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
                if (folder != null)
                    Selection.activeObject = folder;
            }

            EditorApplication.delayCall += delegate
            {
                if (browser == null)
                    return;
                var folderId = (int)RequireMethod(browserType, "GetFolderInstanceID", StaticMethod, typeof(string))
                    .Invoke(null, new object[] { folderPath });
                RequireMethod(browserType, "ShowFolderContents", InstanceMethod, typeof(int), typeof(bool))
                    .Invoke(browser, new object[] { folderId, true });
                if (asset != null)
                    RequireMethod(browserType, "FrameObject", InstanceMethod, typeof(int), typeof(bool))
                        .Invoke(browser, new object[] { asset.GetInstanceID(), true });
                browser.Focus();
                browser.Repaint();
            };
        }

        private static void ScrollToComponent(EditorWindow inspector, Component component)
        {
            if (inspector == null || component == null)
                return;
            var inspectorElementType = RequireEditorType("UnityEditor.UIElements.InspectorElement");
            var editorProperty = inspectorElementType.GetProperty("editor", InstanceMethod);
            if (editorProperty == null)
                throw new MissingMemberException(inspectorElementType.FullName, "editor");
            var scrollView = inspector.rootVisualElement.Query<ScrollView>().First();
            foreach (var element in inspector.rootVisualElement.Query<VisualElement>().ToList())
            {
                if (!inspectorElementType.IsInstanceOfType(element))
                    continue;
                var editor = editorProperty.GetValue(element, null) as UnityEditor.Editor;
                if (editor == null || !editor.targets.Contains(component))
                    continue;
                scrollView.ScrollTo(element);
                inspector.Focus();
                inspector.Repaint();
                return;
            }
        }

        private static Type RequireEditorType(string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            if (type == null)
                throw new MissingMemberException("Unity editor window type is unavailable: " + fullName);
            return type;
        }

        private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameters)
        {
            var method = type.GetMethod(name, flags, null, parameters, null);
            if (method == null)
                throw new MissingMethodException(type.FullName, name);
            return method;
        }
    }
}
