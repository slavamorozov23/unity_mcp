using System;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

namespace SceneAPI.Modules
{
    public class ManageAnimPropertyModule
    {
        [Serializable]
        public class AnimPropertyRequest
        {
            public string action; // "add_key" or "remove_property"
            public string animPath;
            public string objectPath; // Путь к объекту внутри анимации (пусто для корня)
            public string componentType; // Transform, BoxCollider, etc.
            public string propertyName; // m_LocalPosition.x
            public float time;  // Для add_key
            public float value; // Для add_key
        }

        public string Execute(string requestBody)
        {
            try
            {
                var req = JsonConvert.DeserializeObject<AnimPropertyRequest>(requestBody);
                
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(req.animPath);
                if (clip == null) return JsonConvert.SerializeObject(new { success = false, error = "Clip not found" });

                // Пытаемся найти тип компонента
                Type type = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(req.componentType);
                    if (type == null) type = asm.GetType($"UnityEngine.{req.componentType}");
                    if (type != null) break;
                }

                if (type == null) return JsonConvert.SerializeObject(new { success = false, error = $"Type {req.componentType} not found" });

                var binding = new EditorCurveBinding
                {
                    path = req.objectPath,
                    type = type,
                    propertyName = req.propertyName
                };

                if (req.action == "remove_property")
                {
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                }
                else if (req.action == "add_key")
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null) curve = new AnimationCurve();

                    // Проверяем, есть ли ключ, если да - обновляем, нет - добавляем
                    bool keyExists = false;
                    for (int i = 0; i < curve.keys.Length; i++)
                    {
                        if (Mathf.Approximately(curve.keys[i].time, req.time))
                        {
                            Keyframe k = curve.keys[i];
                            k.value = req.value;
                            curve.MoveKey(i, k);
                            keyExists = true;
                            break;
                        }
                    }

                    if (!keyExists)
                    {
                        curve.AddKey(req.time, req.value);
                    }

                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                }
                else
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "Unknown action" });
                }

                AssetDatabase.SaveAssets();
                return JsonConvert.SerializeObject(new { success = true, message = "Animation modified" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }
    }
}