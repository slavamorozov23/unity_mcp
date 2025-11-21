using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

namespace SceneAPI.Modules
{
    public class GetAssetInfoModule
    {
        [Serializable]
        public class AssetInfoRequest
        {
            public string assetPath;
            public string guid;
            public bool includeSerializedProperties = true;
            public bool includeImporterSerialized = true;
            public bool includeSubAssets = true;
            public bool includeDependencies = false;
            public bool flattenPropertyPaths = false;
        }

        [Serializable]
        public class AssetProperty
        {
            public string name;
            public string displayName;
            public string type;
            public string path;
            public object value;
            public bool isEditable;
            public string description;
        }

        [Serializable]
        public class SubAssetInfo
        {
            public string name;
            public string type;
            public long localId;
        }

        [Serializable]
        public class AssetInfo
        {
            public string path;
            public string name;
            public string type;
            public string typeFullName;
            public string guid;
            public bool isFolder;

            public long fileSize;
            public string lastModified;

            public string[] labels;
            public List<SubAssetInfo> subAssets;
            public string[] dependencies;

            public List<AssetProperty> properties;
            public Dictionary<string, object> importSettings;
        }

        public string Execute(string requestBody)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<AssetInfoRequest>(requestBody) ?? new AssetInfoRequest();

                if (!string.IsNullOrEmpty(request.guid) && string.IsNullOrEmpty(request.assetPath))
                {
                    var pathByGuid = AssetDatabase.GUIDToAssetPath(request.guid);
                    if (!string.IsNullOrEmpty(pathByGuid))
                        request.assetPath = pathByGuid;
                }

