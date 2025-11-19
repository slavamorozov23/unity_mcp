import requests
import json
from typing import Dict


class GetSceneStatusModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self) -> Dict:
        """Получает текущее состояние сцены (play/edit и др.)"""
        try:
            response = requests.get(f"{self.base_url}/scene/status")
            response.raise_for_status()
            result = response.json()
            return {
                "success": result.get("success", True),
                "action": "scene_status",
                "data": result,
                "error": result.get("error"),
            }
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "scene_status",
                "error": f"Request error: {str(e)}",
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "scene_status",
                "error": f"JSON decode error: {str(e)}",
            }
