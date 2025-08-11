import requests
import json
from typing import Dict, List, Optional, Any
import os
import tempfile
from datetime import datetime

MAX_LOG_CHARS = 100000
LOG_FILENAME = "unity_api_client.log.txt"

class UnitySceneAPI:
    def __init__(self, host: str = "localhost", port: int = 8080):
        self.base_url = f"http://{host}:{port}"
        
    def get_scene_hierarchy(self) -> Optional[Dict]:
        try:
            response = requests.get(f"{self.base_url}/scene")
            response.raise_for_status()
            result = response.json()
            return result
        except requests.exceptions.RequestException as e:
            return {"error": f"Request error: {str(e)}"}
        except json.JSONDecodeError as e:
            return {"error": f"JSON decode error: {str(e)}"}
    
    def open_scene(self, scene_path: str) -> Dict:
        try:
            response = requests.post(f"{self.base_url}/scene/open", 
                                   json={"scenePath": scene_path})
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            return {"success": False, "error": str(e)}
    
    def get_build_scenes(self) -> Optional[Dict]:
        try:
            response = requests.get(f"{self.base_url}/build/scenes")
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            return {"error": str(e)}
    
    def add_scene_to_build(self, scene_path: str) -> Dict:
        try:
            response = requests.post(f"{self.base_url}/build/scenes/add", 
                                   json={"scenePath": scene_path})
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            return {"success": False, "error": str(e)}
    
    def remove_scene_from_build(self, scene_path: str) -> Dict:
        try:
            response = requests.delete(f"{self.base_url}/build/scenes/remove", 
                                     json={"scenePath": scene_path})
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            return {"success": False, "error": str(e)}
    
    def create_object(self, name: str = "GameObject", parent_path: str = "") -> Dict:
        try:
            response = requests.post(f"{self.base_url}/objects/create", 
                                   json={"name": name, "parentPath": parent_path})
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            return {"success": False, "error": str(e)}
    
    def delete_object(self, object_path: str) -> Dict:
        try:
            response = requests.delete(f"{self.base_url}/objects/delete", 
                                     json={"path": object_path})
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            return {"success": False, "error": str(e)}
    
    def get_object_components(self, object_path: str) -> Optional[Dict]:
        try:
            response = requests.get(f"{self.base_url}/objects/components", 
                                  params={"path": object_path})
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            return {"error": str(e)}
    
    def add_component(self, object_path: str, component_type: str) -> Dict:
        try:
            response = requests.post(f"{self.base_url}/objects/components/add", 
                                   json={"path": object_path, "componentType": component_type})
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            return {"success": False, "error": str(e)}
    
    def modify_component(self, object_path: str, component_type: str, properties: Dict[str, Any]) -> Dict:
        try:
            response = requests.put(f"{self.base_url}/objects/components/modify", 
                                  json={
                                      "path": object_path, 
                                      "componentType": component_type, 
                                      "properties": properties
                                  })
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            return {"success": False, "error": str(e)}
    
    def remove_component(self, object_path: str, component_type: str) -> Dict:
        try:
            response = requests.delete(f"{self.base_url}/objects/components/remove", 
                                     json={"path": object_path, "componentType": component_type})
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            return {"success": False, "error": str(e)}
    
    def move_object(self, object_path: str, x: float, y: float, z: float) -> Dict:
        return self.modify_component(object_path, "Transform", {
            "position": {"x": x, "y": y, "z": z}
        })
    
    def rotate_object(self, object_path: str, x: float, y: float, z: float, w: float) -> Dict:
        return self.modify_component(object_path, "Transform", {
            "rotation": {"x": x, "y": y, "z": z, "w": w}
        })
    
    def scale_object(self, object_path: str, x: float, y: float, z: float) -> Dict:
        return self.modify_component(object_path, "Transform", {
            "localScale": {"x": x, "y": y, "z": z}
        })
    
    def find_objects_by_name(self, name: str) -> Dict:
        hierarchy = self.get_scene_hierarchy()
        if not hierarchy or "error" in hierarchy:
            return {"paths": [], "error": hierarchy.get("error", "Failed to get hierarchy")}
        
        paths = []
        
        def search_recursive(obj_data):
            if name.lower() in obj_data["name"].lower():
                paths.append(obj_data["path"])
            
            for child in obj_data.get("children", []):
                search_recursive(child)
        
        for root_obj in hierarchy.get("rootObjects", []):
            search_recursive(root_obj)
        
        return {"paths": paths, "searchTerm": name, "foundCount": len(paths)}
    
    # JSON-focused getters for tools integration
    def get_hierarchy_json(self) -> Optional[Dict]:
        return self.get_scene_hierarchy()
    
    def get_build_scenes_json(self) -> Optional[Dict]:
        return self.get_build_scenes()
    
    def get_object_info_json(self, object_path: str) -> Optional[Dict]:
        return self.get_object_components(object_path)
    
    # Внутреннее логирование в temp-файл (до 10000 символов)
    def _get_log_path(self) -> str:
        return os.path.join(tempfile.gettempdir(), LOG_FILENAME)
    
    def get_log_file_path(self) -> str:
        """Публичный метод: получить путь к лог-файлу"""
        return self._get_log_path()
    
    def _log_structured(self, request_payload: Dict, response_payload: Dict) -> None:
        try:
            ts = datetime.utcnow().isoformat() + "Z"
            entry = (
                f"[{ts}] REQUEST:\n" +
                json.dumps(request_payload, ensure_ascii=False, indent=2) +
                "\nRESPONSE:\n" +
                json.dumps(response_payload, ensure_ascii=False, indent=2) +
                "\n\n"
            )
            log_path = self._get_log_path()
            try:
                with open(log_path, "r", encoding="utf-8") as f:
                    existing = f.read()
            except FileNotFoundError:
                existing = ""
            combined = existing + entry
            if len(combined) > MAX_LOG_CHARS:
                combined = combined[-MAX_LOG_CHARS:]
            with open(log_path, "w", encoding="utf-8") as f:
                f.write(combined)
        except Exception:
            # Логирование ошибок логгера не должно мешать основной работе
            pass
    
    # Структурированные JSON методы
    def execute_command(self, command: Dict) -> Dict:
        """
        Выполняет структурированную команду и возвращает структурированный ответ
        Формат запроса: {"action": "get_hierarchy|get_components|create_object|...", "params": {...}}
        """
        try:
            action = command.get("action")
            params = command.get("params", {})
            
            if action == "get_hierarchy":
                hierarchy = self.get_scene_hierarchy()
                result = {
                    "success": True,
                    "action": action,
                    "data": self._format_hierarchy_as_tree(hierarchy) if hierarchy else None,
                    "error": hierarchy.get("error") if hierarchy and "error" in hierarchy else None
                }
                self._log_structured(command, result)
                return result
            
            elif action == "get_components":
                object_path = params.get("object_path")
                if not object_path:
                    result = {"success": False, "action": action, "error": "object_path is required"}
                    self._log_structured(command, result)
                    return result
                
                components = self.get_object_components(object_path)
                filtered_components = self._filter_inspector_properties(components) if components else None
                
                result = {
                    "success": True,
                    "action": action,
                    "data": {
                        "object_path": object_path,
                        "components": filtered_components
                    },
                    "error": components.get("error") if components and "error" in components else None
                }
                self._log_structured(command, result)
                return result
            
            elif action == "create_object":
                name = params.get("name", "GameObject")
                parent_path = params.get("parent_path", "")
                create_result = self.create_object(name, parent_path)
                
                result = {
                    "success": create_result.get("success", False),
                    "action": action,
                    "data": create_result if create_result.get("success") else None,
                    "error": create_result.get("error")
                }
                self._log_structured(command, result)
                return result
            
            elif action == "modify_component":
                object_path = params.get("object_path")
                component_type = params.get("component_type")
                properties = params.get("properties", {})
                
                if not all([object_path, component_type]):
                    result = {"success": False, "action": action, "error": "object_path and component_type are required"}
                    self._log_structured(command, result)
                    return result
                
                modify_result = self.modify_component(object_path, component_type, properties)
                
                result = {
                    "success": modify_result.get("success", False),
                    "action": action,
                    "data": modify_result if modify_result.get("success") else None,
                    "error": modify_result.get("error")
                }
                self._log_structured(command, result)
                return result
            
            elif action == "find_objects":
                name = params.get("name")
                if not name:
                    result = {"success": False, "action": action, "error": "name is required"}
                    self._log_structured(command, result)
                    return result
                
                find_result = self.find_objects_by_name(name)
                
                result = {
                    "success": True,
                    "action": action,
                    "data": find_result,
                    "error": find_result.get("error")
                }
                self._log_structured(command, result)
                return result
            
            else:
                result = {"success": False, "action": action, "error": f"Unknown action: {action}"}
                self._log_structured(command, result)
                return result
                
        except Exception as e:
            result = {"success": False, "action": command.get("action", "unknown"), "error": str(e)}
            self._log_structured(command, result)
            return result
    
    def _format_hierarchy_as_tree(self, hierarchy: Dict) -> Dict:
        """Форматирует иерархию сцены как JSON дерево"""
        if not hierarchy or "error" in hierarchy:
            return hierarchy
        
        def format_object(obj):
            formatted = {
                "name": obj.get("name"),
                "path": obj.get("path"),
                "active": obj.get("active", True),
                "children": []
            }
            
            for child in obj.get("children", []):
                formatted["children"].append(format_object(child))
            
            return formatted
        
        return {
            "scene_name": hierarchy.get("sceneName", "Unknown"),
            "root_objects": [format_object(obj) for obj in hierarchy.get("rootObjects", [])],
            "total_objects": hierarchy.get("totalObjects", 0)
        }
    
    def _filter_inspector_properties(self, components_data) -> Dict:
        """Фильтрует только свойства, видимые и редактируемые в Inspector Unity"""
        if not components_data:
            return {}
            
        if isinstance(components_data, dict) and "error" in components_data:
            return components_data
        
        # Если данные не являются словарем, возвращаем как есть
        if not isinstance(components_data, dict):
            return {"error": "Invalid components data format"}
        
        # Список свойств, которые НЕ видны или НЕ редактируемые в Inspector
        hidden_properties = {
            'hideFlags', 'worldToLocalMatrix', 'localToWorldMatrix', 'root', 'childCount',
            'hierarchyCapacity', 'hierarchyCount', 'transform', 'gameObject', 'tag',
            'right', 'up', 'forward', 'hasChanged', 'parent', 'worldCenterOfMass',
            'automaticCenterOfMass', 'automaticInertiaTensor', 'inertiaTensorRotation',
            'inertiaTensor', 'excludeLayers', 'includeLayers', 'sleepVelocity', 
            'sleepAngularVelocity', 'solverIterationCount', 'solverVelocityIterationCount'
        }
        
        filtered_components = {}
        
        for component_name, component_data in components_data.items():
            if component_name == "error":
                continue
            
            # Проверяем, что component_data является словарем
            if not isinstance(component_data, dict):
                filtered_components[component_name] = component_data
                continue
                
            filtered_component = {}
            
            for prop_name, prop_value in component_data.items():
                if prop_name not in hidden_properties:
                    filtered_component[prop_name] = prop_value
            
            # Дополнительная фильтрация для Transform
            if component_name == "Transform":
                # Оставляем только основные редактируемые свойства Transform
                transform_editable = {}
                for key in ['position', 'localPosition', 'rotation', 'localRotation', 
                           'eulerAngles', 'localEulerAngles', 'localScale', 'name']:
                    if key in filtered_component:
                        transform_editable[key] = filtered_component[key]
                filtered_components[component_name] = transform_editable
            else:
                filtered_components[component_name] = filtered_component
        
        return filtered_components

# Оставляем пример main для проверки, но он не обязателен для интеграции
def main():
    unity = UnitySceneAPI()
    
    hierarchy_request = {"action": "get_hierarchy"}
    hierarchy_response = unity.execute_command(hierarchy_request)
    print(json.dumps(hierarchy_response, indent=2, ensure_ascii=False))
    
    components_request = {
        "action": "get_components",
        "params": {"object_path": "Main Camera"}
    }
    components_response = unity.execute_command(components_request)
    print(json.dumps(components_response, indent=2, ensure_ascii=False))
    
    print("Log saved to:", unity.get_log_file_path())

if __name__ == "__main__":
    main()