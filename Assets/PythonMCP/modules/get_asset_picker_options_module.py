import requests
import json
from typing import Dict

class GetAssetPickerOptionsModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self, asset_path: str, property_name: str, max_results: int = 10) -> Dict:
        """Получает варианты для выбора ассета (для поля Object Picker)"""
        try:
            data = {
                "assetPath": asset_path,
                "propertyName": property_name,
                "maxResults": max_results
            }
            response = requests.post(
                f"{self.base_url}/assets/picker/options",
                json=data
            )
            response.raise_for_status()
            return response.json()
        except Exception as e:
            return {"success": False, "error": str(e)}