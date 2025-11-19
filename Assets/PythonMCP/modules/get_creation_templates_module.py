import requests
import json
from typing import Dict

class GetCreationTemplatesModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self, query: str = "", max_results: int = 10) -> Dict:
        """Получает список шаблонов создания ассетов (по запросу)"""
        try:
            response = requests.post(
                f"{self.base_url}/templates/search",
                json={"query": query, "maxResults": max_results}
            )
            response.raise_for_status()
            return response.json()
        except Exception as e:
            return {"success": False, "error": str(e)}