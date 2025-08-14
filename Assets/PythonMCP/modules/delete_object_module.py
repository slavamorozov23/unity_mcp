import requests
import json
from typing import Dict

class DeleteObjectModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def execute(self, object_path: str) -> Dict:
        """Удаляет объект из сцены"""
        try:
            if not object_path:
                return {
                    "success": False,
                    "action": "delete_object",
                    "error": "object_path is required"
                }
            
            response = requests.delete(
                f"{self.base_url}/objects/delete", 
                json={"path": object_path}
            )
            response.raise_for_status()
            delete_result = response.json()
            
            return {
                "success": delete_result.get("success", False),
                "action": "delete_object",
                "data": delete_result if delete_result.get("success") else None,
                "error": delete_result.get("error")
            }
            
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "delete_object",
                "error": f"Request error: {str(e)}"
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "delete_object",
                "error": f"JSON decode error: {str(e)}"
            }