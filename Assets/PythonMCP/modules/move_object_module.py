import requests
import json
from typing import Dict

class MoveObjectModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def execute(self, source_path: str, target_parent_path: str, new_name: str = "") -> Dict:
        """Перемещает объект в дереве сцены"""
        try:
            if not source_path or not target_parent_path:
                return {
                    "success": False,
                    "action": "move_object",
                    "error": "source_path and target_parent_path are required"
                }
            
            data = {
                "sourcePath": source_path,
                "targetParentPath": target_parent_path,
                "newName": new_name
            }
            response = requests.put(
                f"{self.base_url}/objects/move",
                json=data
            )
            response.raise_for_status()
            result = response.json()
            return {
                "success": result.get("success", False),
                "action": "move_object",
                "data": result,
                "error": result.get("error")
            }
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "move_object",
                "error": f"Request error: {str(e)}"
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "move_object",
                "error": f"JSON decode error: {str(e)}"
            }