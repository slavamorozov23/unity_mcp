using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

namespace SceneAPI.Modules
{
    public class GetCreationTemplatesModule
    {
        [Serializable]
        public class TemplateSearchRequest
        {
            public string query;
            public int maxResults = 10;
        }

        [Serializable]
        public class CreationTemplate
        {
            public string name;
            public string displayName;
            public string description;
            public string category;
            public string fileExtension;
            public bool isBuiltIn;
            public double score; // Для сортировки релевантности
        }

        public string Execute(string requestBody)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<TemplateSearchRequest>(requestBody);
                string query = request?.query ?? "";
                int maxResults = request?.maxResults > 0 ? request.maxResults : 10;

                // Получаем все, затем фильтруем
                var allTemplates = GetAvailableTemplates();
                var filtered = FilterTemplates(allTemplates, query, maxResults);

                var response = new
                {
                    success = true,
                    query = query,
                    templates = filtered,
                    totalFound = filtered.Count
                };

                return JsonConvert.SerializeObject(response, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message }, Formatting.Indented);
            }
        }

        private List<CreationTemplate> FilterTemplates(List<CreationTemplate> all, string query, int maxResults)
        {
            if (string.IsNullOrEmpty(query))
                return all.Take(maxResults).ToList();

            string q = query.ToLowerInvariant();
            
            // Простая оценка релевантности
            foreach (var t in all)
            {
                t.score = 0;
                string dn = t.displayName.ToLowerInvariant();
                string cat = t.category.ToLowerInvariant();

                if (dn.Equals(q)) t.score += 100; // Точное совпадение
                else if (dn.StartsWith(q)) t.score += 50; // Начало слова
                else if (dn.Contains(q)) t.score += 10; // Содержит слово
                
                if (cat.Contains(q)) t.score += 5; // Совпадение по категории
            }

            return all
                .Where(t => t.score > 0)
                .OrderByDescending(t => t.score)
                .Take(maxResults)
                .ToList();
        }

        private List<CreationTemplate> GetAvailableTemplates()
        {
            var templates = new List<CreationTemplate>();

            try
            {
                templates.AddRange(GetUnityBuiltInTemplates());
                templates.AddRange(GetScriptTemplates());
                templates.AddRange(GetAssetCreationTemplates());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error getting creation templates: {ex.Message}");
                templates.AddRange(GetFallbackTemplates());
            }

            return templates;
        }

        private List<CreationTemplate> GetUnityBuiltInTemplates()
        {
            var templates = new List<CreationTemplate>();
            // Рефлексия для получения меню Assets/Create
            var menuItems = typeof(Editor).Assembly.GetType("UnityEditor.Unsupported")
                ?.GetMethod("GetSubmenus", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(null, new object[] { "Assets/Create" }) as string[];

            if (menuItems != null)
            {
                foreach (var item in menuItems)
                {
                    // item пример: "C# Script", "Material", "Shader/Unlit Shader"
                    string name = item;
                    // Убираем "Assets/Create/" если оно там есть (обычно GetSubmenus возвращает чистые пути относительно корня)
                    
                    templates.Add(new CreationTemplate
                    {
                        name = name,
                        displayName = System.IO.Path.GetFileName(name), // Берем только имя файла для отображения
                        description = $"Create {name}",
                        category = GetCategoryFromName(name),
                        fileExtension = GetExtensionFromName(name),
                        isBuiltIn = true
                    });
                }
            }
            return templates;
        }

        private List<CreationTemplate> GetScriptTemplates()
        {
            var templates = new List<CreationTemplate>();
            // Простейшая эвристика для скриптов, если рефлексия выше не нашла
            // (Обычно Assets/Create/C# Script уже есть в списке выше)
            return templates; 
        }

        private List<CreationTemplate> GetAssetCreationTemplates()
        {
            // Дополнительные специфичные типы, если их нет в меню
            return new List<CreationTemplate>();
        }

        private List<CreationTemplate> GetFallbackTemplates()
        {
            return new List<CreationTemplate>
            {
                new CreationTemplate { name = "C# Script", displayName = "C# Script", category = "Scripts", fileExtension = ".cs", isBuiltIn = true },
                new CreationTemplate { name = "Material", displayName = "Material", category = "Rendering", fileExtension = ".mat", isBuiltIn = true },
                new CreationTemplate { name = "Folder", displayName = "Folder", category = "Organization", fileExtension = "", isBuiltIn = true }
            };
        }

        private string GetCategoryFromName(string name)
        {
            if (name.Contains("Script")) return "Scripts";
            if (name.Contains("Material") || name.Contains("Shader") || name.Contains("Texture")) return "Rendering";
            if (name.Contains("Animation") || name.Contains("Animator")) return "Animation";
            if (name.Contains("Audio")) return "Audio";
            if (name.Contains("Scene")) return "Scene";
            if (name.Contains("Folder")) return "Organization";
            return "Other";
        }

        private string GetExtensionFromName(string name)
        {
            if (name.Contains("C# Script")) return ".cs";
            if (name.Contains("Material")) return ".mat";
            if (name.Contains("Shader")) return ".shader";
            if (name.Contains("Scene")) return ".unity";
            return "";
        }
    }
}