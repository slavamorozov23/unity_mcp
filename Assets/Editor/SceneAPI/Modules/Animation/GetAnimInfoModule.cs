using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

namespace SceneAPI.Modules
{
    public class GetAnimInfoModule
    {
        [Serializable]
        public class AnimInfoRequest
        {
            public string animPath;
            public string query;
        }

        public string Execute(string requestBody)
        {
            try
            {
                var req = JsonConvert.DeserializeObject<AnimInfoRequest>(requestBody);
                if (string.IsNullOrEmpty(req.animPath))
                    return JsonConvert.SerializeObject(new { success = false, error = "animPath is required" });

                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(req.animPath);
                if (clip == null)
                    return JsonConvert.SerializeObject(new { success = false, error = "Animation clip not found" });

                var bindings = AnimationUtility.GetCurveBindings(clip);
                
                // Структура для таблицы: Time -> { PropertyName: Value }
                // Используем SortedDictionary для сортировки по времени
                var timeline = new SortedDictionary<float, Dictionary<string, float>>();
                
                // Список всех свойств для заголовков
                var allProperties = new HashSet<string>();

                foreach (var binding in bindings)
                {
                    // Фильтрация по имени свойства или пути объекта
                    if (!string.IsNullOrEmpty(req.query))
                    {
                        string fullName = string.IsNullOrEmpty(binding.path) ? binding.propertyName : $"{binding.path}/{binding.propertyName}";
                        if (!fullName.ToLower().Contains(req.query.ToLower()))
                            continue;
                    }

                    string propKey = string.IsNullOrEmpty(binding.path) 
                        ? binding.propertyName 
                        : $"{binding.path} : {binding.propertyName}";
                    
                    allProperties.Add(propKey);

                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    foreach(var key in curve.keys)
                    {
                        if (!timeline.ContainsKey(key.time))
                        {
                            timeline[key.time] = new Dictionary<string, float>();
                        }
                        // Округляем значение для читаемости
                        timeline[key.time][propKey] = (float)Math.Round(key.value, 3);
                    }
                }

                // Преобразуем в список для JSON сериализации
                var tableRows = timeline.Select(kvp => new 
                {
                    time = kvp.Key,
                    values = kvp.Value
                }).ToList();

                return JsonConvert.SerializeObject(new 
                { 
                    success = true, 
                    clipName = clip.name,
                    frameRate = clip.frameRate,
                    length = clip.length,
                    propertiesList = allProperties.ToList(), // Список колонок
                    timeline = tableRows                     // Данные (строки)
                }, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }
    }
}