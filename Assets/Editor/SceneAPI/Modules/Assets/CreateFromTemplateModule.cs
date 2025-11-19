using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

namespace SceneAPI.Modules
{
    public class CreateFromTemplateModule
    {
        [Serializable]
        public class CreateFromTemplateRequest
        {
            // Может быть как "Material", так и "Shader/Unlit Shader" или "My Tools/My Custom Asset"
            public string templateName;
            // Папка или полный путь. Примеры: "Assets/MyFolder", "Assets/MyFolder/MyFile", "MyFolder"
            public string targetPath;
            // Необязательно. Если задано — ассет будет переименован после создания (расширение сохраняется)
            public string fileName;
        }

        public string Execute(string requestBody)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<CreateFromTemplateRequest>(requestBody) ?? new CreateFromTemplateRequest();

                if (string.IsNullOrWhiteSpace(request.templateName))
                    return JsonConvert.SerializeObject(new { success = false, error = "templateName is required" }, Formatting.Indented);

                if (string.IsNullOrWhiteSpace(request.targetPath))
                    return JsonConvert.SerializeObject(new { success = false, error = "targetPath is required" }, Formatting.Indented);

                var normalizedTarget = NormalizeAssetsPath(request.targetPath);
                string folderPath = normalizedTarget;
                string desiredFileName = string.IsNullOrWhiteSpace(request.fileName) ? null : request.fileName;

                // Если targetPath указывает на файл — извлекаем папку и имя
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    var dir = Path.GetDirectoryName(normalizedTarget)?.Replace('\\', '/') ?? "Assets";
                    folderPath = string.IsNullOrEmpty(dir) ? "Assets" : dir;
                    if (string.IsNullOrEmpty(desiredFileName))
                    {
                        var nameCandidate = Path.GetFileNameWithoutExtension(normalizedTarget);
                        if (!string.IsNullOrEmpty(nameCandidate))
                            desiredFileName = nameCandidate;
                    }
                }

                EnsureFolderExists(folderPath);

                string usedMenuPath;
                string createdPath = TryCreateViaAssetsCreateMenu(request.templateName, folderPath, desiredFileName, out usedMenuPath);

                bool usedAttrFallback = false;
                if (string.IsNullOrEmpty(createdPath))
                {
                    // Fallback для ScriptableObject с [CreateAssetMenu]
                    createdPath = TryCreateViaCreateAssetMenuAttribute(request.templateName, folderPath, desiredFileName);
                    usedAttrFallback = !string.IsNullOrEmpty(createdPath);
                }

