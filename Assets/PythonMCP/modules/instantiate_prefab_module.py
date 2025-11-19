import requests
import json
from typing import Dict

class InstantiatePrefabModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def execute(self, prefab_asset_path: str, parent_path: str = "", name_override: str = "") -> Dict:
        """Инстанцирует префаб на сцене"""
        try:
            if not prefab_asset_path:
                return {
                    "success": False,
                    "action": "instantiate_prefab",
                    "error": "prefab_asset_path is required"
                }
            
            data = {
                "prefabPath": prefab_asset_path,
                "parentPath": parent_path,
                "name": name_override
            }
            
            response = requests.post(
                f"{self.base_url}/objects/instantiate/prefab",
                json=data
            )
            response.raise_for_status()
            result = response.json()
            
            return {
                "success": result.get("success", False),
                "action": "instantiate_prefab",
                "data": result,
                "error": result.get("error")
            }
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "instantiate_prefab",
                "error": f"Request error: {str(e)}"
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "instantiate_prefab",
                "error": f"JSON decode error: {str(e)}"
            }