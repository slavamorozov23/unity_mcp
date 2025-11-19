import requests
import json
from typing import Dict

class GetAssetInfoModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self, asset_path: str) -> Dict:
        """Получает информацию об ассете (свойства, зависимости)"""
        try:
            response = requests.post(
                f"{self.base_url}/assets/info",
                json={"assetPath": asset_path}
            )
            response.raise_for_status()
            return response.json()
        except Exception as e:
            return {"success": False, "error": str(e)}