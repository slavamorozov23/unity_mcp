using System;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

namespace SceneAPI.Modules
{
    public class ManageInputAxisModule
    {
        [Serializable]
        public class InputRequest
        {
            public string action; // "create", "delete", "list"
            public string name;
            public string negativeButton;
            public string positiveButton;
            public string altNegativeButton;
            public string altPositiveButton;
            public float gravity = 3;
            public float dead = 0.001f;
            public float sensitivity = 3;
            public bool snap = true;
            public bool invert = false;
            public int type = 0; // 0=KeyOrMouseButton, 1=MouseMovement, 2=JoystickAxis
            public int axis = 0;
            public int joyNum = 0;
        }

        public string Execute(string requestBody)
        {
            try
            {
                var req = JsonConvert.DeserializeObject<InputRequest>(requestBody);
                var inputManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0];
                SerializedObject obj = new SerializedObject(inputManager);
                SerializedProperty axesProperty = obj.FindProperty("m_Axes");

                if (req.action == "list")
                {
                    var list = new System.Collections.Generic.List<string>();
                    for (int i = 0; i < axesProperty.arraySize; i++)
                    {
                        list.Add(axesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue);
                    }
                    return JsonConvert.SerializeObject(new { success = true, axes = list });
                }
                else if (req.action == "delete")
                {
                    bool found = false;
                    // Идем с конца, чтобы удаление не ломало индексы
                    for (int i = axesProperty.arraySize - 1; i >= 0; i--)
                    {
                        SerializedProperty axis = axesProperty.GetArrayElementAtIndex(i);
                        if (axis.FindPropertyRelative("m_Name").stringValue == req.name)
                        {
                            axesProperty.DeleteArrayElementAtIndex(i);
                            found = true;
                        }
                    }
                    obj.ApplyModifiedProperties();
                    return JsonConvert.SerializeObject(new { success = found, message = found ? "Deleted" : "Not found" });
                }
                else if (req.action == "create")
                {
                    axesProperty.arraySize++;
                    obj.ApplyModifiedProperties(); // Применяем размер, чтобы получить новый элемент

                    SerializedProperty axis = axesProperty.GetArrayElementAtIndex(axesProperty.arraySize - 1);
                    axis.FindPropertyRelative("m_Name").stringValue = req.name;
                    axis.FindPropertyRelative("negativeButton").stringValue = req.negativeButton;
                    axis.FindPropertyRelative("positiveButton").stringValue = req.positiveButton;
                    axis.FindPropertyRelative("altNegativeButton").stringValue = req.altNegativeButton;
                    axis.FindPropertyRelative("altPositiveButton").stringValue = req.altPositiveButton;
                    axis.FindPropertyRelative("gravity").floatValue = req.gravity;
                    axis.FindPropertyRelative("dead").floatValue = req.dead;
                    axis.FindPropertyRelative("sensitivity").floatValue = req.sensitivity;
                    axis.FindPropertyRelative("snap").boolValue = req.snap;
                    axis.FindPropertyRelative("invert").boolValue = req.invert;
                    axis.FindPropertyRelative("type").intValue = req.type;
                    axis.FindPropertyRelative("axis").intValue = req.axis;
                    axis.FindPropertyRelative("joyNum").intValue = req.joyNum;

                    obj.ApplyModifiedProperties();
                    return JsonConvert.SerializeObject(new { success = true, message = "Created" });
                }

                return JsonConvert.SerializeObject(new { success = false, error = "Unknown action" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }
    }
}