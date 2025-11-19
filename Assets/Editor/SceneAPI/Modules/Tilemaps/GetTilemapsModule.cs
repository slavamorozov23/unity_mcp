using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Newtonsoft.Json;

namespace SceneAPI.Modules
{
    public class GetTilemapsModule
    {
        public string Execute(string requestBody)
        {
            try
            {
                var tilemaps = UnityEngine.Object.FindObjectsOfType<Tilemap>();
                var result = tilemaps.Select(tm => new
                {
                    name = tm.name,
                    path = GetGameObjectPath(tm.gameObject),
                    sortingLayer = UnityEngine.SortingLayer.IDToName(tm.GetComponent<TilemapRenderer>()?.sortingLayerID ?? 0),
                    orderInLayer = tm.GetComponent<TilemapRenderer>()?.sortingOrder ?? 0,
                    cellBounds = new 
                    { 
                        x = tm.cellBounds.x, 
                        y = tm.cellBounds.y, 
                        width = tm.cellBounds.size.x, 
                        height = tm.cellBounds.size.y 
                    },
                    grid = tm.layoutGrid?.name ?? "Unknown"
                }).OrderBy(t => t.sortingLayer).ThenBy(t => t.orderInLayer).ToList();

                return JsonConvert.SerializeObject(new { success = true, tilemaps = result }, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }

        private string GetGameObjectPath(GameObject go)
        {
            return go.transform.parent == null ? go.name : GetGameObjectPath(go.transform.parent.gameObject) + "/" + go.name;
        }
    }
}