import requests
import json
from typing import Dict

class CreateObjectFromPrefabModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def execute(self, prefab_path: str, object_name: str = "", parent_path: str = "") -> Dict:
        """Создает объект из префаба"""
        try:
            if not prefab_path:
                return {
                    "success": False,
                    "action": "create_object_from_prefab",
                    "error": "prefab_path is required"
                }
            
            data = {
                "prefabPath": prefab_path,
                "objectName": object_name,
                "parentPath": parent_path
            }
            
            response = requests.post(
                f"{self.base_url}/objects/create/prefab",
                json=data
            )
            response.raise_for_status()
            result = response.json()
            
            return {
                "success": result.get("success", False),
                "action": "create_object_from_prefab",
                "data": result,
                "error": result.get("error")
            }
            
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "create_object_from_prefab",
                "error": f"Request error: {str(e)}"
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "create_object_from_prefab",
                "error": f"JSON decode error: {str(e)}"
            }