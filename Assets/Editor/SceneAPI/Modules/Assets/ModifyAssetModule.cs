using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SceneAPI.Modules
{
    public class ModifyAssetModule
    {
        [Serializable]
        public class ModifyAssetRequest
        {
            public string assetPath;
            public Dictionary<string, object> properties;
            public Dictionary<string, object> importSettings;
        }

        [Serializable]
        public class ModificationResult
        {
            public string propertyName;
            public bool success;
            public string error;
            public object oldValue;
            public object newValue;
        }

        public string Execute(string requestBody)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<ModifyAssetRequest>(requestBody);

                if (string.IsNullOrEmpty(request.assetPath))
                {
                    var errorResponse = new
                    {
                        success = false,
                        error = "Asset path is required"
                    };
                    return JsonConvert.SerializeObject(errorResponse, Formatting.Indented);
                }

                // Убеждаемся, что путь начинается с Assets/
                if (!request.assetPath.StartsWith("Assets/"))
                {
                    request.assetPath = "Assets/" + request.assetPath.TrimStart('/');
                }

                // Проверяем существование ассета
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(request.assetPath);
                if (!asset)
                {
                    var notFoundResponse = new
                    {
                        success = false,
                        error = $"Asset not found at path: {request.assetPath}"
                    };
                    return JsonConvert.SerializeObject(notFoundResponse, Formatting.Indented);
                }

                var results = new List<ModificationResult>();

                // Изменяем свойства ассета
                if (request.properties != null && request.properties.Count > 0)
                {
                    var assetResults = ModifyAssetProperties(asset, request.properties);
                    results.AddRange(assetResults);
                }

                // Изменяем настройки импорта
                if (request.importSettings != null && request.importSettings.Count > 0)
                {
                    var importResults = ModifyImportSettings(request.assetPath, request.importSettings);
                    results.AddRange(importResults);
                }

                // Сохраняем изменения
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var response = new
                {
                    success = true,
                    assetPath = request.assetPath,
                    modifications = results,
                    totalModifications = results.Count,
                    successfulModifications = results.FindAll(r => r.success).Count
                };

                return JsonConvert.SerializeObject(response, Formatting.Indented);
            }
            catch (Exception ex)
            {
                var errorResponse = new
                {
                    success = false,
                    error = ex.Message
                };

                return JsonConvert.SerializeObject(errorResponse, Formatting.Indented);
            }
        }

        private List<ModificationResult> ModifyAssetProperties(UnityEngine.Object asset, Dictionary<string, object> properties)
        {
            var results = new List<ModificationResult>();
            var serializedObject = new SerializedObject(asset);

            foreach (var kvp in properties)
            {
                var result = new ModificationResult
                {
                    propertyName = kvp.Key,
                    success = false
                };

                try
                {
                    var property = serializedObject.FindProperty(kvp.Key);
                    if (property == null)
                    {
                        result.error = $"Property '{kvp.Key}' not found";
                        results.Add(result);
                        continue;
                    }

                    if (!property.editable)
                    {
                        result.error = $"Property '{kvp.Key}' is not editable";
                        results.Add(result);
                        continue;
                    }

                    // Сохраняем старое значение
                    result.oldValue = GetPropertyValue(property);

                    // Устанавливаем новое значение
                    bool setValue = SetPropertyValue(property, kvp.Value);

                    if (setValue)
                    {
                        result.success = true;
                        result.newValue = kvp.Value;
                        serializedObject.ApplyModifiedProperties();
                    }
                    else
                    {
                        result.error = $"Failed to set value for property '{kvp.Key}'";
                    }
                }
                catch (Exception ex)
                {
                    result.error = ex.Message;
                }

                results.Add(result);
            }

            return results;
        }

        private List<ModificationResult> ModifyImportSettings(string assetPath, Dictionary<string, object> importSettings)
        {
            var results = new List<ModificationResult>();
            var importer = AssetImporter.GetAtPath(assetPath);

            if (importer == null)
            {
                results.Add(new ModificationResult
                {
                    propertyName = "importer",
                    success = false,
                    error = "Asset importer not found"
                });
                return results;
            }

            foreach (var kvp in importSettings)
            {
                var result = new ModificationResult
                {
                    propertyName = kvp.Key,
                    success = false
                };

                try
                {
                    bool success = SetImportSetting(importer, kvp.Key, kvp.Value, result);
                    result.success = success;

                    if (success)
                    {
                        result.newValue = kvp.Value;
                    }
                }
                catch (Exception ex)
                {
                    result.error = ex.Message;
                }

                results.Add(result);
            }

            // Применяем изменения импорта
            if (results.Exists(r => r.success))
            {
                importer.SaveAndReimport();
            }

            return results;
        }

        private static readonly Dictionary<SerializedPropertyType, Func<SerializedProperty, object>> PropertyValueGetters = 
            new Dictionary<SerializedPropertyType, Func<SerializedProperty, object>>
            {
                { SerializedPropertyType.Integer, p => p.intValue },
                { SerializedPropertyType.Boolean, p => p.boolValue },
                { SerializedPropertyType.Float, p => p.floatValue },
                { SerializedPropertyType.String, p => p.stringValue },
                { SerializedPropertyType.Color, p => p.colorValue },
                { SerializedPropertyType.ObjectReference, p => p.objectReferenceValue },
                { SerializedPropertyType.LayerMask, p => p.intValue },
                { SerializedPropertyType.Enum, p => p.enumValueIndex },
                { SerializedPropertyType.Vector2, p => p.vector2Value },
                { SerializedPropertyType.Vector3, p => p.vector3Value },
                { SerializedPropertyType.Vector4, p => p.vector4Value },
                { SerializedPropertyType.Rect, p => p.rectValue },
                { SerializedPropertyType.Bounds, p => p.boundsValue }
            };

        private object GetPropertyValue(SerializedProperty property)
        {
            return PropertyValueGetters.TryGetValue(property.propertyType, out var getter) 
                ? getter(property) 
                : "Complex Type";
        }

        private static readonly Dictionary<SerializedPropertyType, Func<SerializedProperty, object, bool>> PropertyValueSetters = 
            new Dictionary<SerializedPropertyType, Func<SerializedProperty, object, bool>>
            {
                { SerializedPropertyType.Integer, (p, v) => { p.intValue = Convert.ToInt32(v); return true; } },
                { SerializedPropertyType.Boolean, (p, v) => { p.boolValue = Convert.ToBoolean(v); return true; } },
                { SerializedPropertyType.Float, (p, v) => { p.floatValue = Convert.ToSingle(v); return true; } },
                { SerializedPropertyType.String, (p, v) => { p.stringValue = v.ToString(); return true; } },
                { SerializedPropertyType.LayerMask, (p, v) => { p.intValue = Convert.ToInt32(v); return true; } },
                { SerializedPropertyType.Enum, (p, v) => { p.enumValueIndex = Convert.ToInt32(v); return true; } },
                { SerializedPropertyType.Color, SetColorValue },
                { SerializedPropertyType.Vector2, SetVector2Value },
                { SerializedPropertyType.Vector3, SetVector3Value },
                { SerializedPropertyType.Vector4, SetVector4Value }
            };

        private bool SetPropertyValue(SerializedProperty property, object value)
        {
            try
            {
                return PropertyValueSetters.TryGetValue(property.propertyType, out var setter) 
                    && setter(property, value);
            }
            catch
            {
                return false;
            }
        }
        
        private static bool SetColorValue(SerializedProperty property, object value)
        {
            if (value is JObject colorObj)
            {
                var r = colorObj["r"]?.ToObject<float>() ?? 0f;
                var g = colorObj["g"]?.ToObject<float>() ?? 0f;
                var b = colorObj["b"]?.ToObject<float>() ?? 0f;
                var a = colorObj["a"]?.ToObject<float>() ?? 1f;
                property.colorValue = new Color(r, g, b, a);
                return true;
            }
            return false;
        }
        
        private static bool SetVector2Value(SerializedProperty property, object value)
        {
            if (value is JObject v2Obj)
            {
                var x = v2Obj["x"]?.ToObject<float>() ?? 0f;
                var y = v2Obj["y"]?.ToObject<float>() ?? 0f;
                property.vector2Value = new Vector2(x, y);
                return true;
            }
            return false;
        }
        
        private static bool SetVector3Value(SerializedProperty property, object value)
        {
            if (value is JObject v3Obj)
            {
                var x = v3Obj["x"]?.ToObject<float>() ?? 0f;
                var y = v3Obj["y"]?.ToObject<float>() ?? 0f;
                var z = v3Obj["z"]?.ToObject<float>() ?? 0f;
                property.vector3Value = new Vector3(x, y, z);
                return true;
            }
            return false;
        }
        
        private static bool SetVector4Value(SerializedProperty property, object value)
        {
            if (value is JObject v4Obj)
            {
                var x = v4Obj["x"]?.ToObject<float>() ?? 0f;
                var y = v4Obj["y"]?.ToObject<float>() ?? 0f;
                var z = v4Obj["z"]?.ToObject<float>() ?? 0f;
                var w = v4Obj["w"]?.ToObject<float>() ?? 0f;
                property.vector4Value = new Vector4(x, y, z, w);
                return true;
            }
            return false;
        }

        private bool SetImportSetting(AssetImporter importer, string settingName, object value, ModificationResult result)
        {
            try
            {
                // Используем рефлексию для универсального доступа к свойствам импортера
                return SetImportSettingByReflection(importer, settingName, value, result);
            }
            catch (Exception ex)
            {
                result.error = ex.Message;
                return false;
            }
        }
        
        private bool SetImportSettingByReflection(AssetImporter importer, string settingName, object value, ModificationResult result)
        {
            try
            {
                var importerType = importer.GetType();
                var propertyInfo = importerType.GetProperty(settingName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                
                if (propertyInfo != null && propertyInfo.CanWrite)
                {
                    // Сохраняем старое значение
                    result.oldValue = propertyInfo.GetValue(importer);
                    
                    // Конвертируем значение к нужному типу
                    var convertedValue = ConvertValueToPropertyType(value, propertyInfo.PropertyType);
                    
                    if (convertedValue != null)
                    {
                        propertyInfo.SetValue(importer, convertedValue);
                        return true;
                    }
                    else
                    {
                        result.error = $"Cannot convert value '{value}' to type {propertyInfo.PropertyType.Name}";
                        return false;
                    }
                }
                else
                {
                    result.error = $"Property '{settingName}' not found or not writable on {importerType.Name}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                result.error = $"Reflection error: {ex.Message}";
                return false;
            }
        }
        
        private object ConvertValueToPropertyType(object value, Type targetType)
        {
            try
            {
                if (value == null) return null;
                
                // Если типы совпадают, возвращаем как есть
                if (targetType.IsAssignableFrom(value.GetType()))
                    return value;
                
                // Обработка enum типов
                if (targetType.IsEnum)
                {
                    if (value is string stringValue)
                        return Enum.Parse(targetType, stringValue, true);
                    else
                        return Enum.ToObject(targetType, value);
                }
                
                // Обработка nullable типов
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    var underlyingType = Nullable.GetUnderlyingType(targetType);
                    return ConvertValueToPropertyType(value, underlyingType);
                }
                
                // Стандартное преобразование типов
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return null;
            }
        }

        // Все специфичные методы для импортеров удалены - используется универсальный метод с рефлексией
    }
}