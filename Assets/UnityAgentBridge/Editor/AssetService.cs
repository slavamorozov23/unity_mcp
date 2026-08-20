using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityAgentBridge.Editor
{
    internal static class AssetService
    {
        private const string TmpFontAssetType = "TMPro.TMP_FontAsset";
        private const string ClearTmpDynamicData = "clear-dynamic-data";
        private static readonly CreationTemplateData[] Templates =
        {
            new CreationTemplateData { name = "C# Script", extension = ".cs" },
            new CreationTemplateData { name = "C# Editor Script", extension = ".cs" },
            new CreationTemplateData { name = "ScriptableObject Script", extension = ".cs" },
            new CreationTemplateData { name = "Text File", extension = ".txt" },
            new CreationTemplateData { name = "JSON File", extension = ".json" },
            new CreationTemplateData { name = "Shader", extension = ".shader" },
            new CreationTemplateData { name = "Compute Shader", extension = ".compute" },
            new CreationTemplateData { name = "Animator Controller", extension = ".controller" },
            new CreationTemplateData { name = "Animation Clip", extension = ".anim" },
            new CreationTemplateData { name = "Material", extension = ".mat" },
            new CreationTemplateData { name = "Scene", extension = ".unity" }
        };

        public static CreationTemplateData[] ListCreationTemplates()
        {
            return Templates.ToArray();
        }

        public static AssetData Create(string templateName, string assetPath)
        {
            var template = Templates.SingleOrDefault(item => string.Equals(item.name, templateName, StringComparison.OrdinalIgnoreCase));
            if (template == null)
                throw new InvalidOperationException("Creation Template was not found: " + templateName);
            assetPath = ValidateNewAssetPath(assetPath, template.extension);
            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));

            switch (template.name)
            {
                case "C# Script":
                    WriteText(assetPath, "using UnityEngine;\n\npublic sealed class " + ClassName(assetPath) + " : MonoBehaviour\n{\n}\n");
                    break;
                case "C# Editor Script":
                    WriteText(assetPath, "using UnityEditor;\nusing UnityEngine;\n\npublic sealed class " + ClassName(assetPath) + " : EditorWindow\n{\n}\n");
                    break;
                case "ScriptableObject Script":
                    WriteText(assetPath, "using UnityEngine;\n\n[CreateAssetMenu]\npublic sealed class " + ClassName(assetPath) + " : ScriptableObject\n{\n}\n");
                    break;
                case "Text File":
                    WriteText(assetPath, string.Empty);
                    break;
                case "JSON File":
                    WriteText(assetPath, "{}\n");
                    break;
                case "Shader":
                    WriteText(assetPath, "Shader \"Custom/" + ClassName(assetPath) + "\"\n{\n    SubShader { Pass { } }\n}\n");
                    break;
                case "Compute Shader":
                    WriteText(assetPath, "#pragma kernel CSMain\n\n[numthreads(8, 8, 1)]\nvoid CSMain(uint3 id : SV_DispatchThreadID)\n{\n}\n");
                    break;
                case "Animator Controller":
                    AnimatorController.CreateAnimatorControllerAtPath(assetPath);
                    break;
                case "Animation Clip":
                    AssetDatabase.CreateAsset(new AnimationClip(), assetPath);
                    break;
                case "Material":
                    var shader = Shader.Find("Standard");
                    if (shader == null)
                        throw new InvalidOperationException("Unity shader 'Standard' was not found.");
                    AssetDatabase.CreateAsset(new Material(shader), assetPath);
                    break;
                case "Scene":
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    EditorSceneManager.SaveScene(scene, assetPath);
                    EditorSceneManager.CloseScene(scene, true);
                    break;
                default:
                    throw new InvalidOperationException("Creation Template is not implemented: " + template.name);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            return GetInfo(assetPath);
        }

        public static AssetData GetInfo(string assetPath, string propertyPath = null)
        {
            assetPath = ValidateExistingAssetPath(assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
                throw new InvalidOperationException("Asset could not be loaded: " + assetPath);
            EditorPresentationService.ShowAsset(asset);
            var importer = AssetImporter.GetAtPath(assetPath);
            var properties = new List<SerializedPropertyData>();
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                AddProperties(asset, "asset", properties);
                if (importer != null)
                    AddProperties(importer, "importer", properties);
                var script = asset as MonoScript;
                if (script != null && script.GetClass() != null)
                    AddScriptFields(script.GetClass(), properties);
            }
            else
            {
                AddSelectedProperties(asset, importer, propertyPath, properties);
            }

            return new AssetData
            {
                name = asset.name,
                assetPath = assetPath,
                type = asset.GetType().FullName,
                importerType = importer == null ? string.Empty : importer.GetType().FullName,
                properties = properties.ToArray(),
                actions = Actions(asset)
            };
        }

        public static string ExecuteAction(string assetPath, string action)
        {
            assetPath = ValidateExistingAssetPath(assetPath);
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
                throw new InvalidOperationException("Asset could not be loaded: " + assetPath);
            if (!string.Equals(action, ClearTmpDynamicData, StringComparison.Ordinal))
                throw new InvalidOperationException("Unknown asset action: " + action);
            if (!string.Equals(asset.GetType().FullName, TmpFontAssetType, StringComparison.Ordinal))
                throw new InvalidOperationException("Action clear-dynamic-data requires a TMP_FontAsset.");
            if (!AssetDatabase.IsOpenForEdit(asset))
                throw new InvalidOperationException("TMP font asset is not open for editing: " + assetPath);

            var clear = asset.GetType().GetMethod(
                "ClearFontAssetData",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(bool) },
                null);
            if (clear == null)
                throw new MissingMethodException(asset.GetType().FullName, "ClearFontAssetData(Boolean)");
            var eventType = Type.GetType("TMPro.TMPro_EventManager, Unity.TextMeshPro", false);
            var changed = eventType == null ? null : eventType.GetMethod(
                "ON_FONT_PROPERTY_CHANGED",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(bool), typeof(UnityEngine.Object) },
                null);
            if (changed == null)
                throw new MissingMethodException("TMPro.TMPro_EventManager", "ON_FONT_PROPERTY_CHANGED(Boolean,Object)");

            EditorPresentationService.ShowAsset(asset);
            clear.Invoke(asset, new object[] { true });
            changed.Invoke(null, new object[] { true, asset });
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return "Cleared dynamic data: " + assetPath;
        }

        private static InspectorActionData[] Actions(UnityEngine.Object asset)
        {
            if (asset == null || !string.Equals(asset.GetType().FullName, TmpFontAssetType, StringComparison.Ordinal))
                return Array.Empty<InspectorActionData>();
            return new[]
            {
                new InspectorActionData { id = ClearTmpDynamicData, label = "Clear Dynamic Data" }
            };
        }

        public static AssetData Reimport(string assetPath)
        {
            assetPath = ValidateExistingAssetPath(assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return GetInfo(assetPath);
        }

        public static string GetSpriteLayout(string assetPath)
        {
            assetPath = ValidateExistingAssetPath(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Sprite editor requires a texture asset: " + assetPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
                throw new InvalidOperationException("Texture could not be loaded: " + assetPath);
            EditorPresentationService.ShowAsset(texture);
            return JsonUtility.ToJson(BuildSpriteLayout(assetPath, texture, importer));
        }

#pragma warning disable 618
        public static string MutateSpriteLayout(string assetPath, string action, string json)
        {
            assetPath = ValidateExistingAssetPath(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Sprite editor requires a texture asset: " + assetPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
                throw new InvalidOperationException("Texture could not be loaded: " + assetPath);

            var payload = string.IsNullOrWhiteSpace(json)
                ? new SpriteMutationPayload()
                : JsonUtility.FromJson<SpriteMutationPayload>(json);
            if (payload == null)
                throw new ArgumentException("Sprite editor payload is invalid.", "json");

            switch (action)
            {
                case "auto":
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Multiple;
                    importer.spritesheet = AutomaticSpriteSheet(texture);
                    break;
                case "manual":
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Multiple;
                    importer.spritesheet = ManualSpriteSheet(payload.slices, texture.width, texture.height);
                    break;
                case "border":
                    if (payload.border == null)
                        throw new ArgumentException("border action requires border.", "json");
                    ValidateBorder(payload.border, texture.width, texture.height);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spriteBorder = new Vector4(
                        payload.border.left,
                        payload.border.bottom,
                        payload.border.right,
                        payload.border.top);
                    break;
                default:
                    throw new ArgumentException("Sprite editor action must be auto, manual or border.", "action");
            }

            importer.SaveAndReimport();
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            EditorPresentationService.ShowAsset(texture);
            return JsonUtility.ToJson(BuildSpriteLayout(assetPath, texture, importer));
        }
#pragma warning restore 618

#pragma warning disable 618
        private static SpriteMetaData[] AutomaticSpriteSheet(Texture2D texture)
        {
            var utility = typeof(EditorApplication).Assembly.GetType("UnityEditorInternal.InternalSpriteUtility", true);
            var method = utility.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(candidate => candidate.Name == "GenerateAutomaticSpriteRectangles")
                .Single(candidate =>
                {
                    var parameters = candidate.GetParameters();
                    return parameters.Length >= 2
                        && parameters[0].ParameterType == typeof(Texture2D)
                        && parameters.Skip(1).All(parameter => parameter.ParameterType == typeof(int));
                });
            var arguments = new object[method.GetParameters().Length];
            arguments[0] = texture;
            for (var index = 1; index < arguments.Length; index++)
                arguments[index] = index == 1 ? 4 : 0;
            var rectangles = method.Invoke(null, arguments) as Rect[];
            if (rectangles == null || rectangles.Length == 0)
                throw new InvalidOperationException("Unity automatic sprite slicing found no sprites.");
            return rectangles
                .OrderByDescending(rectangle => rectangle.y)
                .ThenBy(rectangle => rectangle.x)
                .Select((rectangle, index) => new SpriteMetaData
                {
                    name = texture.name + "_" + index.ToString(CultureInfo.InvariantCulture),
                    rect = rectangle,
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero
                })
                .ToArray();
        }

        private static SpriteMetaData[] ManualSpriteSheet(SpriteSliceInput[] slices, int width, int height)
        {
            if (slices == null || slices.Length == 0)
                throw new ArgumentException("manual action requires at least one slice.", "json");
            var names = new HashSet<string>(StringComparer.Ordinal);
            return slices.Select((slice, index) =>
            {
                if (slice == null || slice.width <= 0 || slice.height <= 0 || slice.x < 0 || slice.y < 0
                    || slice.x + slice.width > width || slice.y + slice.height > height)
                    throw new ArgumentException("Sprite slice " + index + " is outside the texture or has an invalid size.", "json");
                var name = string.IsNullOrWhiteSpace(slice.name)
                    ? "slice_" + index.ToString(CultureInfo.InvariantCulture)
                    : slice.name.Trim();
                if (!names.Add(name))
                    throw new ArgumentException("Sprite slice names must be unique: " + name, "json");
                return new SpriteMetaData
                {
                    name = name,
                    rect = new Rect(slice.x, slice.y, slice.width, slice.height),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero
                };
            }).ToArray();
        }
#pragma warning restore 618

        private static void ValidateBorder(SpriteBorderInput border, int width, int height)
        {
            if (border.left < 0 || border.right < 0 || border.top < 0 || border.bottom < 0
                || border.left + border.right > width || border.top + border.bottom > height)
                throw new ArgumentException("Sprite border is outside the texture.", "json");
        }

#pragma warning disable 618
        private static SpriteLayoutData BuildSpriteLayout(string assetPath, Texture2D texture, TextureImporter importer)
        {
            var border = importer.spriteBorder;
            var sheet = importer.spriteImportMode == SpriteImportMode.Multiple
                ? importer.spritesheet
                : Array.Empty<SpriteMetaData>();
            return new SpriteLayoutData
            {
                path = assetPath,
                width = texture.width,
                height = texture.height,
                mode = importer.spriteImportMode == SpriteImportMode.Multiple ? "multiple" : "single",
                border = new SpriteBorderInput
                {
                    left = border.x,
                    bottom = border.y,
                    right = border.z,
                    top = border.w
                },
                slices = sheet.Select(item => new SpriteSliceInput
                {
                    name = item.name,
                    x = item.rect.x,
                    y = item.rect.y,
                    width = item.rect.width,
                    height = item.rect.height
                }).ToArray()
            };
        }
#pragma warning restore 618

        [Serializable]
        private sealed class SpriteMutationPayload
        {
            public SpriteSliceInput[] slices = Array.Empty<SpriteSliceInput>();
            public SpriteBorderInput border;
        }

        [Serializable]
        private sealed class SpriteLayoutData
        {
            public string path;
            public int width;
            public int height;
            public string mode;
            public SpriteSliceInput[] slices = Array.Empty<SpriteSliceInput>();
            public SpriteBorderInput border;
        }

        [Serializable]
        private sealed class SpriteSliceInput
        {
            public string name;
            public float x;
            public float y;
            public float width;
            public float height;
        }

        [Serializable]
        private sealed class SpriteBorderInput
        {
            public float left;
            public float right;
            public float top;
            public float bottom;
        }

        public static AssetData Move(string assetPath, string destinationPath)
        {
            assetPath = ValidateExistingAssetPath(assetPath);
            destinationPath = NormalizeAssetPath(destinationPath);
            EnsureFolder(Path.GetDirectoryName(destinationPath).Replace('\\', '/'));
            var error = AssetDatabase.MoveAsset(assetPath, destinationPath);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(error);
            AssetDatabase.SaveAssets();
            return GetInfo(destinationPath);
        }

        public static string Delete(string assetPath)
        {
            assetPath = ValidateExistingAssetPath(assetPath);
            if (!AssetDatabase.DeleteAsset(assetPath))
                throw new InvalidOperationException("Unity could not delete asset: " + assetPath);
            AssetDatabase.SaveAssets();
            return "Asset deleted: " + assetPath;
        }

        public static AssetData Modify(string assetPath, PropertyValue[] values)
        {
            assetPath = ValidateExistingAssetPath(assetPath);
            if (values == null || values.Length == 0)
                throw new InvalidOperationException("Asset modification requires at least one property value.");
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath);
            var assetObject = asset == null ? null : new SerializedObject(asset);
            var importerObject = importer == null ? null : new SerializedObject(importer);
            var importerChanged = false;
            var assetChanged = false;

            foreach (var entry in values)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.path))
                    throw new InvalidOperationException("Every asset value requires a serialized property path.");
                string source;
                string propertyPath;
                ResolvePropertyPath(entry.path, assetObject, importerObject, out source, out propertyPath);
                if (source == "asset")
                {
                    var assetProperty = assetObject == null ? null : assetObject.FindProperty(propertyPath);
                    if (assetProperty == null)
                        throw new InvalidOperationException("Serialized asset property was not found: " + propertyPath);
                    ComponentService.SetProperty(assetProperty, entry.value);
                    assetChanged = true;
                    continue;
                }
                var importerProperty = importerObject == null ? null : importerObject.FindProperty(propertyPath);
                if (importerProperty == null)
                    throw new InvalidOperationException("Serialized importer property was not found: " + propertyPath);
                ComponentService.SetProperty(importerProperty, entry.value);
                importerChanged = true;
            }

            if (assetChanged)
            {
                assetObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }
            if (importerChanged)
            {
                importerObject.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
            }
            return GetInfo(assetPath);
        }

        public static CandidateData[] GetObjectPickerCandidates(string assetPath, string propertyPath, int limit)
        {
            assetPath = ValidateExistingAssetPath(assetPath);
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
                throw new InvalidOperationException("Asset could not be loaded: " + assetPath);
            EditorPresentationService.ShowAsset(asset);
            var importer = AssetImporter.GetAtPath(assetPath);
            string source;
            string rawPropertyPath;
            ResolvePropertyPath(
                propertyPath,
                asset == null ? null : new SerializedObject(asset),
                importer == null ? null : new SerializedObject(importer),
                out source,
                out rawPropertyPath);
            var property = source == "asset"
                ? asset == null ? null : new SerializedObject(asset).FindProperty(rawPropertyPath)
                : importer == null ? null : new SerializedObject(importer).FindProperty(rawPropertyPath);
            if (property == null)
                throw new InvalidOperationException("Serialized " + source + " property was not found: " + rawPropertyPath);
            if (property.propertyType != SerializedPropertyType.ObjectReference)
                throw new InvalidOperationException("Asset Object Picker requires an object-reference property: " + propertyPath);
            var targetType = ComponentService.ResolveObjectReferenceType(property.type);

            return AssetDatabase.FindAssets("t:" + targetType.Name)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(path => path.StartsWith("Assets/", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(path => path, StringComparer.Ordinal)
                .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path)
                    .Where(asset => asset != null && targetType.IsAssignableFrom(asset.GetType()))
                    .GroupBy(asset => asset.GetType())
                    .SelectMany(group => group.Select((asset, index) => new
                    {
                        path = path + "#" + Uri.EscapeDataString(asset.GetType().FullName) + "[" + index + "]",
                        asset
                    })))
                .Take(limit)
                .Select(item => new CandidateData
                {
                    label = item.asset.name,
                    path = item.path,
                    type = item.asset.GetType().FullName,
                    source = "asset"
                })
                .ToArray();
        }

        private static void AddProperties(UnityEngine.Object target, string source, ICollection<SerializedPropertyData> result)
        {
            var serialized = new SerializedObject(target);
            var iterator = serialized.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                result.Add(new SerializedPropertyData
                {
                    path = source + ":" + iterator.propertyPath,
                    type = iterator.propertyType.ToString(),
                    value = PropertyValue(iterator),
                    writable = iterator.propertyPath != "m_Script"
                });
            }
        }

        private static void AddSelectedProperties(
            UnityEngine.Object asset,
            AssetImporter importer,
            string qualifiedPath,
            ICollection<SerializedPropertyData> result)
        {
            var serializedAsset = new SerializedObject(asset);
            var serializedImporter = importer == null ? null : new SerializedObject(importer);
            string source;
            string propertyPath;
            ResolvePropertyPath(qualifiedPath, serializedAsset, serializedImporter, out source, out propertyPath);
            var serialized = source == "asset" ? serializedAsset : serializedImporter;
            var property = serialized.FindProperty(propertyPath);
            if (property == null)
                throw new ArgumentException("Serialized property was not found: " + qualifiedPath, "qualifiedPath");
            AddProperty(property, source, result);
            if (property.isArray)
                return;
            var current = property.Copy();
            var end = property.GetEndProperty();
            if (!current.NextVisible(true))
                return;
            while (!SerializedProperty.EqualContents(current, end))
            {
                AddProperty(current, source, result);
                if (!current.NextVisible(true))
                    break;
            }
        }

        private static void AddProperty(SerializedProperty property, string source, ICollection<SerializedPropertyData> result)
        {
            result.Add(new SerializedPropertyData
            {
                path = source + ":" + property.propertyPath,
                type = property.propertyType.ToString(),
                value = PropertyValue(property),
                writable = property.propertyPath != "m_Script"
            });
        }

        private static void AddScriptFields(Type type, ICollection<SerializedPropertyData> result)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => field.IsPublic || field.GetCustomAttributes(typeof(SerializeField), true).Length > 0)
                .OrderBy(field => field.Name, StringComparer.Ordinal))
            {
                result.Add(new SerializedPropertyData
                {
                    path = "script:" + field.Name,
                    type = field.FieldType.FullName,
                    value = string.Empty,
                    writable = false
                });
            }
        }

        private static string PropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: return property.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean: return property.boolValue ? "true" : "false";
                case SerializedPropertyType.Float: return property.doubleValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.String: return property.stringValue;
                case SerializedPropertyType.Enum: return property.enumDisplayNames.Length > property.enumValueIndex && property.enumValueIndex >= 0 ? property.enumDisplayNames[property.enumValueIndex] : property.enumValueIndex.ToString();
                case SerializedPropertyType.ObjectReference: return property.objectReferenceValue == null ? string.Empty : AssetDatabase.GetAssetPath(property.objectReferenceValue);
                case SerializedPropertyType.Vector2: return property.vector2Value.ToString("G9");
                case SerializedPropertyType.Vector3: return property.vector3Value.ToString("G9");
                case SerializedPropertyType.Vector4: return property.vector4Value.ToString("G9");
                case SerializedPropertyType.Color: return property.colorValue.ToString();
                default: return property.isArray ? "Array[" + property.arraySize + "]" : string.Empty;
            }
        }

        private static string ValidateNewAssetPath(string path, string extension)
        {
            path = NormalizeAssetPath(path);
            if (!string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Creation Template requires extension " + extension + ".");
            if (File.Exists(ToFullPath(path)) || AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new IOException("Asset already exists: " + path);
            return path;
        }

        private static void SplitPropertyPath(string qualifiedPath, out string source, out string propertyPath)
        {
            var separator = qualifiedPath == null ? -1 : qualifiedPath.IndexOf(':');
            if (separator <= 0 || separator == qualifiedPath.Length - 1)
                throw new ArgumentException("Property must use asset:<path> or importer:<path>.", "qualifiedPath");
            source = qualifiedPath.Substring(0, separator);
            propertyPath = qualifiedPath.Substring(separator + 1);
            if (source != "asset" && source != "importer")
                throw new ArgumentException("Writable property source must be 'asset' or 'importer'.", "qualifiedPath");
        }

        private static void ResolvePropertyPath(
            string path,
            SerializedObject asset,
            SerializedObject importer,
            out string source,
            out string propertyPath)
        {
            if (!string.IsNullOrWhiteSpace(path) && path.IndexOf(':') < 0)
            {
                var inAsset = asset != null && asset.FindProperty(path) != null;
                var inImporter = importer != null && importer.FindProperty(path) != null;
                if (inAsset == inImporter)
                    throw new ArgumentException(inAsset
                        ? "Property exists on both asset and importer; use asset:<path> or importer:<path>."
                        : "Serialized asset or importer property was not found: " + path,
                        "path");
                source = inAsset ? "asset" : "importer";
                propertyPath = path;
                return;
            }
            SplitPropertyPath(path, out source, out propertyPath);
        }

        private static string ValidateExistingAssetPath(string path)
        {
            path = NormalizeAssetPath(path);
            if (!File.Exists(ToFullPath(path)) && !Directory.Exists(ToFullPath(path)))
                throw new FileNotFoundException("Asset was not found.", path);
            return path;
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Asset path is required.", "path");
            path = path.Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("../") || path.EndsWith("/", StringComparison.Ordinal))
                throw new ArgumentException("Asset path must point to a file below Assets.", "path");
            ToFullPath(path);
            return path;
        }

        private static string ToFullPath(string assetPath)
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var full = Path.GetFullPath(Path.Combine(root, assetPath));
            if (!full.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Asset path escaped the project's Assets folder.");
            return full;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || folder == "Assets" || AssetDatabase.IsValidFolder(folder))
                return;
            EnsureFolder(Path.GetDirectoryName(folder).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
        }

        private static void WriteText(string assetPath, string contents)
        {
            File.WriteAllText(ToFullPath(assetPath), contents, new UTF8Encoding(false));
        }

        private static string ClassName(string assetPath)
        {
            var name = Regex.Replace(Path.GetFileNameWithoutExtension(assetPath), "[^A-Za-z0-9_]", string.Empty);
            if (string.IsNullOrEmpty(name) || char.IsDigit(name[0]))
                throw new InvalidOperationException("Asset file name cannot form a valid C# class name: " + assetPath);
            return name;
        }
    }
}
