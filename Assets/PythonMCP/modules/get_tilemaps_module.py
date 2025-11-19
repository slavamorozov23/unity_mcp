import requests
import json
from typing import Dict

class GetTilemapsModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self) -> Dict:
        """Получает список всех Tilemap на сцене"""
        try:
            response = requests.get(f"{self.base_url}/tilemaps")
            response.raise_for_status()
            return response.json()
        except Exception as e:
            return {"success": False, "error": str(e)}