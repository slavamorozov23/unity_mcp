import requests
import json
from typing import Dict, List

class GetPrefabsListModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def execute(self) -> Dict:
        """Получает список префабов из стандартной папки"""
        try:
            response = requests.get(f"{self.base_url}/prefabs")
            response.raise_for_status()
            result = response.json()
            return {
                "success": result.get("success", False),
                "action": "get_prefabs_list",
                "data": result,
                "error": result.get("error")
            }
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "get_prefabs_list",
                "error": f"Request error: {str(e)}"
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "get_prefabs_list",
                "error": f"JSON decode error: {str(e)}"
            }