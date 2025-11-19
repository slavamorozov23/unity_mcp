using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Newtonsoft.Json;

namespace SceneAPI.Modules
{
    public class ManageTileAssetModule
    {
        [Serializable]
        public class ManageTileRequest
        {
            public string action; // "create" or "delete"
            public string tileName;
            public string spritePath; // Только для create: путь к спрайту (Assets/...)
            public string targetFolder = "Assets/Tiles";
        }

        public string Execute(string requestBody)
        {
            try
            {
                var req = JsonConvert.DeserializeObject<ManageTileRequest>(requestBody);
                
                if (req.action == "create")
                {
                    return CreateTile(req);
                }
                else if (req.action == "delete")
                {
                    return DeleteTile(req);
                }

                return JsonConvert.SerializeObject(new { success = false, error = "Unknown action (use 'create' or 'delete')" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }

        private string CreateTile(ManageTileRequest req)
        {
            if (string.IsNullOrEmpty(req.spritePath))
                return JsonConvert.SerializeObject(new { success = false, error = "spritePath is required" });

            // Создаем папку если нет
            if (!AssetDatabase.IsValidFolder(req.targetFolder))
            {
                Directory.CreateDirectory(req.targetFolder); // Опасно без Refresh, лучше через AssetDatabase
                // Упрощение: предполагаем, что папка Assets/Tiles существует или создаем через API
                if (!AssetDatabase.IsValidFolder("Assets/Tiles")) AssetDatabase.CreateFolder("Assets", "Tiles");
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(req.spritePath);
            if (sprite == null)
            {
                return JsonConvert.SerializeObject(new { success = false, error = $"Sprite not found at {req.spritePath}" });
            }

            string tilePath = $"{req.targetFolder}/{req.tileName}.asset";
            
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            
            AssetDatabase.CreateAsset(tile, tilePath);
            AssetDatabase.SaveAssets();

            return JsonConvert.SerializeObject(new 
            { 
                success = true, 
                path = tilePath,
                message = $"Tile asset created: {req.tileName}" 
            });
        }

        private string DeleteTile(ManageTileRequest req)
        {
            string path = $"{req.targetFolder}/{req.tileName}.asset";
            
            if (!File.Exists(path) && !File.Exists(path.Replace("Assets", Application.dataPath)))
            {
                // Попробуем найти
                string[] guids = AssetDatabase.FindAssets($"{req.tileName} t:TileBase");
                if (guids.Length > 0) path = AssetDatabase.GUIDToAssetPath(guids[0]);
                else return JsonConvert.SerializeObject(new { success = false, error = "Tile asset not found" });
            }

            bool deleted = AssetDatabase.DeleteAsset(path);
            return JsonConvert.SerializeObject(new 
            { 
                success = deleted, 
                message = deleted ? "Tile deleted" : "Failed to delete tile" 
            });
        }
    }
}