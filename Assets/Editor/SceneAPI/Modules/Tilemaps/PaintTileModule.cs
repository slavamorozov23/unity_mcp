using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Newtonsoft.Json;

namespace SceneAPI.Modules
{
    public class PaintTileModule
    {
        [Serializable]
        public class PaintRequest
        {
            public string tilemapName; // Имя Tilemap (слоя)
            public string tileName;    // Имя ассета тайла (без пути, поиск в Assets/Tiles по умолчанию)
            public int x;
            public int y;
            public bool ensureGrid = true; // Создать Grid/Tilemap, если нет
        }

        public string Execute(string requestBody)
        {
            try
            {
                var req = JsonConvert.DeserializeObject<PaintRequest>(requestBody);

                // 1. Находим или создаем Tilemap/Grid
                Tilemap tilemap = FindOrEnsureTilemap(req.tilemapName, req.ensureGrid);
                if (tilemap == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = $"Tilemap '{req.tilemapName}' not found and could not be created." });
                }

                // 2. Находим тайл (Asset)
                TileBase tileToPaint = null;
                if (!string.IsNullOrEmpty(req.tileName) && req.tileName.ToLower() != "null")
                {
                    tileToPaint = FindTileAsset(req.tileName);
                    if (tileToPaint == null)
                    {
                        return JsonConvert.SerializeObject(new { success = false, error = $"Tile asset '{req.tileName}' not found (checked Assets/Tiles and global search)." });
                    }
                }
                // Если tileName == null или "null", мы стираем тайл (ставим null)

                // 3. Рисуем
                Vector3Int pos = new Vector3Int(req.x, req.y, 0);
                tilemap.SetTile(pos, tileToPaint);
                
                // Обновляем редактор
                EditorUtility.SetDirty(tilemap);

                return JsonConvert.SerializeObject(new 
                { 
                    success = true, 
                    message = tileToPaint != null ? $"Painted {req.tileName} at ({req.x}, {req.y})" : $"Erased at ({req.x}, {req.y})",
                    tilemap = tilemap.name 
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }

        private Tilemap FindOrEnsureTilemap(string name, bool createIfMissing)
        {
            // Поиск по имени
            var go = GameObject.Find(name);
            if (go != null)
            {
                var tm = go.GetComponent<Tilemap>();
                if (tm != null) return tm;
            }

            if (!createIfMissing) return null;

            // Логика создания "Стандартной Сетки"
            Grid grid = UnityEngine.Object.FindObjectOfType<Grid>();
            if (grid == null)
            {
                GameObject gridObj = new GameObject("Grid");
                grid = gridObj.AddComponent<Grid>();
                // Важно: смещение по схеме для центрирования пиксель-арт тайлов
                gridObj.transform.position = new Vector3(-0.5f, -0.5f, 0); 
            }

            // Создаем новый слой (Tilemap)
            string tmName = string.IsNullOrEmpty(name) ? "Tilemap" : name;
            GameObject tmObj = new GameObject(tmName);
            tmObj.transform.SetParent(grid.transform);
            tmObj.transform.localPosition = Vector3.zero;
            
            var newTm = tmObj.AddComponent<Tilemap>();
            tmObj.AddComponent<TilemapRenderer>();
            
            return newTm;
        }

        private TileBase FindTileAsset(string name)
        {
            // 1. Пробуем найти в Assets/Tiles
            string specificPath = $"Assets/Tiles/{name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(specificPath);
            if (tile != null) return tile;

            // 2. Ищем везде по имени
            string[] guids = AssetDatabase.FindAssets($"{name} t:TileBase");
            if (guids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            return null;
        }
    }
}