import requests
import json
from typing import Dict

class GetAnimInfoModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self, anim_path: str, query: str = "") -> Dict:
        """Получает информацию о ключах и свойствах анимации"""
        try:
            response = requests.post(
                f"{self.base_url}/animation/info",
                json={"animPath": anim_path, "query": query}
            )
            response.raise_for_status()
            return response.json()
        except Exception as e:
            return {"success": False, "error": str(e)}