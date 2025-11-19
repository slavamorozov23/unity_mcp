import requests
import json
from typing import Dict


class GetLogsModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self) -> Dict:
        """Получает список логов с Unity-сервера"""
        try:
            response = requests.get(f"{self.base_url}/logs")
            response.raise_for_status()
            result = response.json()
            return {
                "success": result.get("success", True),
                "action": "get_logs",
                "data": result,
                "error": result.get("error"),
            }
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "get_logs",
                "error": f"Request error: {str(e)}",
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "get_logs",
                "error": f"JSON decode error: {str(e)}",
            }
