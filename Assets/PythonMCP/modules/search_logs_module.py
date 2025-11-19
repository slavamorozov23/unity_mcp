import requests
import json
from typing import Dict


class SearchLogsModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self, query: str, max_results: int = 100) -> Dict:
        """Ищет логи по строке запроса"""
        try:
            payload = {"query": query, "maxResults": max_results}
            response = requests.post(f"{self.base_url}/logs/search", json=payload)
            response.raise_for_status()
            result = response.json()
            return {
                "success": result.get("success", True),
                "action": "search_logs",
                "data": result,
                "error": result.get("error"),
            }
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "search_logs",
                "error": f"Request error: {str(e)}",
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "search_logs",
                "error": f"JSON decode error: {str(e)}",
            }
