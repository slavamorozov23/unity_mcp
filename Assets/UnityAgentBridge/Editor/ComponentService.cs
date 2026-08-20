using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityAgentBridge.Editor
{
    internal static class ComponentService
    {
        [Serializable]
        private sealed class StringBox { public string value; }
        [Serializable]
        private sealed class VectorBox { public float x; public float y; public float z; public float w; }
        [Serializable]
        private sealed class ColorBox { public float r; public float g; public float b; public float a; }
        [Serializable]
        private sealed class RectBox { public float x; public float y; public float width; public float height; }
        [Serializable]
        private sealed class RectOffsetBox
        {
            public int left; public int right; public int top; public int bottom;
            public int m_Left; public int m_Right; public int m_Top; public int m_Bottom;
        }
        [Serializable]
        private sealed class BoundsBox { public VectorBox center; public VectorBox size; }
        [Serializable]
        private sealed class VectorIntBox { public int x; public int y; public int z; }
        [Serializable]
        private sealed class UnityEventCallBox
        {
            public string target;
            public string method;
            public string mode;
            public string state;
            public string objectArgument;
            public int intArgument;
            public float floatArgument;
            public string stringArgument;
            public bool boolArgument;
        }
        [Serializable]
        private sealed class UnityEventCallsBox { public UnityEventCallBox[] calls; }
        [Serializable]
        private sealed class AnimationCurveBox
        {
            public AnimationKeyBox[] keys;
            public AnimationKeyBox[] m_Curve;
            public int preWrapMode;
            public int postWrapMode;
            public int m_PreInfinity;
            public int m_PostInfinity;
        }
        [Serializable]
        private sealed class AnimationKeyBox
        {
            public float time;
            public float value;
            public float inTangent;
            public float outTangent;
            public float inSlope;
            public float outSlope;
            public float inWeight;
            public float outWeight;
            public int weightedMode;
        }

        public static string[] GetAllComponentTypeNames()
        {
            return TypeCache.GetTypesDerivedFrom<Component>()
                .Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition)
                .Select(type => type.FullName)
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        public static SceneObjectData Modify(BridgeRequest request, out string warning)
        {
            var gameObject = ScenePath.ResolveObject(request.path);
            var component = ResolveAttachedComponent(gameObject, request.componentType, request.componentIndex);
            EditorPresentationService.ShowComponent(component);
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                Undo.RecordObject(component, "Unity Agent Bridge: Modify Component");
            ApplyValues(component, request.values, out warning);
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.SetDirty(component);
                if (PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            return SceneService.Describe(gameObject, HierarchyDepth(gameObject.transform));
        }

        public static SceneObjectData Add(BridgeRequest request, out string warning, out int componentIndex)
        {
            EnsureEditMode();
            var gameObject = ScenePath.ResolveObject(request.path);
            var type = ResolveComponentType(request.componentType);
            EnsureUnityScript(type);
            var existingCount = gameObject.GetComponents(type).Length;
            if (existingCount > 0 && type.GetCustomAttributes(typeof(DisallowMultipleComponent), true).Length > 0)
            {
                EditorPresentationService.ShowComponent(gameObject.GetComponents(type)[0]);
                warning = "Warning: the object already has its single allowed component of type " + type.FullName + ".";
                componentIndex = 0;
                return SceneService.Describe(gameObject, HierarchyDepth(gameObject.transform));
            }
            var component = Undo.AddComponent(gameObject, type);
            if (component == null)
            {
                if (existingCount > 0)
                {
                    warning = "Warning: Unity did not add another " + type.FullName + " because the object already has one.";
                    componentIndex = 0;
                    return SceneService.Describe(gameObject, HierarchyDepth(gameObject.transform));
                }
                throw new InvalidOperationException("Unity could not add component " + type.FullName + ".");
            }
            string valueWarning;
            EditorPresentationService.ShowComponent(component);
            ApplyValues(component, request.values, out valueWarning);
            EditorUtility.SetDirty(component);
            if (PrefabUtility.IsPartOfPrefabInstance(component))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            var duplicateWarning = existingCount > 0
                ? "Warning: the object already had " + existingCount + " component(s) of type " + type.FullName + "."
                : string.Empty;
            warning = string.Join(" ", new[] { duplicateWarning, valueWarning }.Where(value => !string.IsNullOrEmpty(value)).ToArray());
            componentIndex = Array.IndexOf(gameObject.GetComponents(type), component);
            if (componentIndex < 0)
                throw new InvalidOperationException("Added component is missing from its GameObject.");
            return SceneService.Describe(gameObject, HierarchyDepth(gameObject.transform));
        }

        public static SceneObjectData Remove(BridgeRequest request)
        {
            EnsureEditMode();
            var gameObject = ScenePath.ResolveObject(request.path);
            var component = ResolveAttachedComponent(gameObject, request.componentType, request.componentIndex);
            EditorPresentationService.ShowComponent(component);
            if (component is Transform)
                throw new InvalidOperationException("Transform cannot be removed from a GameObject.");
            Undo.DestroyObjectImmediate(component);
            return SceneService.Describe(gameObject, HierarchyDepth(gameObject.transform));
        }

        public static CandidateData[] GetObjectPickerCandidates(BridgeRequest request)
        {
            var gameObject = ScenePath.ResolveObject(request.path);
            var component = ResolveAttachedComponent(gameObject, request.componentType, request.componentIndex);
            EditorPresentationService.ShowComponent(component);
            var expectedType = ObjectReferenceType(component, request.propertyPath);
            var limit = Mathf.Clamp(request.limit <= 0 ? 10 : request.limit, 1, 10);
            var candidates = new List<CandidateData>();

            foreach (var item in Resources.FindObjectsOfTypeAll(expectedType))
            {
                if (item == null || EditorUtility.IsPersistent(item))
                    continue;

                var owner = Owner(item);
                if (owner == null || !IsListedLoadedScene(owner.scene))
                    continue;

                var path = ScenePath.For(owner);
                var candidatePath = path;
                var label = item.name;
                var candidateComponent = item as Component;
                if (candidateComponent != null)
                {
                    var concreteType = candidateComponent.GetType();
                    var matchingComponents = owner.GetComponents(concreteType).Cast<Component>().ToArray();
                    var componentIndex = Array.IndexOf(matchingComponents, candidateComponent);
                    if (componentIndex < 0)
                        throw new InvalidOperationException("Object Picker component is missing from its GameObject.");
                    candidatePath = path + "#" + Uri.EscapeDataString(concreteType.FullName) + "[" + componentIndex + "]";
                    label += " (" + concreteType.Name + " #" + componentIndex + ")";
                }

                candidates.Add(new CandidateData
                {
                    label = label,
                    path = candidatePath,
                    type = item.GetType().FullName,
                    source = "scene"
                });
            }

            candidates.AddRange(AssetObjectPickerCandidates(expectedType));

            return candidates
                .GroupBy(item => item.source + "\n" + item.path + "\n" + item.type)
                .Select(group => group.First())
                .OrderBy(item => item.source == "scene" ? 0 : item.path.StartsWith("Assets/", StringComparison.Ordinal) ? 1 : 2)
                .ThenBy(item => item.label, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.path, StringComparer.Ordinal)
                .Take(limit)
                .ToArray();
        }

        private static Type ObjectReferenceType(Component component, string propertyPath)
        {
            var serializedObject = new SerializedObject(component);
            var property = FindProperty(serializedObject, propertyPath);
            if (property == null)
                throw new InvalidOperationException("Serialized property was not found: " + propertyPath);
            if (property.propertyType == SerializedPropertyType.ObjectReference)
                return ResolveObjectReferenceType(property.type);
            if (property.isArray && property.propertyType == SerializedPropertyType.Generic)
                return ResolveObjectReferenceType(property.arrayElementType);
            throw new InvalidOperationException("Property is not an Object Reference or an Object Reference list: " + propertyPath);
        }

        private static IEnumerable<CandidateData> AssetObjectPickerCandidates(Type expectedType)
        {
            var candidates = new List<CandidateData>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + expectedType.Name))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var typeGroup in AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .Where(asset => asset != null && expectedType.IsAssignableFrom(asset.GetType()))
                    .GroupBy(asset => asset.GetType()))
                {
                    var typedAssets = typeGroup.ToArray();
                    for (var index = 0; index < typedAssets.Length; index++)
                    {
                        var asset = typedAssets[index];
                        candidates.Add(new CandidateData
                        {
                            label = asset.name,
                            path = assetPath + "#" + Uri.EscapeDataString(asset.GetType().FullName) + "[" + index + "]",
                            type = asset.GetType().FullName,
                            source = "asset"
                        });
                    }
                }
            }
            if (typeof(Component).IsAssignableFrom(expectedType))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var root = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (root == null)
                        continue;
                    foreach (var component in root.GetComponentsInChildren(expectedType, true).Cast<Component>())
                    {
                        var concreteType = component.GetType();
                        var components = component.gameObject.GetComponents(concreteType).Cast<Component>().ToArray();
                        var index = Array.IndexOf(components, component);
                        var childPath = PrefabChildIndexPath(root.transform, component.transform);
                        candidates.Add(new CandidateData
                        {
                            label = component.gameObject.name + " (" + concreteType.Name + " #" + index + ")",
                            path = assetPath + "#" + (childPath.Length == 0 ? string.Empty : childPath + "#") + Uri.EscapeDataString(concreteType.FullName) + "[" + index + "]",
                            type = concreteType.FullName,
                            source = "asset"
                        });
                    }
                }
            }
            return candidates
                .GroupBy(item => item.path + "\n" + item.type)
                .Select(group => group.First());
        }

        public static Type ResolveComponentType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException("Component type is required.", "typeName");

            var types = TypeCache.GetTypesDerivedFrom<Component>()
                .Where(type => !type.IsAbstract && (type.FullName == typeName || type.Name == typeName))
                .ToArray();
            if (types.Length == 0)
                throw new InvalidOperationException("Component type was not found: " + typeName);
            if (types.Length > 1)
                throw new InvalidOperationException("Component type name is ambiguous; use the full name: " + string.Join(", ", types.Select(type => type.FullName).ToArray()));
            return types[0];
        }

        internal static Component ResolveAttachedComponent(GameObject gameObject, string typeName, int componentIndex)
        {
            var type = ResolveComponentType(typeName);
            var components = gameObject.GetComponents(type).Cast<Component>().ToArray();
            if (components.Length == 0)
                throw new InvalidOperationException("Object has no component of type " + type.FullName + ".");
            if (componentIndex < 0 && components.Length > 1)
                throw new InvalidOperationException("Object has " + components.Length + " components of type " + type.FullName + "; provide componentIndex.");
            var index = componentIndex < 0 ? 0 : componentIndex;
            if (index >= components.Length)
                throw new InvalidOperationException("componentIndex is outside the available range 0.." + (components.Length - 1) + ".");
            return components[index];
        }

        internal static bool ApplyValues(Component component, PropertyValue[] values, out string warning)
        {
            warning = string.Empty;
            if (values == null || values.Length == 0)
                return false;

            var serializedObject = new SerializedObject(component);
            var expectedValues = new Dictionary<string, string>();
            var errors = new List<string>();
            Vector3? expectedPosition = null;
            var originalPosition = component.transform.position;
            foreach (var entry in values.OrderBy(entry => entry != null && entry.path != null && entry.path.EndsWith(".Array.size", StringComparison.Ordinal) ? 0 : 1))
            {
                try
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.path))
                        throw new InvalidOperationException("Every component value requires a serialized property path.");
                    var transform = component as Transform;
                    if (transform != null && string.Equals(entry.path, "position", StringComparison.Ordinal))
                    {
                        var position = ParseVector(entry.value);
                        transform.position = new Vector3(position.x, position.y, position.z);
                        expectedPosition = transform.position;
                        continue;
                    }
                    var property = FindProperty(serializedObject, entry.path);
                    if (property == null)
                        throw new InvalidOperationException("Serialized property was not found: " + entry.path);
                    SetProperty(property, entry.value);
                    expectedValues[property.propertyPath] = ComparableValue(property);
                }
                catch (Exception error)
                {
                    errors.Add((entry == null || string.IsNullOrWhiteSpace(entry.path) ? "<empty>" : entry.path) + ": " + error.Message);
                }
            }
            if (expectedValues.Count == 0 && !expectedPosition.HasValue)
                throw new InvalidOperationException(string.Join("; ", errors.ToArray()));
            var changed = serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            foreach (var expected in expectedValues)
            {
                var actual = serializedObject.FindProperty(expected.Key);
                if (actual == null || !string.Equals(ComparableValue(actual), expected.Value, StringComparison.Ordinal))
                {
                    var hint = component is RectTransform && expected.Key == "m_LocalPosition"
                        ? " RectTransform layout may drive this field; use m_AnchoredPosition."
                        : string.Empty;
                    throw new InvalidOperationException("Unity did not retain the serialized value: " + expected.Key + "." + hint);
                }
            }
            if (expectedPosition.HasValue && component.transform.position != expectedPosition.Value)
                throw new InvalidOperationException("Unity did not retain the Transform world position.");
            if (!EditorApplication.isPlayingOrWillChangePlaymode && component.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            if (errors.Count > 0)
                warning = "Skipped values: " + string.Join("; ", errors.ToArray());
            return changed || component.transform.position != originalPosition;
        }

        private static string ComparableValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.ArraySize:
                    return property.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean:
                    return property.boolValue ? "true" : "false";
                case SerializedPropertyType.Float:
                    return property.doubleValue.ToString("R", CultureInfo.InvariantCulture);
                case SerializedPropertyType.String:
                    return property.stringValue ?? string.Empty;
                case SerializedPropertyType.Color:
                    return Vector(property.colorValue.r, property.colorValue.g, property.colorValue.b, property.colorValue.a);
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue == null ? "null" : property.objectReferenceValue.GetInstanceID().ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Enum:
                    return property.enumValueIndex.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Vector2:
                    return Vector(property.vector2Value.x, property.vector2Value.y);
                case SerializedPropertyType.Vector3:
                    return Vector(property.vector3Value.x, property.vector3Value.y, property.vector3Value.z);
                case SerializedPropertyType.Vector4:
                    return Vector(property.vector4Value.x, property.vector4Value.y, property.vector4Value.z, property.vector4Value.w);
                case SerializedPropertyType.Vector2Int:
                    return property.vector2IntValue.x.ToString(CultureInfo.InvariantCulture) + "|" +
                        property.vector2IntValue.y.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Vector3Int:
                    return property.vector3IntValue.x.ToString(CultureInfo.InvariantCulture) + "|" +
                        property.vector3IntValue.y.ToString(CultureInfo.InvariantCulture) + "|" +
                        property.vector3IntValue.z.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Rect:
                    return Vector(property.rectValue.x, property.rectValue.y, property.rectValue.width, property.rectValue.height);
                case SerializedPropertyType.Bounds:
                    var bounds = property.boundsValue;
                    return Vector(bounds.center.x, bounds.center.y, bounds.center.z, bounds.size.x, bounds.size.y, bounds.size.z);
                case SerializedPropertyType.Quaternion:
                    var quaternion = property.quaternionValue;
                    return Vector(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
                case SerializedPropertyType.AnimationCurve:
                    return AnimationCurveSignature(property.animationCurveValue);
                case SerializedPropertyType.Generic:
                    if (property.type == "RectOffset")
                        return string.Join("|", new[] { "m_Left", "m_Right", "m_Top", "m_Bottom" }
                            .Select(name => property.FindPropertyRelative(name).intValue.ToString(CultureInfo.InvariantCulture)).ToArray());
                    SerializedProperty persistentCalls;
                    if (TryPersistentCalls(property, out persistentCalls))
                        return PersistentCallsSignature(persistentCalls);
                    if (property.type == "PersistentCall")
                        return PersistentCallSignature(property);
                    throw new NotSupportedException("Serialized Generic property is not supported: " + property.type);
                default:
                    throw new NotSupportedException("Serialized property cannot be verified by the component agent: " + property.propertyType);
            }
        }

        private static SerializedProperty FindProperty(SerializedObject serializedObject, string path)
        {
            var property = serializedObject.FindProperty(path);
            if (property != null || string.IsNullOrEmpty(path) || path.IndexOf('.') >= 0 || path.StartsWith("m_", StringComparison.Ordinal))
                return property;
            var serializedName = "m_" + char.ToUpperInvariant(path[0]) + path.Substring(1);
            return serializedObject.FindProperty(serializedName);
        }

        private static string Vector(params float[] values)
        {
            return string.Join("|", values.Select(value => value.ToString("R", CultureInfo.InvariantCulture)).ToArray());
        }

        internal static void EnsureEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Component changes are not allowed in Play Mode because Unity discards them when Play Mode stops.");
        }

        internal static SerializedPropertyData[] DescribeReferences(Component component)
        {
            var result = new List<SerializedPropertyData>();
            var serializedObject = new SerializedObject(component);
            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = iterator.hasVisibleChildren;
                if (iterator.propertyType != SerializedPropertyType.ObjectReference || iterator.propertyPath == "m_Script")
                    continue;
                string referencePath;
                if (!TryObjectReferencePath(iterator.objectReferenceValue, out referencePath))
                    continue;
                result.Add(new SerializedPropertyData
                {
                    path = iterator.propertyPath,
                    type = iterator.propertyType.ToString(),
                    value = referencePath,
                    writable = true
                });
            }
            return result.ToArray();
        }

        private static bool TryObjectReferencePath(UnityEngine.Object reference, out string path)
        {
            if (reference == null)
            {
                path = string.Empty;
                return true;
            }
            var assetPath = AssetDatabase.GetAssetPath(reference);
            if (!string.IsNullOrEmpty(assetPath))
            {
                var persistentComponent = reference as Component;
                if (persistentComponent != null && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    var root = persistentComponent.transform.root;
                    var persistentType = persistentComponent.GetType();
                    var components = persistentComponent.gameObject.GetComponents(persistentType).Cast<Component>().ToArray();
                    var persistentIndex = Array.IndexOf(components, persistentComponent);
                    var childPath = PrefabChildIndexPath(root, persistentComponent.transform);
                    path = assetPath + "#" + (childPath.Length == 0 ? string.Empty : childPath + "#") + Uri.EscapeDataString(persistentType.FullName) + "[" + persistentIndex + "]";
                    return true;
                }
                path = assetPath;
                return true;
            }
            var owner = Owner(reference);
            if (owner == null || !IsListedLoadedScene(owner.scene))
            {
                path = string.Empty;
                return false;
            }
            path = ScenePath.For(owner);
            var component = reference as Component;
            if (component == null)
                return true;
            var concreteType = component.GetType();
            var matching = owner.GetComponents(concreteType).Cast<Component>().ToArray();
            var index = Array.IndexOf(matching, component);
            if (index < 0)
                throw new InvalidOperationException("Referenced component is missing from its GameObject.");
            path += "#" + Uri.EscapeDataString(concreteType.FullName) + "[" + index + "]";
            return true;
        }

        internal static void SetProperty(SerializedProperty property, string rawValue)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.ArraySize:
                    property.intValue = int.Parse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    return;
                case SerializedPropertyType.Boolean:
                    property.boolValue = rawValue == "1" ? true : rawValue == "0" ? false : bool.Parse(rawValue);
                    return;
                case SerializedPropertyType.Float:
                    property.doubleValue = double.Parse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture);
                    return;
                case SerializedPropertyType.String:
                    property.stringValue = ParseJsonString(rawValue);
                    return;
                case SerializedPropertyType.Color:
                    var color = ParseColor(rawValue);
                    property.colorValue = new Color(color.r, color.g, color.b, color.a);
                    return;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = ResolveObjectReference(property.type, ParseJsonString(rawValue));
                    return;
                case SerializedPropertyType.Enum:
                    SetEnum(property, rawValue);
                    return;
                case SerializedPropertyType.Vector2:
                    var vector2 = ParseVector(rawValue);
                    property.vector2Value = new Vector2(vector2.x, vector2.y);
                    return;
                case SerializedPropertyType.Vector3:
                    var vector3 = ParseVector(rawValue);
                    property.vector3Value = new Vector3(vector3.x, vector3.y, vector3.z);
                    return;
                case SerializedPropertyType.Vector4:
                    var vector4 = ParseVector(rawValue);
                    property.vector4Value = new Vector4(vector4.x, vector4.y, vector4.z, vector4.w);
                    return;
                case SerializedPropertyType.Vector2Int:
                    if (rawValue == null || !HasField(rawValue, "x") || !HasField(rawValue, "y"))
                        throw new FormatException("Vector2Int value must contain x and y fields.");
                    var vector2Int = JsonUtility.FromJson<VectorIntBox>(rawValue);
                    property.vector2IntValue = new Vector2Int(vector2Int.x, vector2Int.y);
                    return;
                case SerializedPropertyType.Vector3Int:
                    if (rawValue == null || !HasField(rawValue, "x") || !HasField(rawValue, "y") || !HasField(rawValue, "z"))
                        throw new FormatException("Vector3Int value must contain x, y, and z fields.");
                    var vector3Int = JsonUtility.FromJson<VectorIntBox>(rawValue);
                    property.vector3IntValue = new Vector3Int(vector3Int.x, vector3Int.y, vector3Int.z);
                    return;
                case SerializedPropertyType.Rect:
                    var rect = JsonUtility.FromJson<RectBox>(rawValue);
                    property.rectValue = new Rect(rect.x, rect.y, rect.width, rect.height);
                    return;
                case SerializedPropertyType.Bounds:
                    var bounds = JsonUtility.FromJson<BoundsBox>(rawValue);
                    if (bounds == null || bounds.center == null || bounds.size == null)
                        throw new FormatException("Bounds value must contain center and size objects.");
                    property.boundsValue = new Bounds(
                        new Vector3(bounds.center.x, bounds.center.y, bounds.center.z),
                        new Vector3(bounds.size.x, bounds.size.y, bounds.size.z));
                    return;
                case SerializedPropertyType.Quaternion:
                    var quaternion = ParseVector(rawValue);
                    property.quaternionValue = new Quaternion(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
                    return;
                case SerializedPropertyType.Generic:
                    if (property.type == "RectOffset")
                    {
                        if (rawValue == null || !HasEitherField(rawValue, "left", "m_Left") ||
                            !HasEitherField(rawValue, "right", "m_Right") || !HasEitherField(rawValue, "top", "m_Top") ||
                            !HasEitherField(rawValue, "bottom", "m_Bottom"))
                            throw new FormatException("RectOffset value must contain left/right/top/bottom or m_Left/m_Right/m_Top/m_Bottom fields.");
                        var offset = JsonUtility.FromJson<RectOffsetBox>(rawValue);
                        property.FindPropertyRelative("m_Left").intValue = HasField(rawValue, "left") ? offset.left : offset.m_Left;
                        property.FindPropertyRelative("m_Right").intValue = HasField(rawValue, "right") ? offset.right : offset.m_Right;
                        property.FindPropertyRelative("m_Top").intValue = HasField(rawValue, "top") ? offset.top : offset.m_Top;
                        property.FindPropertyRelative("m_Bottom").intValue = HasField(rawValue, "bottom") ? offset.bottom : offset.m_Bottom;
                        return;
                    }
                    SerializedProperty calls;
                    if (TryPersistentCalls(property, out calls))
                    {
                        SetPersistentCalls(calls, rawValue);
                        return;
                    }
                    if (property.type == "PersistentCall")
                    {
                        SetPersistentCall(property, ParseUnityEventCall(rawValue));
                        return;
                    }
                    throw new NotSupportedException("Serialized Generic property is not supported: " + property.type);
                case SerializedPropertyType.AnimationCurve:
                    property.animationCurveValue = ParseAnimationCurve(rawValue);
                    return;
                default:
                    throw new NotSupportedException("Serialized property type is not supported by the component agent: " + property.propertyType);
            }
        }

        private static void SetEnum(SerializedProperty property, string rawValue)
        {
            int numericValue;
            if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out numericValue))
            {
                property.intValue = numericValue;
                return;
            }

            var name = ParseJsonString(rawValue);
            var index = Array.IndexOf(property.enumNames, name);
            if (index < 0)
                throw new InvalidOperationException("Enum value was not found. Available values: " + string.Join(", ", property.enumNames));
            property.enumValueIndex = index;
        }

        private static bool TryPersistentCalls(SerializedProperty property, out SerializedProperty calls)
        {
            if (property.isArray && string.Equals(property.arrayElementType, "PersistentCall", StringComparison.Ordinal))
            {
                calls = property;
                return true;
            }
            var persistent = property.FindPropertyRelative("m_PersistentCalls");
            calls = persistent == null ? null : persistent.FindPropertyRelative("m_Calls");
            return calls != null && calls.isArray;
        }

        private static void SetPersistentCalls(SerializedProperty calls, string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                throw new FormatException("UnityEvent value must be a JSON array of calls.");
            var json = rawValue.Trim();
            if (json.StartsWith("{", StringComparison.Ordinal))
                json = "[" + json + "]";
            if (!json.StartsWith("[", StringComparison.Ordinal))
                throw new FormatException("UnityEvent value must be a JSON array of calls.");
            var box = JsonUtility.FromJson<UnityEventCallsBox>("{\"calls\":" + json + "}");
            var values = box == null || box.calls == null ? new UnityEventCallBox[0] : box.calls;
            calls.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                SetPersistentCall(calls.GetArrayElementAtIndex(index), values[index]);
        }

        private static UnityEventCallBox ParseUnityEventCall(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue) || !rawValue.TrimStart().StartsWith("{", StringComparison.Ordinal))
                throw new FormatException("UnityEvent call must be a JSON object.");
            var value = JsonUtility.FromJson<UnityEventCallBox>(rawValue);
            if (value == null)
                throw new FormatException("UnityEvent call is invalid.");
            return value;
        }

        private static void SetPersistentCall(SerializedProperty property, UnityEventCallBox value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.target) || string.IsNullOrWhiteSpace(value.method))
                throw new FormatException("UnityEvent call requires target and method.");
            var targetProperty = RequiredRelative(property, "m_Target");
            var target = ResolveObjectReference(targetProperty.type, value.target);
            var gameObject = target as GameObject;
            if (gameObject != null)
            {
                var matches = gameObject.GetComponents<Component>()
                    .Where(item => item != null && item.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Any(method => method.Name == value.method))
                    .ToArray();
                if (matches.Length == 1)
                    target = matches[0];
                else if (matches.Length == 0)
                    throw new InvalidOperationException("UnityEvent target has no method " + value.method + ".");
                else
                    throw new InvalidOperationException("UnityEvent target method is ambiguous; use an exact component reference from object-info.");
            }
            targetProperty.objectReferenceValue = target;
            SetStringIfPresent(property, "m_TargetAssemblyTypeName", target.GetType().AssemblyQualifiedName);
            RequiredRelative(property, "m_MethodName").stringValue = value.method;
            var mode = UnityEventMode(value.mode);
            RequiredRelative(property, "m_Mode").intValue = mode;
            RequiredRelative(property, "m_CallState").intValue = UnityEventState(value.state);
            var arguments = RequiredRelative(property, "m_Arguments");
            RequiredRelative(arguments, "m_ObjectArgument").objectReferenceValue = null;
            SetStringIfPresent(arguments, "m_ObjectArgumentAssemblyTypeName", typeof(UnityEngine.Object).AssemblyQualifiedName);
            RequiredRelative(arguments, "m_IntArgument").intValue = value.intArgument;
            RequiredRelative(arguments, "m_FloatArgument").floatValue = value.floatArgument;
            RequiredRelative(arguments, "m_StringArgument").stringValue = value.stringArgument ?? string.Empty;
            RequiredRelative(arguments, "m_BoolArgument").boolValue = value.boolArgument;
            if (mode == 2 && !string.IsNullOrWhiteSpace(value.objectArgument))
            {
                var objectArgument = RequiredRelative(arguments, "m_ObjectArgument");
                objectArgument.objectReferenceValue = ResolveObjectReference(objectArgument.type, value.objectArgument);
                SetStringIfPresent(arguments, "m_ObjectArgumentAssemblyTypeName", objectArgument.objectReferenceValue.GetType().AssemblyQualifiedName);
            }
        }

        private static SerializedProperty RequiredRelative(SerializedProperty property, string name)
        {
            var value = property.FindPropertyRelative(name);
            if (value == null)
                throw new InvalidOperationException("UnityEvent field is missing: " + name);
            return value;
        }

        private static void SetStringIfPresent(SerializedProperty property, string name, string value)
        {
            var target = property.FindPropertyRelative(name);
            if (target != null)
                target.stringValue = value ?? string.Empty;
        }

        private static int UnityEventMode(string value)
        {
            switch ((value ?? "Void").Trim().ToLowerInvariant())
            {
                case "eventdefined": return 0;
                case "void": return 1;
                case "object": return 2;
                case "int": return 3;
                case "float": return 4;
                case "string": return 5;
                case "bool": return 6;
                default: throw new ArgumentException("UnityEvent mode must be EventDefined, Void, Object, Int, Float, String, or Bool.", "value");
            }
        }

        private static int UnityEventState(string value)
        {
            switch ((value ?? "RuntimeOnly").Trim().ToLowerInvariant())
            {
                case "off": return 0;
                case "editorandruntime": return 1;
                case "runtimeonly": return 2;
                default: throw new ArgumentException("UnityEvent state must be Off, EditorAndRuntime, or RuntimeOnly.", "value");
            }
        }

        private static string PersistentCallsSignature(SerializedProperty calls)
        {
            var result = new string[calls.arraySize];
            for (var index = 0; index < calls.arraySize; index++)
                result[index] = PersistentCallSignature(calls.GetArrayElementAtIndex(index));
            return string.Join(";", result);
        }

        private static string PersistentCallSignature(SerializedProperty call)
        {
            var target = RequiredRelative(call, "m_Target").objectReferenceValue;
            var arguments = RequiredRelative(call, "m_Arguments");
            var objectArgument = RequiredRelative(arguments, "m_ObjectArgument").objectReferenceValue;
            return string.Join("|", new[]
            {
                target == null ? "0" : target.GetInstanceID().ToString(CultureInfo.InvariantCulture),
                RequiredRelative(call, "m_MethodName").stringValue ?? string.Empty,
                RequiredRelative(call, "m_Mode").intValue.ToString(CultureInfo.InvariantCulture),
                RequiredRelative(call, "m_CallState").intValue.ToString(CultureInfo.InvariantCulture),
                objectArgument == null ? "0" : objectArgument.GetInstanceID().ToString(CultureInfo.InvariantCulture),
                RequiredRelative(arguments, "m_IntArgument").intValue.ToString(CultureInfo.InvariantCulture),
                RequiredRelative(arguments, "m_FloatArgument").floatValue.ToString("R", CultureInfo.InvariantCulture),
                RequiredRelative(arguments, "m_StringArgument").stringValue ?? string.Empty,
                RequiredRelative(arguments, "m_BoolArgument").boolValue ? "1" : "0"
            });
        }

        private static VectorBox ParseVector(string rawValue)
        {
            var result = JsonUtility.FromJson<VectorBox>(rawValue);
            if (result == null)
                throw new FormatException("Vector value must be a JSON object with x/y/z/w fields.");
            return result;
        }

        private static AnimationCurve ParseAnimationCurve(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                throw new FormatException("AnimationCurve value must contain a JSON array of keys.");
            var json = rawValue.Trim();
            if (json.StartsWith("[", StringComparison.Ordinal))
                json = "{\"keys\":" + json + "}";
            var box = JsonUtility.FromJson<AnimationCurveBox>(json);
            var serializedKeys = box == null ? null : box.keys ?? box.m_Curve;
            if (serializedKeys == null)
                throw new FormatException("AnimationCurve value must be a key array or an object containing keys.");
            var usesTangentNames = HasField(json, "inTangent") || HasField(json, "outTangent");
            var usesWeights = HasField(json, "inWeight") || HasField(json, "outWeight");
            var usesWeightedMode = HasField(json, "weightedMode");
            var keys = serializedKeys.Select(value =>
            {
                var inTangent = usesTangentNames ? value.inTangent : value.inSlope;
                var outTangent = usesTangentNames ? value.outTangent : value.outSlope;
                var key = usesWeights
                    ? new Keyframe(value.time, value.value, inTangent, outTangent, value.inWeight, value.outWeight)
                    : new Keyframe(value.time, value.value, inTangent, outTangent);
                if (usesWeightedMode)
                    key.weightedMode = (WeightedMode)value.weightedMode;
                return key;
            }).ToArray();
            var curve = new AnimationCurve(keys);
            if (HasEitherField(json, "preWrapMode", "m_PreInfinity"))
                curve.preWrapMode = (WrapMode)(HasField(json, "preWrapMode") ? box.preWrapMode : box.m_PreInfinity);
            if (HasEitherField(json, "postWrapMode", "m_PostInfinity"))
                curve.postWrapMode = (WrapMode)(HasField(json, "postWrapMode") ? box.postWrapMode : box.m_PostInfinity);
            return curve;
        }

        private static string AnimationCurveSignature(AnimationCurve curve)
        {
            if (curve == null)
                return "null";
            var keys = curve.keys.Select(key => string.Join("|", new[]
            {
                key.time.ToString("R", CultureInfo.InvariantCulture),
                key.value.ToString("R", CultureInfo.InvariantCulture),
                key.inTangent.ToString("R", CultureInfo.InvariantCulture),
                key.outTangent.ToString("R", CultureInfo.InvariantCulture),
                key.inWeight.ToString("R", CultureInfo.InvariantCulture),
                key.outWeight.ToString("R", CultureInfo.InvariantCulture),
                ((int)key.weightedMode).ToString(CultureInfo.InvariantCulture)
            })).ToArray();
            return ((int)curve.preWrapMode).ToString(CultureInfo.InvariantCulture) + ":" +
                ((int)curve.postWrapMode).ToString(CultureInfo.InvariantCulture) + ":" + string.Join(";", keys);
        }

        private static void EnsureUnityScript(Type type)
        {
            if (!typeof(MonoBehaviour).IsAssignableFrom(type))
                return;
            var registered = MonoImporter.GetAllRuntimeMonoScripts()
                .Any(script => script != null && script.GetClass() == type);
            if (!registered)
                throw new InvalidOperationException(
                    "Unity has no MonoScript for " + type.FullName + "; place this MonoBehaviour in " + type.Name + ".cs.");
        }

        private static ColorBox ParseColor(string rawValue)
        {
            if (rawValue == null
                || !Regex.IsMatch(rawValue, "\\\"r\\\"\\s*:")
                || !Regex.IsMatch(rawValue, "\\\"g\\\"\\s*:")
                || !Regex.IsMatch(rawValue, "\\\"b\\\"\\s*:")
                || !Regex.IsMatch(rawValue, "\\\"a\\\"\\s*:"))
                throw new FormatException("Color value must be a JSON object with r/g/b/a fields.");
            var result = JsonUtility.FromJson<ColorBox>(rawValue);
            if (result == null)
                throw new FormatException("Color value must be a JSON object with r/g/b/a fields.");
            return result;
        }

        private static bool HasField(string json, string name)
        {
            return Regex.IsMatch(json, "\\\"" + Regex.Escape(name) + "\\\"\\s*:");
        }

        private static bool HasEitherField(string json, string first, string second)
        {
            return HasField(json, first) || HasField(json, second);
        }

        private static string ParseJsonString(string rawValue)
        {
            if (rawValue == null)
                throw new ArgumentNullException("rawValue");
            var box = JsonUtility.FromJson<StringBox>("{\"value\":" + rawValue + "}");
            return box.value;
        }

        internal static Type ResolveObjectReferenceType(string serializedPropertyType)
        {
            if (!serializedPropertyType.StartsWith("PPtr<", StringComparison.Ordinal))
                serializedPropertyType = "PPtr<$" + serializedPropertyType + ">";
            const string prefix = "PPtr<";
            if (!serializedPropertyType.StartsWith(prefix, StringComparison.Ordinal) || !serializedPropertyType.EndsWith(">", StringComparison.Ordinal))
                throw new InvalidOperationException("Unknown Object Reference property type: " + serializedPropertyType);
            var typeName = serializedPropertyType.Substring(prefix.Length, serializedPropertyType.Length - prefix.Length - 1).TrimStart('$');
            var engineType = typeof(UnityEngine.Object).Assembly.GetType("UnityEngine." + typeName, false);
            if (engineType != null && typeof(UnityEngine.Object).IsAssignableFrom(engineType))
                return engineType;
            var matches = TypeCache.GetTypesDerivedFrom<UnityEngine.Object>()
                .Concat(new[] { typeof(GameObject), typeof(Component), typeof(UnityEngine.Object) })
                .Where(type => type.Name == typeName || type.FullName == typeName)
                .Distinct()
                .ToArray();
            if (matches.Length == 1)
                return matches[0];
            throw new InvalidOperationException("Object Reference type is missing or ambiguous: " + typeName);
        }

        private static UnityEngine.Object ResolveObjectReference(string serializedPropertyType, string reference)
        {
            if (string.IsNullOrEmpty(reference))
                return null;
            var expectedType = ResolveObjectReferenceType(serializedPropertyType);
            if (reference.StartsWith("Assets/", StringComparison.Ordinal) || reference.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var assetSelectorSeparator = reference.IndexOf('#');
                var assetPath = Uri.UnescapeDataString(assetSelectorSeparator < 0 ? reference : reference.Substring(0, assetSelectorSeparator));
                if (assetSelectorSeparator >= 0 && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    return ResolvePrefabComponentReference(assetPath, reference.Substring(assetSelectorSeparator + 1), expectedType);
                UnityEngine.Object asset;
                if (assetSelectorSeparator >= 0)
                {
                    var selector = reference.Substring(assetSelectorSeparator + 1);
                    var bracket = selector.LastIndexOf('[');
                    int assetIndex;
                    if (bracket <= 0 || !selector.EndsWith("]", StringComparison.Ordinal)
                        || !int.TryParse(selector.Substring(bracket + 1, selector.Length - bracket - 2), out assetIndex)
                        || assetIndex < 0)
                        throw new ArgumentException("Asset Object Picker reference is invalid.", "reference");
                    var typeName = Uri.UnescapeDataString(selector.Substring(0, bracket));
                    var matches = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                        .Where(item => item != null && expectedType.IsAssignableFrom(item.GetType())
                            && (item.GetType().FullName == typeName || item.GetType().Name == typeName))
                        .ToArray();
                    asset = assetIndex < matches.Length ? matches[assetIndex] : null;
                }
                else
                {
                    asset = AssetDatabase.LoadAssetAtPath(assetPath, expectedType);
                }
                if (asset == null)
                    throw new InvalidOperationException("Compatible asset reference was not found: " + assetPath);
                return asset;
            }

            var selectorSeparator = reference.LastIndexOf('#');
            var objectPath = selectorSeparator < 0 ? reference : reference.Substring(0, selectorSeparator);
            var gameObject = ScenePath.ResolveObject(objectPath);
            if (selectorSeparator >= 0)
            {
                var selector = reference.Substring(selectorSeparator + 1);
                var bracket = selector.LastIndexOf('[');
                var hasIndex = bracket > 0 && selector.EndsWith("]", StringComparison.Ordinal);
                var componentIndex = -1;
                if (hasIndex && (!int.TryParse(selector.Substring(bracket + 1, selector.Length - bracket - 2), out componentIndex) || componentIndex < 0))
                    throw new ArgumentException("Object Picker component index is invalid.", "reference");
                var componentTypeName = Uri.UnescapeDataString(hasIndex ? selector.Substring(0, bracket) : selector);
                var componentType = ResolveComponentType(componentTypeName);
                if (expectedType != typeof(UnityEngine.Object) && !expectedType.IsAssignableFrom(componentType))
                    throw new InvalidOperationException("Object Picker reference is not compatible with " + expectedType.FullName + ".");
                var exactComponents = gameObject.GetComponents(componentType).Cast<Component>().ToArray();
                if (!hasIndex && exactComponents.Length != 1)
                    throw new InvalidOperationException("Object Picker component reference is ambiguous; use an index.");
                if (!hasIndex)
                    return exactComponents[0];
                if (componentIndex >= exactComponents.Length)
                    throw new InvalidOperationException("Object Picker component index is outside the available range.");
                return exactComponents[componentIndex];
            }

            if (expectedType == typeof(GameObject) || expectedType == typeof(UnityEngine.Object))
                return gameObject;
            if (typeof(Component).IsAssignableFrom(expectedType))
            {
                var components = gameObject.GetComponents(expectedType).Cast<Component>().ToArray();
                if (components.Length == 0)
                    throw new InvalidOperationException("Referenced scene object lacks component " + expectedType.FullName + ".");
                if (components.Length > 1)
                    throw new InvalidOperationException("Referenced scene object has multiple compatible components; use the exact reference returned by Object Picker.");
                return components[0];
            }
            throw new InvalidOperationException("Scene object is not compatible with reference type " + expectedType.FullName + ".");
        }

        private static UnityEngine.Object ResolvePrefabComponentReference(string assetPath, string selector, Type expectedType)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (root == null)
                throw new InvalidOperationException("Prefab was not found: " + assetPath);
            var parts = selector.Split('#');
            if (parts.Length < 1 || parts.Length > 2)
                throw new ArgumentException("Prefab component reference is invalid.", "selector");
            var target = root.transform;
            if (parts.Length == 2 && parts[0].Length > 0)
            {
                foreach (var segment in parts[0].Split('/'))
                {
                    int childIndex;
                    if (!int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out childIndex) || childIndex < 0 || childIndex >= target.childCount)
                        throw new InvalidOperationException("Prefab component child path is invalid: " + parts[0]);
                    target = target.GetChild(childIndex);
                }
            }
            var componentSelector = parts[parts.Length - 1];
            var bracket = componentSelector.LastIndexOf('[');
            var hasIndex = bracket > 0 && componentSelector.EndsWith("]", StringComparison.Ordinal);
            var componentIndex = -1;
            if (hasIndex && (!int.TryParse(componentSelector.Substring(bracket + 1, componentSelector.Length - bracket - 2), out componentIndex) || componentIndex < 0))
                throw new ArgumentException("Prefab component index is invalid.", "selector");
            var componentType = ResolveComponentType(Uri.UnescapeDataString(hasIndex ? componentSelector.Substring(0, bracket) : componentSelector));
            if (!expectedType.IsAssignableFrom(componentType))
                throw new InvalidOperationException("Prefab component is not compatible with " + expectedType.FullName + ".");
            var components = target.GetComponents(componentType).Cast<Component>().ToArray();
            if (!hasIndex && components.Length != 1)
                throw new InvalidOperationException("Prefab component reference is ambiguous; use an index.");
            if (!hasIndex)
                return components[0];
            if (componentIndex >= components.Length)
                throw new InvalidOperationException("Prefab component index is outside the available range.");
            return components[componentIndex];
        }

        private static string PrefabChildIndexPath(Transform root, Transform target)
        {
            if (target == root)
                return string.Empty;
            var indices = new Stack<int>();
            for (var current = target; current != null && current != root; current = current.parent)
                indices.Push(current.GetSiblingIndex());
            return string.Join("/", indices.Select(index => index.ToString(CultureInfo.InvariantCulture)).ToArray());
        }

        private static GameObject Owner(UnityEngine.Object item)
        {
            var gameObject = item as GameObject;
            if (gameObject != null)
                return gameObject;
            var component = item as Component;
            return component == null ? null : component.gameObject;
        }

        private static bool IsListedLoadedScene(UnityEngine.SceneManagement.Scene target)
        {
            if (!target.IsValid() || !target.isLoaded)
                return false;
            return ScenePath.ContextScenes().Any(scene => scene == target);
        }

        private static int HierarchyDepth(Transform transform)
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
