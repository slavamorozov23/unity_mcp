using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;

namespace SceneAPI.Modules
{
    public class TilemapSnapshotModule
    {
        [Serializable]
        public class TilemapSnapshotRequest
        {
            public string tilemapName;
            public string[] overlayObjectPaths; 
            public int centerX;
            public int centerY;
            public int width = 20;
            public int height = 20;
        }

        private class OverlayInfo
        {
            public string Name;
            public Bounds Bounds;
            public float Z;
        }

        public string Execute(string requestBody)
        {
            try
            {
                var req = JsonConvert.DeserializeObject<TilemapSnapshotRequest>(requestBody);
                
                // Sync Physics2D to ensure recent transforms are updated for intersection checks
                Physics2D.SyncTransforms();

                Tilemap tilemap = null;
                if (!string.IsNullOrEmpty(req.tilemapName))
                {
                    var go = GameObject.Find(req.tilemapName);
                    if (go != null) tilemap = go.GetComponent<Tilemap>();
                }
                else
                {
                    tilemap = UnityEngine.Object.FindObjectOfType<Tilemap>();
                }

                if (tilemap == null) return JsonConvert.SerializeObject(new { success = false, error = "Tilemap not found" });

                List<OverlayInfo> overlays = new List<OverlayInfo>();
                if (req.overlayObjectPaths != null)
                {
                    foreach(var path in req.overlayObjectPaths)
                    {
                        var go = GameObjectUtilities.FindGameObjectByPath(path);
                        if (go != null)
                        {
                            Bounds b = new Bounds(go.transform.position, Vector3.zero); 
                            var rend = go.GetComponent<Renderer>();
                            var col = go.GetComponent<Collider2D>();
                            
                            if (rend != null) b = rend.bounds;
                            else if (col != null) b = col.bounds;

                            overlays.Add(new OverlayInfo 
                            { 
                                Name = go.name, 
                                Bounds = b,
                                Z = go.transform.position.z 
                            });
                        }
                    }
                }

                StringBuilder mdTable = new StringBuilder();
                
                mdTable.Append("| Y \\ X |");
                int startX = req.centerX - req.width / 2;
                int endX = req.centerX + req.width / 2;
                int startY = req.centerY - req.height / 2;
                int endY = req.centerY + req.height / 2;

                for (int x = startX; x <= endX; x++) mdTable.Append($" {x} |");
                mdTable.Append("\n|");
                mdTable.Append("---|".PadRight((endX - startX + 2) * 4, '-')); 
                mdTable.Append("\n");

                float gridZ = tilemap.layoutGrid != null ? tilemap.layoutGrid.transform.position.z : 0;

                for (int y = endY; y >= startY; y--)
                {
                    mdTable.Append($"| **{y}** |");
                    for (int x = startX; x <= endX; x++)
                    {
                        Vector3Int pos = new Vector3Int(x, y, 0);
                        TileBase tile = tilemap.GetTile(pos);
                        
                        List<string> cellContents = new List<string>();

                        if (tile != null)
                        {
                            cellContents.Add($"[{tile.name} Z:{gridZ:F1}]");
                        }

                        Vector3 cellCenter = tilemap.GetCellCenterWorld(pos);
                        Vector3 cellSize = tilemap.cellSize;
                        Bounds cellBounds = new Bounds(cellCenter, cellSize); 
                        cellBounds.Expand(new Vector3(0, 0, 1000f)); // Expand Z to catch 2D intersections

                        foreach (var info in overlays)
                        {
                            if (cellBounds.Intersects(info.Bounds))
                            {
                                string zWarning = "";
                                // Check if object is visually behind the tilemap
                                if (info.Z > gridZ + 0.1f) zWarning = " (!BEHIND!)"; 
                                
                                cellContents.Add($"*{info.Name}{zWarning}*");
                            }
                        }

                        if (cellContents.Count == 0) mdTable.Append("  |");
                        else mdTable.Append($" {string.Join("<br>", cellContents)} |");
                    }
                    mdTable.Append("\n");
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    tilemapName = tilemap.name,
                    gridZ = gridZ,
                    markdown = mdTable.ToString()
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }
    }
}