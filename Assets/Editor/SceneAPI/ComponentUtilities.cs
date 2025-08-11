using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace SceneAPI
{
    public static class ComponentUtilities
    {
        public static object GetComponentProperties(Component component)
        {
            var properties = new Dictionary<string, object>();

            try
            {
                SerializedObject serializedObject = new SerializedObject(component);
                SerializedProperty property = serializedObject.GetIterator();

                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    if (property.name == "m_Script")
                        continue;

                    properties[property.name] = GetSerializedPropertyValue(property);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error getting properties for {component.GetType().Name}: {ex.Message}");
            }

            return properties;
        }

        public static void ModifyComponentProperties(Component component, dynamic properties)
        {
            SerializedObject serializedObject = new SerializedObject(component);

            foreach (var prop in properties)
            {
                string propertyName = prop.Name;
                var propertyValue = prop.Value;

                SerializedProperty serializedProperty = serializedObject.FindProperty(propertyName);
                if (serializedProperty != null)
                {
                    SetSerializedPropertyValue(serializedProperty, propertyValue);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static object GetSerializedPropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.intValue;
                case SerializedPropertyType.Boolean:
                    return property.boolValue;
                case SerializedPropertyType.Float:
                    return property.floatValue;
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Color:
                    return new { r = property.colorValue.r, g = property.colorValue.g, b = property.colorValue.b, a = property.colorValue.a };
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue != null ? property.objectReferenceValue.name : null;
                case SerializedPropertyType.LayerMask:
                    return property.intValue;
                case SerializedPropertyType.Enum:
                    return property.enumNames[property.enumValueIndex];
                case SerializedPropertyType.Vector2:
                    return new { x = property.vector2Value.x, y = property.vector2Value.y };
                case SerializedPropertyType.Vector3:
                    return new { x = property.vector3Value.x, y = property.vector3Value.y, z = property.vector3Value.z };
                case SerializedPropertyType.Vector4:
                    return new { x = property.vector4Value.x, y = property.vector4Value.y, z = property.vector4Value.z, w = property.vector4Value.w };
                case SerializedPropertyType.Rect:
                    return new { x = property.rectValue.x, y = property.rectValue.y, width = property.rectValue.width, height = property.rectValue.height };
                case SerializedPropertyType.ArraySize:
                    return property.intValue;
                case SerializedPropertyType.Character:
                    return (char)property.intValue;
                case SerializedPropertyType.AnimationCurve:
                    return property.animationCurveValue != null ? "AnimationCurve" : null;
                case SerializedPropertyType.Bounds:
                    return new
                    {
                        center = new { x = property.boundsValue.center.x, y = property.boundsValue.center.y, z = property.boundsValue.center.z },
                        size = new { x = property.boundsValue.size.x, y = property.boundsValue.size.y, z = property.boundsValue.size.z }
                    };
                case SerializedPropertyType.Gradient:
                    return "Gradient";
                case SerializedPropertyType.Quaternion:
                    return new { x = property.quaternionValue.x, y = property.quaternionValue.y, z = property.quaternionValue.z, w = property.quaternionValue.w };
                case SerializedPropertyType.Vector2Int:
                    return new { x = property.vector2IntValue.x, y = property.vector2IntValue.y };
                case SerializedPropertyType.Vector3Int:
                    return new { x = property.vector3IntValue.x, y = property.vector3IntValue.y, z = property.vector3IntValue.z };
                case SerializedPropertyType.RectInt:
                    return new { x = property.rectIntValue.x, y = property.rectIntValue.y, width = property.rectIntValue.width, height = property.rectIntValue.height };
                case SerializedPropertyType.BoundsInt:
                    return new
                    {
                        position = new { x = property.boundsIntValue.position.x, y = property.boundsIntValue.position.y, z = property.boundsIntValue.position.z },
                        size = new { x = property.boundsIntValue.size.x, y = property.boundsIntValue.size.y, z = property.boundsIntValue.size.z }
                    };
                default:
                    return property.propertyType.ToString();
            }
        }

        private static void SetSerializedPropertyValue(SerializedProperty property, object value)
        {
            try
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        property.intValue = Convert.ToInt32(value);
                        break;
                    case SerializedPropertyType.Boolean:
                        property.boolValue = Convert.ToBoolean(value);
                        break;
                    case SerializedPropertyType.Float:
                        property.floatValue = Convert.ToSingle(value);
                        break;
                    case SerializedPropertyType.String:
                        property.stringValue = value.ToString();
                        break;
                    case SerializedPropertyType.Color:
                        var colorData = JsonConvert.DeserializeObject<dynamic>(value.ToString());
                        property.colorValue = new Color((float)colorData.r, (float)colorData.g, (float)colorData.b, (float)colorData.a);
                        break;
                    case SerializedPropertyType.LayerMask:
                        property.intValue = Convert.ToInt32(value);
                        break;
                    case SerializedPropertyType.Enum:
                        if (value is string)
                        {
                            int enumIndex = Array.IndexOf(property.enumNames, value.ToString());
                            if (enumIndex >= 0)
                                property.enumValueIndex = enumIndex;
                        }
                        else
                        {
                            property.enumValueIndex = Convert.ToInt32(value);
                        }
                        break;
                    case SerializedPropertyType.Vector2:
                        var vec2Data = JsonConvert.DeserializeObject<dynamic>(value.ToString());
                        property.vector2Value = new Vector2((float)vec2Data.x, (float)vec2Data.y);
                        break;
                    case SerializedPropertyType.Vector3:
                        var vec3Data = JsonConvert.DeserializeObject<dynamic>(value.ToString());
                        property.vector3Value = new Vector3((float)vec3Data.x, (float)vec3Data.y, (float)vec3Data.z);
                        break;
                    case SerializedPropertyType.Vector4:
                        var vec4Data = JsonConvert.DeserializeObject<dynamic>(value.ToString());
                        property.vector4Value = new Vector4((float)vec4Data.x, (float)vec4Data.y, (float)vec4Data.z, (float)vec4Data.w);
                        break;
                    case SerializedPropertyType.Rect:
                        var rectData = JsonConvert.DeserializeObject<dynamic>(value.ToString());
                        property.rectValue = new Rect((float)rectData.x, (float)rectData.y, (float)rectData.width, (float)rectData.height);
                        break;
                    case SerializedPropertyType.Quaternion:
                        var quatData = JsonConvert.DeserializeObject<dynamic>(value.ToString());
                        property.quaternionValue = new Quaternion((float)quatData.x, (float)quatData.y, (float)quatData.z, (float)quatData.w);
                        break;
                    case SerializedPropertyType.Vector2Int:
                        var vec2IntData = JsonConvert.DeserializeObject<dynamic>(value.ToString());
                        property.vector2IntValue = new Vector2Int((int)vec2IntData.x, (int)vec2IntData.y);
                        break;
                    case SerializedPropertyType.Vector3Int:
                        var vec3IntData = JsonConvert.DeserializeObject<dynamic>(value.ToString());
                        property.vector3IntValue = new Vector3Int((int)vec3IntData.x, (int)vec3IntData.y, (int)vec3IntData.z);
                        break;
                    case SerializedPropertyType.RectInt:
                        var rectIntData = JsonConvert.DeserializeObject<dynamic>(value.ToString());
                        property.rectIntValue = new RectInt((int)rectIntData.x, (int)rectIntData.y, (int)rectIntData.width, (int)rectIntData.height);
                        break;
                    case SerializedPropertyType.BoundsInt:
                        var boundsIntData = JsonConvert.DeserializeObject<dynamic>(value.ToString());
                        property.boundsIntValue = new BoundsInt(
                            new Vector3Int((int)boundsIntData.position.x, (int)boundsIntData.position.y, (int)boundsIntData.position.z),
                            new Vector3Int((int)boundsIntData.size.x, (int)boundsIntData.size.y, (int)boundsIntData.size.z)
                        );
                        break;
                    case SerializedPropertyType.Bounds:
                        var boundsData = JsonConvert.DeserializeObject<dynamic>(value.ToString());
                        property.boundsValue = new Bounds(
                            new Vector3((float)boundsData.center.x, (float)boundsData.center.y, (float)boundsData.center.z),
                            new Vector3((float)boundsData.size.x, (float)boundsData.size.y, (float)boundsData.size.z)
                        );
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to set property {property.name}: {ex.Message}");
            }
        }
    }
}