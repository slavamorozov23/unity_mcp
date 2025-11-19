import requests
import json
from typing import Dict, Any

class ModifyAssetModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self, asset_path: str, properties: Dict[str, Any] = None, import_settings: Dict[str, Any] = None) -> Dict:
        """Модифицирует свойства ассета или настройки импорта"""
        try:
            data = {"assetPath": asset_path}
            if properties:
                data["properties"] = properties
            if import_settings:
                data["importSettings"] = import_settings

            response = requests.put(
                f"{self.base_url}/assets/modify",
                json=data
            )
            response.raise_for_status()
            return response.json()
        except Exception as e:
            return {"success": False, "error": str(e)}