                if (string.IsNullOrWhiteSpace(request.assetPath))
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "Asset path or guid is required"
                    }, Formatting.Indented);
                }

                var assetPath = NormalizeAssetsPath(request.assetPath);
                bool exists = AssetExists(assetPath);
                if (!exists)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Asset not found at path: {assetPath}"
                    }, Formatting.Indented);
                }

                var info = GetAssetInformation(assetPath, request);

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    assetInfo = info
                }, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.ToString()
                }, Formatting.Indented);
            }
        }

        private AssetInfo GetAssetInformation(string assetPath, AssetInfoRequest request)
        {
            var isFolder = AssetDatabase.IsValidFolder(assetPath);
            var asset = isFolder ? null : AssetDatabase.LoadMainAssetAtPath(assetPath);
            var guid = AssetDatabase.AssetPathToGUID(assetPath);

            long fileSize = 0;
            string lastModified = "Unknown";
            try
            {
                var abs = ToAbsolutePath(assetPath);
                if (isFolder)
                {
                    if (Directory.Exists(abs))
                        lastModified = Directory.GetLastWriteTime(abs).ToString("yyyy-MM-dd HH:mm:ss");
                }
                else
                {
                    var fi = new FileInfo(abs);
                    if (fi.Exists)
                    {
                        fileSize = fi.Length;
                        lastModified = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                }
            }
            catch { }

            var info = new AssetInfo
            {
                path = assetPath,
                name = isFolder ? Path.GetFileName(assetPath.TrimEnd('/')) : asset?.name ?? Path.GetFileNameWithoutExtension(assetPath),
                type = isFolder ? "Folder" : asset?.GetType().Name ?? "Unknown",
                typeFullName = isFolder ? "Folder" : asset?.GetType().FullName ?? "Unknown",
                guid = guid,
                isFolder = isFolder,

                fileSize = fileSize,
                lastModified = lastModified,

                labels = AssetDatabase.GetLabels(AssetDatabase.LoadMainAssetAtPath(assetPath)) ?? Array.Empty<string>(),
                subAssets = new List<SubAssetInfo>(),
                dependencies = Array.Empty<string>(),

                properties = new List<AssetProperty>(),
                importSettings = new Dictionary<string, object>()
            };

            if (request.includeSubAssets && !isFolder)
            {
                var subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
                foreach (var sa in subAssets)
                {
                    if (sa == null) continue;
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sa, out var _, out long localId);
                    info.subAssets.Add(new SubAssetInfo
                    {
                        name = sa.name,
                        type = sa.GetType().Name,
                        localId = localId
                    });
                }
            }

            if (request.includeDependencies)
            {
                var deps = AssetDatabase.GetDependencies(assetPath, true)
                    .Where(p => !string.Equals(p, assetPath, StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .ToArray();
                info.dependencies = deps;
            }

            if (request.includeSerializedProperties && !isFolder && asset != null)
            {
                var so = new SerializedObject(asset);
                info.properties = ReadSerializedObject(so, request.flattenPropertyPaths);
            }

            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer != null)
            {
                info.importSettings["importerType"] = importer.GetType().FullName;
                info.importSettings["assetBundleName"] = importer.assetBundleName;
                info.importSettings["assetBundleVariant"] = importer.assetBundleVariant;
                info.importSettings["userData"] = importer.userData;

                if (request.includeImporterSerialized)
                {
                    var iso = new SerializedObject(importer);
                    var importerProps = ReadSerializedObject(iso, request.flattenPropertyPaths);
                    info.importSettings["serializedProperties"] = importerProps;
                }
            }

            return info;
        }

        private List<AssetProperty> ReadSerializedObject(SerializedObject so, bool flatten)
        {
            var list = new List<AssetProperty>();
            try
            {
                var iterator = so.GetIterator();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    if (iterator.propertyType == SerializedPropertyType.ArraySize) continue;

                    var ap = new AssetProperty
                    {
                        name = iterator.name,
                        displayName = iterator.displayName,
                        type = iterator.propertyType.ToString(),
                        path = iterator.propertyPath,
                        isEditable = iterator.editable,
                        description = iterator.tooltip
                    };

                    ap.value = ReadPropertyValue(iterator, flatten);
                    list.Add(ap);
                }
            }
            catch (Exception ex)
            {
                list.Add(new AssetProperty
                {
                    name = "_error",
                    displayName = "Error",
                    type = "Exception",
                    path = "",
                    isEditable = false,
                    description = "Failed to read serialized object",
                    value = ex.Message
                });
            }
            return list;
        }

        private object ReadPropertyValue(SerializedProperty p, bool flatten)
        {
            try
            {
                if (p.isArray && p.propertyType != SerializedPropertyType.String)
                {
                    int size = p.arraySize;
                    var arr = new List<object>(size);
                    for (int i = 0; i < size; i++)
                    {
                        var el = p.GetArrayElementAtIndex(i);
                        arr.Add(ReadPropertyValue(el, flatten));
                    }
                    return arr;
                }

                switch (p.propertyType)
                {
                    case SerializedPropertyType.Integer: return p.intValue;
                    case SerializedPropertyType.Boolean: return p.boolValue;
                    case SerializedPropertyType.Float: return p.floatValue;
                    case SerializedPropertyType.String: return p.stringValue;
                    case SerializedPropertyType.LayerMask: return p.intValue;

                    case SerializedPropertyType.Color:
                    {
                        var c = p.colorValue;
                        return new { r = c.r, g = c.g, b = c.b, a = c.a };
                    }

                    case SerializedPropertyType.ObjectReference:
                    {
                        var o = p.objectReferenceValue;
                        return ObjectRefToDto(o);
                    }

                    case SerializedPropertyType.Enum:
                    {
                        var names = p.enumDisplayNames;
                        var idx = p.enumValueIndex;
                        string name = (idx >= 0 && idx < names.Length) ? names[idx] : idx.ToString();
                        return new { index = idx, name };
                    }

                    case SerializedPropertyType.Vector2:
                        return new { x = p.vector2Value.x, y = p.vector2Value.y };
                    case SerializedPropertyType.Vector3:
                        return new { x = p.vector3Value.x, y = p.vector3Value.y, z = p.vector3Value.z };
                    case SerializedPropertyType.Vector4:
                        return new { x = p.vector4Value.x, y = p.vector4Value.y, z = p.vector4Value.z, w = p.vector4Value.w };

                    case SerializedPropertyType.Rect:
                    {
                        var r = p.rectValue;
                        return new { x = r.x, y = r.y, width = r.width, height = r.height };
                    }

                    case SerializedPropertyType.Bounds:
                    {
                        var b = p.boundsValue;
                        return new
                        {
                            center = new { x = b.center.x, y = b.center.y, z = b.center.z },
                            size = new { x = b.size.x, y = b.size.y, z = b.size.z }
                        };
                    }

                    case SerializedPropertyType.Quaternion:
                    {
                        var q = p.quaternionValue;
                        return new { x = q.x, y = q.y, z = q.z, w = q.w };
                    }

                    case SerializedPropertyType.AnimationCurve:
                    {
                        if (!flatten)
                            return "AnimationCurve";

                        var curve = p.animationCurveValue;
                        if (curve == null) return null;
                        var keys = curve.keys.Select(k => new
                        {
                            time = k.time,
                            value = k.value,
                            inTangent = k.inTangent,
                            outTangent = k.outTangent,
                            // ИСПРАВЛЕНО: удален tangentMode
                            weightedMode = k.weightedMode.ToString()
                        }).ToArray();
                        return new { keys, preWrapMode = curve.preWrapMode.ToString(), postWrapMode = curve.postWrapMode.ToString() };
                    }

                    case SerializedPropertyType.Gradient:
                        return "Gradient";

                    case SerializedPropertyType.Character:
                        return (int)p.intValue;

                    case SerializedPropertyType.ExposedReference:
                        return ObjectRefToDto(p.exposedReferenceValue);

                    case SerializedPropertyType.FixedBufferSize:
                        return p.fixedBufferSize;

#if UNITY_2019_1_OR_NEWER
                    case SerializedPropertyType.Vector2Int:
                        return new { x = p.vector2IntValue.x, y = p.vector2IntValue.y };
                    case SerializedPropertyType.Vector3Int:
                        return new { x = p.vector3IntValue.x, y = p.vector3IntValue.y, z = p.vector3IntValue.z };
                    case SerializedPropertyType.RectInt:
                    {
                        var r = p.rectIntValue;
                        return new { x = r.x, y = r.y, width = r.width, height = r.height };
                    }
                    case SerializedPropertyType.BoundsInt:
                    {
                        var b = p.boundsIntValue;
                        return new
                        {
                            position = new { x = b.position.x, y = b.position.y, z = b.position.z },
                            size = new { x = b.size.x, y = b.size.y, z = b.size.z }
                        };
                    }
#endif

#if UNITY_2020_1_OR_NEWER
                    case SerializedPropertyType.ManagedReference:
                        return new
                        {
                            type = p.managedReferenceFullTypename,
                            id = p.managedReferenceId
                        };
#endif

                    case SerializedPropertyType.Generic:
                        return flatten ? TryFlattenGeneric(p) : "Generic";
                }
            }
            catch
            {
                // ignore
            }

            return "Complex Type";
        }

        private object TryFlattenGeneric(SerializedProperty genericProp)
        {
            try
            {
                var copy = genericProp.Copy();
                var end = copy.GetEndProperty();
                var dict = new Dictionary<string, object>();
                bool enterChildren = true;
                int baseDepth = copy.depth;

                while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
                {
                    enterChildren = false;
                    if (copy.depth <= baseDepth) break;
                    if (copy.propertyType == SerializedPropertyType.ArraySize) continue;

                    dict[copy.name] = ReadPropertyValue(copy, true);
                }
                return dict;
            }
            catch
            {
                return "Generic";
            }
        }

        private object ObjectRefToDto(UnityEngine.Object o)
        {
            if (!o) return null;

            string path = null;
            string guid = null;
            long localId = 0;

            if (EditorUtility.IsPersistent(o))
            {
                path = AssetDatabase.GetAssetPath(o);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out guid, out localId);
            }

            return new
            {
                name = o.name,
                type = o.GetType().FullName,
                path,
                guid,
                localId
            };
        }

        private static string NormalizeAssetsPath(string p)
        {
            var path = (p ?? "").Replace('\\', '/').Trim();
            if (string.IsNullOrEmpty(path)) return "Assets";
            if (path == "Assets" || path.StartsWith("Assets/")) return path;
            return "Assets/" + path.TrimStart('/');
        }

        private static bool AssetExists(string assetsPath)
        {
            if (AssetDatabase.IsValidFolder(assetsPath)) return true;
            return AssetDatabase.LoadMainAssetAtPath(assetsPath) != null;
        }

        private static string ToAbsolutePath(string assetsPath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var abs = Path.GetFullPath(Path.Combine(projectRoot ?? "", assetsPath));
            return abs;
        }
    }
}