                if (string.IsNullOrEmpty(createdPath))
                {
                    var menuPaths = GetCreateMenuItems();
                    var suggestions = menuPaths
                        .Where(m => m.IndexOf(request.templateName, StringComparison.OrdinalIgnoreCase) >= 0)
                        .Select(m => m.Replace("Assets/Create/", ""))
                        .Distinct()
                        .Take(10)
                        .ToArray();

                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"Template '{request.templateName}' was not found or failed to create.",
                        suggestions
                    }, Formatting.Indented);
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    template = request.templateName,
                    createdPath,
                    folderPath,
                    usedMenuPath,
                    usedFallback = usedAttrFallback ? "CreateAssetMenu" : "Assets/Create",
                    fileName = Path.GetFileNameWithoutExtension(createdPath)
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

        // ---------- Core: вызов реального меню Assets/Create ----------

        private static string TryCreateViaAssetsCreateMenu(string template, string folderPath, string desiredFileName, out string usedMenuPath)
        {
            usedMenuPath = null;
            try
            {
                var menuItems = GetCreateMenuItems();
                usedMenuPath = ResolveCreateMenuPath(template, menuItems);
                if (string.IsNullOrEmpty(usedMenuPath))
                    return null;

                // Выделяем папку, чтобы Unity создал ассет именно там
                var folderObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folderPath);
                if (folderObj != null)
                    Selection.activeObject = folderObj;
                else
                    Selection.activeObject = null;

                // Зафиксируем, что было до
                var before = new HashSet<string>(AssetDatabase.FindAssets("", new[] { folderPath }));

                // Триггерим настоящий пункт меню
                bool executed = EditorApplication.ExecuteMenuItem(usedMenuPath);
                if (!executed)
                    return null;

                // Ждём появления нового ассета в целевой папке (обычно синхронно)
                var createdPath = WaitForNewAssetInFolder(folderPath, before, 2.0);
                if (string.IsNullOrEmpty(createdPath))
                    return null;

                // Если нужно — переименуем (расширение сохранится)
                if (!string.IsNullOrEmpty(desiredFileName))
                {
                    var cleanName = SanitizeFileNameWithoutExtension(desiredFileName);
                    var error = AssetDatabase.RenameAsset(createdPath, cleanName);
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogWarning($"RenameAsset error: {error}. Keeping original name: {Path.GetFileName(createdPath)}");
                    }
                    else
                    {
                        var dir = Path.GetDirectoryName(createdPath)?.Replace('\\', '/') ?? folderPath;
                        var ext = Path.GetExtension(createdPath);
                        createdPath = (string.IsNullOrEmpty(dir) ? "" : dir + "/") + cleanName + ext;
                    }
                }

                return createdPath;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"TryCreateViaAssetsCreateMenu failed: {ex.Message}");
                return null;
            }
        }

        // ---------- Fallback: ScriptableObject с [CreateAssetMenu] ----------

        private static string TryCreateViaCreateAssetMenuAttribute(string template, string folderPath, string desiredFileName)
        {
            try
            {
                var types = TypeCache.GetTypesWithAttribute<CreateAssetMenuAttribute>();
                if (types == null || types.Count == 0) return null;

                string normalized = (template ?? "").Trim().TrimStart('/');

                Type matchedType = null;
                CreateAssetMenuAttribute matchedAttr = null;

                foreach (var type in types)
                {
                    var attrs = type.GetCustomAttributes(typeof(CreateAssetMenuAttribute), false) as CreateAssetMenuAttribute[];
                    if (attrs == null || attrs.Length == 0) continue;
                    var attr = attrs[0];
                    var menuName = attr.menuName ?? "";

                    bool isMatch =
                        string.Equals(menuName, normalized, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(menuName.TrimStart('/'), normalized, StringComparison.OrdinalIgnoreCase) ||
                        menuName.EndsWith("/" + normalized, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(menuName.Split('/').Last(), normalized.Split('/').Last(), StringComparison.OrdinalIgnoreCase);

                    if (isMatch)
                    {
                        matchedType = type;
                        matchedAttr = attr;
                        break;
                    }
                }

                if (matchedType == null) return null;

                var instance = ScriptableObject.CreateInstance(matchedType);
                if (instance == null) return null;

                var baseName = !string.IsNullOrEmpty(desiredFileName)
                    ? SanitizeFileNameWithoutExtension(desiredFileName)
                    : (!string.IsNullOrEmpty(matchedAttr.fileName) ? SanitizeFileNameWithoutExtension(matchedAttr.fileName) : matchedType.Name);

                var path = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{baseName}.asset");
                AssetDatabase.CreateAsset(instance, path);
                AssetDatabase.SaveAssets();

                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"TryCreateViaCreateAssetMenuAttribute failed: {ex.Message}");
                return null;
            }
        }

        // ---------- Utils ----------

        private static string NormalizeAssetsPath(string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return "Assets";
            var path = p.Replace('\\', '/').Trim();
            if (path == "Assets" || path.StartsWith("Assets/")) return path.TrimEnd('/');
            return "Assets/" + path.TrimStart('/').TrimEnd('/');
        }

        private static void EnsureFolderExists(string assetsFolderPath)
        {
            assetsFolderPath = assetsFolderPath.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(assetsFolderPath)) assetsFolderPath = "Assets";
            if (assetsFolderPath == "Assets") return;

            if (AssetDatabase.IsValidFolder(assetsFolderPath)) return;

            var segs = assetsFolderPath.Split('/');
            var curr = "Assets";
            for (int i = 1; i < segs.Length; i++)
            {
                var next = curr + "/" + segs[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(curr, segs[i]);
                }
                curr = next;
            }
        }

        private static List<string> GetCreateMenuItems()
        {
            var list = new List<string>();
            try
            {
                // Используем внутренний Unsupported.GetSubmenus("Assets/Create") через рефлексию
                var unsupported = typeof(Editor).Assembly.GetType("UnityEditor.Unsupported");
                var mi = unsupported?.GetMethod("GetSubmenus", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null)
                {
                    var arr = mi.Invoke(null, new object[] { "Assets/Create" }) as string[];
                    if (arr != null) list.AddRange(arr);
                }
            }
            catch { /* no-op */ }
            return list;
        }

        private static string ResolveCreateMenuPath(string template, IReadOnlyList<string> menuItems)
        {
            if (string.IsNullOrWhiteSpace(template) || menuItems == null || menuItems.Count == 0) return null;

            string normalized = template.Trim().TrimStart('/');
            if (normalized.StartsWith("Assets/Create/", StringComparison.OrdinalIgnoreCase))
            {
                return menuItems.FirstOrDefault(m => string.Equals(m, normalized, StringComparison.OrdinalIgnoreCase));
            }

            var candidate = "Assets/Create/" + normalized;

            var exact = menuItems.FirstOrDefault(m => string.Equals(m, candidate, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            string last = normalized.Contains("/") ? normalized.Split('/').Last() : normalized;

            var byLast = menuItems.FirstOrDefault(m => string.Equals(m.Split('/').Last(), last, StringComparison.OrdinalIgnoreCase));
            if (byLast != null) return byLast;

            var contains = menuItems.FirstOrDefault(m => m.EndsWith(last, StringComparison.OrdinalIgnoreCase));
            return contains;
        }

        private static string WaitForNewAssetInFolder(string folderPath, HashSet<string> beforeSet, double timeoutSeconds)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < timeoutSeconds)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var after = AssetDatabase.FindAssets("", new[] { folderPath });
                foreach (var guid in after)
                {
                    if (!beforeSet.Contains(guid))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!string.IsNullOrEmpty(path))
                            return path;
                    }
                }
                Thread.Sleep(30);
            }
            return null;
        }

        private static string SanitizeFileNameWithoutExtension(string name)
        {
            var n = Path.GetFileNameWithoutExtension(name ?? "NewAsset");
            foreach (var c in Path.GetInvalidFileNameChars())
                n = n.Replace(c, '_');
            return string.IsNullOrWhiteSpace(n) ? "NewAsset" : n;
        }

        public string ExecuteTest()
        {
            var testRequest = new CreateFromTemplateRequest
            {
                templateName = "Material",
                targetPath = "Assets/Mod Assets/Custom Materials",
                fileName = "TestMaterial"
            };
            var json = JsonConvert.SerializeObject(testRequest);
            return Execute(json);
        }
    }
}