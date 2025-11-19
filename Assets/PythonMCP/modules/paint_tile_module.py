import requests
import json
from typing import Dict

class PaintTileModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self, tilemap_name: str, tile_name: str, x: int, y: int) -> Dict:
        """Рисует тайл на сетке. Если tile_name=None, стирает."""
        try:
            data = {
                "tilemapName": tilemap_name,
                "tileName": tile_name,
                "x": x,
                "y": y,
                "ensureGrid": True
            }
            response = requests.post(f"{self.base_url}/tilemaps/paint", json=data)
            response.raise_for_status()
            return response.json()
        except Exception as e:
            return {"success": False, "error": str(e)}