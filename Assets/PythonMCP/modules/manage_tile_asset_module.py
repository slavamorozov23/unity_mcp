import requests
import json
from typing import Dict

class ManageTileAssetModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def create_tile(self, tile_name: str, sprite_path: str) -> Dict:
        """Создает ассет тайла из спрайта"""
        return self._execute("create", tile_name, sprite_path)

    def delete_tile(self, tile_name: str) -> Dict:
        """Удаляет ассет тайла"""
        return self._execute("delete", tile_name, None)

    def _execute(self, action: str, name: str, sprite: str) -> Dict:
        try:
            data = {
                "action": action,
                "tileName": name,
                "spritePath": sprite
            }
            response = requests.post(f"{self.base_url}/tilemaps/assets", json=data)
            response.raise_for_status()
            return response.json()
        except Exception as e:
            return {"success": False, "error": str(e)}