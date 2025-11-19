import requests
import json
from typing import Dict, List

class TilemapSnapshotModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self, tilemap_name: str, overlay_object_paths: List[str], center_x: int, center_y: int, width: int = 20, height: int = 20) -> Dict:
        """
        Получает Markdown таблицу тайлмапа с наложением объектов.
        Unity проверит Z-координаты для выявления проблем отображения (!BEHIND!).
        """
        try:
            data = {
                "tilemapName": tilemap_name,
                "overlayObjectPaths": overlay_object_paths,
                "centerX": center_x,
                "centerY": center_y,
                "width": width,
                "height": height
            }
            
            response = requests.post(
                f"{self.base_url}/debug/tilemap/snapshot",
                json=data
            )
            response.raise_for_status()
            return response.json()
        except Exception as e:
            return {"success": False, "error": str(e)}