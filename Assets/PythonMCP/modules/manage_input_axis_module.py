import requests
import json
from typing import Dict, Any

class ManageInputAxisModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def list_axes(self) -> Dict:
        return self._execute({"action": "list"})

    def delete_axis(self, name: str) -> Dict:
        return self._execute({"action": "delete", "name": name})

    def create_axis(self, name: str, pos_btn: str, neg_btn: str, alt_pos: str = "", alt_neg: str = "", type: int = 0, axis: int = 0) -> Dict:
        data = {
            "action": "create",
            "name": name,
            "positiveButton": pos_btn,
            "negativeButton": neg_btn,
            "altPositiveButton": alt_pos,
            "altNegativeButton": alt_neg,
            "type": type,
            "axis": axis
        }
        return self._execute(data)

    def _execute(self, data: Dict) -> Dict:
        try:
            response = requests.post(f"{self.base_url}/input/axes", json=data)
            response.raise_for_status()
            return response.json()
        except Exception as e:
            return {"success": False, "error": str(e)}