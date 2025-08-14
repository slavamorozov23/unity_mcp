import requests
import json
from typing import Dict, Any

class ModifyComponentModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def execute(self, object_path: str, component_type: str, properties: Dict[str, Any]) -> Dict:
        """Модифицирует свойства компонента объекта"""
        try:
            if not all([object_path, component_type]):
                return {
                    "success": False,
                    "action": "modify_component",
                    "error": "object_path and component_type are required"
                }
            
            response = requests.put(
                f"{self.base_url}/objects/components/modify", 
                json={
                    "path": object_path, 
                    "componentType": component_type, 
                    "properties": properties
                }
            )
            response.raise_for_status()
            modify_result = response.json()
            
            return {
                "success": modify_result.get("success", False),
                "action": "modify_component",
                "data": modify_result if modify_result.get("success") else None,
                "error": modify_result.get("error")
            }
            
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "modify_component",
                "error": f"Request error: {str(e)}"
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "modify_component",
                "error": f"JSON decode error: {str(e)}"
            }
    
    def move_object(self, object_path: str, x: float, y: float, z: float) -> Dict:
        """Перемещает объект в указанную позицию"""
        return self.execute(object_path, "Transform", {
            "position": {"x": x, "y": y, "z": z}
        })
    
    def rotate_object(self, object_path: str, x: float, y: float, z: float, w: float) -> Dict:
        """Поворачивает объект с указанным кватернионом"""
        return self.execute(object_path, "Transform", {
            "rotation": {"x": x, "y": y, "z": z, "w": w}
        })
    
    def scale_object(self, object_path: str, x: float, y: float, z: float) -> Dict:
        """Масштабирует объект"""
        return self.execute(object_path, "Transform", {
            "localScale": {"x": x, "y": y, "z": z}
        })