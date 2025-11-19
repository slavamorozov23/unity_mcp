import requests
import json
from typing import Dict

class SaveObjectAsPrefabModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def execute(self, object_path: str, prefab_name: str = "", target_folder: str = "Assets/Prefabs") -> Dict:
        """Сохраняет объект как префаб в указанную папку"""
        try:
            if not object_path:
                return {
                    "success": False,
                    "action": "save_object_as_prefab",
                    "error": "object_path is required"
                }
            
            data = {
                "path": object_path,
                "prefabName": prefab_name,
                "targetFolder": target_folder
            }
            
            response = requests.post(
                f"{self.base_url}/objects/save/prefab",
                json=data
            )
            response.raise_for_status()
            result = response.json()
            
            return {
                "success": result.get("success", False),
                "action": "save_object_as_prefab",
                "data": result,
                "error": result.get("error")
            }
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "save_object_as_prefab",
                "error": f"Request error: {str(e)}"
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "save_object_as_prefab",
                "error": f"JSON decode error: {str(e)}"
            }