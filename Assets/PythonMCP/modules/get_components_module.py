import requests
import json
from typing import Dict, Optional

class GetComponentsModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def execute(self, object_path: str) -> Dict:
        """Получает компоненты указанного объекта"""
        try:
            if not object_path:
                return {
                    "success": False,
                    "action": "get_components",
                    "error": "object_path is required"
                }
            
            response = requests.get(
                f"{self.base_url}/objects/components", 
                params={"path": object_path}
            )
            response.raise_for_status()
            components = response.json()
            
            filtered_components = self._filter_inspector_properties(components) if components else None
            
            return {
                "success": True,
                "action": "get_components",
                "data": {
                    "object_path": object_path,
                    "components": filtered_components
                },
                "error": components.get("error") if components and "error" in components else None
            }
            
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "get_components",
                "error": f"Request error: {str(e)}"
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "get_components",
                "error": f"JSON decode error: {str(e)}"
            }
    
    def _filter_inspector_properties(self, components_data) -> Dict:
        """Обрабатывает данные компонентов, полученные от Unity API"""
        if not components_data:
            return {}
            
        if isinstance(components_data, dict) and "error" in components_data:
            return components_data
        
        # Если данные не являются словарем, возвращаем как есть
        if not isinstance(components_data, dict):
            return {"error": "Invalid components data format"}
        
        # Преобразуем структуру данных для удобства использования
        filtered_components = {}
        
        for component_name, component_data in components_data.items():
            if component_name == "error":
                continue
            
            # Проверяем, что component_data является словарем
            if not isinstance(component_data, dict):
                filtered_components[component_name] = component_data
                continue
            
            # Unity API теперь возвращает только Inspector-видимые свойства,
            # поэтому просто передаем данные как есть
            filtered_components[component_name] = component_data
        
        return filtered_components