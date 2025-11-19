import requests
import json
from typing import Dict

class ResetObjectModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def execute(self, object_path: str) -> Dict:
        """Сбрасывает параметры объекта на сцене (или компонента/префаба по политике сервера)"""
        try:
            if not object_path:
                return {
                    "success": False,
                    "action": "reset_object",
                    "error": "object_path is required"
                }
            
            data = { "path": object_path }
            response = requests.put(
                f"{self.base_url}/objects/reset",
                json=data
            )
            response.raise_for_status()
            result = response.json()
            return {
                "success": result.get("success", False),
                "action": "reset_object",
                "data": result,
                "error": result.get("error")
            }
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "reset_object",
                "error": f"Request error: {str(e)}"
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "reset_object",
                "error": f"JSON decode error: {str(e)}"
            }