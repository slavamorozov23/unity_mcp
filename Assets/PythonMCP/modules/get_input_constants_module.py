import requests
import json
from typing import Dict

class GetInputConstantsModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self) -> Dict:
        """Получает словарь констант для InputManager (Type, Axis, JoyNum)"""
        try:
            response = requests.get(f"{self.base_url}/input/constants")
            response.raise_for_status()
            return response.json()
        except Exception as e:
            return {"success": False, "error": str(e)}