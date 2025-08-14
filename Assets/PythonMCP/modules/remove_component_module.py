import requests
import json
from typing import Dict

class RemoveComponentModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def execute(self, object_path: str, component_type: str) -> Dict:
        """Удаляет компонент с объекта"""
        try:
            if not all([object_path, component_type]):
                return {
                    "success": False,
                    "action": "remove_component",
                    "error": "object_path and component_type are required"
                }
            
            response = requests.delete(
                f"{self.base_url}/objects/components/remove", 
                json={"path": object_path, "componentType": component_type}
            )
            response.raise_for_status()
            remove_result = response.json()
            
            return {
                "success": remove_result.get("success", False),
                "action": "remove_component",
                "data": remove_result if remove_result.get("success") else None,
                "error": remove_result.get("error")
            }
            
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "remove_component",
                "error": f"Request error: {str(e)}"
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "remove_component",
                "error": f"JSON decode error: {str(e)}"
            }