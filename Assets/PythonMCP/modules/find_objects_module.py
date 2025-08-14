import requests
import json
from typing import Dict, List

class FindObjectsModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def execute(self, name: str) -> Dict:
        """Находит объекты по имени в иерархии сцены"""
        try:
            if not name:
                return {
                    "success": False,
                    "action": "find_objects",
                    "error": "name is required"
                }
            
            # Получаем иерархию сцены
            response = requests.get(f"{self.base_url}/scene")
            response.raise_for_status()
            hierarchy = response.json()
            
            if not hierarchy or "error" in hierarchy:
                return {
                    "success": False,
                    "action": "find_objects",
                    "error": hierarchy.get("error", "Failed to get hierarchy")
                }
            
            paths = []
            
            def search_recursive(obj_data):
                if name.lower() in obj_data["name"].lower():
                    paths.append(obj_data["path"])
                
                for child in obj_data.get("children", []):
                    search_recursive(child)
            
            for root_obj in hierarchy.get("rootObjects", []):
                search_recursive(root_obj)
            
            find_result = {
                "paths": paths, 
                "searchTerm": name, 
                "foundCount": len(paths)
            }
            
            return {
                "success": True,
                "action": "find_objects",
                "data": find_result,
                "error": None
            }
            
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "find_objects",
                "error": f"Request error: {str(e)}"
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "find_objects",
                "error": f"JSON decode error: {str(e)}"
            }