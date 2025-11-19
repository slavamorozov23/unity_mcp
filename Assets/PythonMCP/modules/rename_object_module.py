import requests
import json
from typing import Dict


class RenameObjectModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self, object_path: str, new_name: str) -> Dict:
        """Переименовывает объект в сцене"""
        try:
            if not object_path or not new_name:
                return {
                    "success": False,
                    "action": "rename_object",
                    "error": "object_path and new_name are required",
                }

            data = {"path": object_path, "newName": new_name}
            response = requests.put(f"{self.base_url}/objects/rename", json=data)
            response.raise_for_status()
            result = response.json()

            return {
                "success": result.get("success", False),
                "action": "rename_object",
                "data": result if result.get("success") else None,
                "error": result.get("error"),
            }
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "rename_object",
                "error": f"Request error: {str(e)}",
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "rename_object",
                "error": f"JSON decode error: {str(e)}",
            }
