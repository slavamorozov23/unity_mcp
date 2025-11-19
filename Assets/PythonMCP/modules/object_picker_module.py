import requests
import json
from typing import Dict, List

class ObjectPickerModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def get_object_picker_options(self, object_path: str) -> Dict:
        """Получает 10 вариантов для Object Picker"""
        try:
            if not object_path:
                return {
                    "success": False,
                    "action": "object_picker_options",
                    "error": "object_path is required"
                }
            
            # Получаем иерархию для генерации вариантов
            hierarchy_response = requests.get(f"{self.base_url}/scene")
            hierarchy_response.raise_for_status()
            hierarchy = hierarchy_response.json()
            
            # Генерируем список всех объектов для Object Picker
            all_objects = self._extract_all_objects(hierarchy.get("rootObjects", []))
            
            # Фильтруем и берем первые 10 объектов
            picker_options = all_objects[:10]
            
            return {
                "success": True,
                "action": "object_picker_options",
                "data": {
                    "object_path": object_path,
                    "options": picker_options
                }
            }
            
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "object_picker_options",
                "error": f"Request error: {str(e)}"
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "object_picker_options",
                "error": f"JSON decode error: {str(e)}"
            }
    
    def _extract_all_objects(self, root_objects: List, parent_path: str = "") -> List[Dict]:
        """Рекурсивно извлекает все объекты из иерархии"""
        objects = []
        
        for obj in root_objects:
            if isinstance(obj, dict):
                name = obj.get("name", "Unknown")
                path = f"{parent_path}/{name}" if parent_path else name
                
                # Добавляем текущий объект
                objects.append({
                    "name": name,
                    "path": path,
                    "type": "GameObject",
                    "components": obj.get("components", [])
                })
                
                # Рекурсивно обрабатываем дочерние объекты
                children = obj.get("children", [])
                if children:
                    objects.extend(self._extract_all_objects(children, path))
        
        return objects