import requests
import json
from typing import Dict


class PlayControlModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self, command: str) -> Dict:
        """Управляет воспроизведением сцены (play/pause/stop и т.п.)"""
        try:
            payload = {"command": command}
            response = requests.post(f"{self.base_url}/scene/control", json=payload)
            response.raise_for_status()
            result = response.json()
            return {
                "success": result.get("success", True),
                "action": "scene_control",
                "data": result,
                "error": result.get("error"),
            }
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "scene_control",
                "error": f"Request error: {str(e)}",
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "scene_control",
                "error": f"JSON decode error: {str(e)}",
            }
