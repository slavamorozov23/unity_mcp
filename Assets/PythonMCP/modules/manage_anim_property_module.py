import requests
import json
from typing import Dict

class ManageAnimPropertyModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def add_key(self, anim_path: str, object_path: str, component_type: str, property_name: str, time: float, value: float) -> Dict:
        return self._execute("add_key", anim_path, object_path, component_type, property_name, time, value)

    def remove_property(self, anim_path: str, object_path: str, component_type: str, property_name: str) -> Dict:
        return self._execute("remove_property", anim_path, object_path, component_type, property_name, 0, 0)

    def _execute(self, action: str, anim_path: str, object_path: str, type: str, prop: str, time: float, val: float) -> Dict:
        try:
            data = {
                "action": action,
                "animPath": anim_path,
                "objectPath": object_path,
                "componentType": type,
                "propertyName": prop,
                "time": time,
                "value": val
            }
            response = requests.post(f"{self.base_url}/animation/modify", json=data)
            response.raise_for_status()
            return response.json()
        except Exception as e:
            return {"success": False, "error": str(e)}