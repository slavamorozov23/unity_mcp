import requests
import json
from typing import Dict


class SetObjectActiveModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self, object_path: str, active: bool) -> Dict:
        """Включает/выключает объект на сцене"""
        try:
            if not object_path:
                return {
                    "success": False,
                    "action": "set_active",
                    "error": "object_path is required",
                }

            data = {"path": object_path, "active": active}
            response = requests.put(f"{self.base_url}/objects/active", json=data)
            response.raise_for_status()
            result = response.json()

            return {
                "success": result.get("success", False),
                "action": "set_active",
                "data": result if result.get("success") else None,
                "error": result.get("error"),
            }
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "set_active",
                "error": f"Request error: {str(e)}",
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "set_active",
                "error": f"JSON decode error: {str(e)}",
            }
