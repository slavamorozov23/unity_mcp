import requests
import json
from typing import Dict

class CreateObjectModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def execute(self, name: str = "GameObject", parent_path: str = "") -> Dict:
        """Создает новый GameObject в сцене"""
        try:
            response = requests.post(
                f"{self.base_url}/objects/create", 
                json={"name": name, "parentPath": parent_path}
            )
            response.raise_for_status()
            create_result = response.json()
            
            return {
                "success": create_result.get("success", False),
                "action": "create_object",
                "data": create_result if create_result.get("success") else None,
                "error": create_result.get("error")
            }
            
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "create_object",
                "error": f"Request error: {str(e)}"
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "create_object",
                "error": f"JSON decode error: {str(e)}"
            }