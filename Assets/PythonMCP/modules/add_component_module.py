import requests
import json
from typing import Dict

class AddComponentModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def execute(self, object_path: str, component_type: str) -> Dict:
        """Добавляет компонент к объекту"""
        try:
            if not all([object_path, component_type]):
                return {
                    "success": False,
                    "action": "add_component",
                    "error": "object_path and component_type are required"
                }
            
            response = requests.post(
                f"{self.base_url}/objects/components/add", 
                json={"path": object_path, "componentType": component_type}
            )
            response.raise_for_status()
            add_result = response.json()
            
            return {
                "success": add_result.get("success", False),
                "action": "add_component",
                "data": add_result if add_result.get("success") else None,
                "error": add_result.get("error")
            }
            
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "add_component",
                "error": f"Request error: {str(e)}"
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "add_component",
                "error": f"JSON decode error: {str(e)}"
            }