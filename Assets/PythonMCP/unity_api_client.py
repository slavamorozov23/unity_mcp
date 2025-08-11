import requests
import json
from typing import Dict, List, Optional, Any
import os
import tempfile
from datetime import datetime
from collections import Counter
import difflib

MAX_LOG_CHARS = 10000000
LOG_FILENAME = "unity_api_client.log.txt"

class UnitySceneAPI:
    def __init__(self, host: str = "localhost", port: int = 8080):
        self.base_url = f"http://{host}:{port}"
        self._clear_log_file()
        
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
    
    def _clear_log_file(self) -> None:
        """Удаляет временный файл логов перед началом работы"""
        try:
            log_path = self._get_log_path()
            if os.path.exists(log_path):
                os.remove(log_path)
        except Exception:
            # Ошибки при удалении лог-файла не должны мешать основной работе
            pass
    
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

                # Optional: filter hierarchy starting from a node whose path contains the substring
                try:
                    params_from_path = params.get("from_path") or params.get("path") or params.get("path_contains")
                except Exception:
                    params_from_path = None

                if hierarchy and not (isinstance(hierarchy, dict) and "error" in hierarchy) and params_from_path:
                    sub = params_from_path.strip()

                    def find_first_match(node: Dict, needle: str):
                        p = node.get("path", "")
                        if isinstance(p, str) and needle.lower() in p.lower():
                            return node
                        for ch in node.get("children", []) or []:
                            found = find_first_match(ch, needle)
                            if found:
                                return found
                        return None

                    found_node = None
                    for root in hierarchy.get("rootObjects", []) or []:
                        found_node = find_first_match(root, sub)
                        if found_node:
                            break

                    if found_node:
                        def count_nodes(n: Dict) -> int:
                            total = 1
                            for ch in n.get("children", []) or []:
                                total += count_nodes(ch)
                            return total
                        hierarchy = {
                            "sceneName": hierarchy.get("sceneName", "Unknown"),
                            "rootObjects": [found_node],
                            "totalObjects": count_nodes(found_node)
                        }

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
        """Форматирует иерархию сцены как JSON дерево c группировкой одноимённых дочерних объектов,
        имеющих одинаковый набор типов компонентов (значения параметров компонентов игнорируются).
        Теперь также группирует объекты с похожими именами (75%+ сходства) при одинаковых компонентах.
        Для сгруппированных записей добавляется поле "count" и список "paths"."""
        if not hierarchy or "error" in hierarchy:
            return hierarchy
        
        def name_similarity(name1: str, name2: str) -> float:
            """Вычисляет сходство между двумя именами (0.0 - 1.0)"""
            if name1 == name2:
                return 1.0
            return difflib.SequenceMatcher(None, name1.lower(), name2.lower()).ratio()
        
        def components_signature(obj: Dict) -> tuple:
            comps = obj.get("components", []) or []
            if not isinstance(comps, list):
                return tuple()
            counter = Counter([str(c) for c in comps])
            return tuple(sorted(counter.items()))  # [(typeName, count), ...] deterministically sorted
        
        def rebuild_components_from_signature(sig: tuple) -> List[str]:
            comps: List[str] = []
            for type_name, cnt in sig:
                comps.extend([type_name] * int(cnt))
            return comps
        
        def get_parent_path(obj_path: str) -> str:
            """Извлекает путь родителя из пути объекта"""
            if not obj_path or "/" not in obj_path:
                return ""
            return "/".join(obj_path.split("/")[:-1])
        
        def format_grouped_list(children_raw: List[Dict]) -> List[Dict]:
            # Сначала группируем по точному совпадению имени и компонентов
            exact_groups: Dict[tuple, List[Dict]] = {}
            for ch in children_raw or []:
                key = (ch.get("name"), components_signature(ch))
                exact_groups.setdefault(key, []).append(ch)
            
            # Теперь ищем группы по сходству имен среди одиночных объектов
            single_objects = []
            grouped_objects = []
            
            for (name, sig), items in exact_groups.items():
                if len(items) == 1:
                    single_objects.append((name, sig, items[0]))
                else:
                    # Точно одинаковые объекты - группируем как раньше
                    merged_children_raw: List[Dict] = []
                    for it in items:
                        merged_children_raw.extend(it.get("children", []))

                    from collections import Counter as _Counter
                    path_list = [it.get("path") for it in items if it.get("path")]
                    pc = _Counter(path_list)

                    grouped_node = {
                        "name": name,
                        "count": len(items),
                        "components": rebuild_components_from_signature(sig),
                        "children": format_grouped_list(merged_children_raw)
                    }

                    if len(pc) == 1:
                        only_path = next(iter(pc.keys())) if pc else None
                        if only_path:
                            grouped_node["path"] = only_path
                    elif len(pc) > 1:
                        grouped_node["path_groups"] = [
                            {"path": p, "count": c} for p, c in sorted(pc.items())
                        ]

                    grouped_objects.append(grouped_node)
            
            # Теперь группируем одиночные объекты по сходству имен
            similarity_groups: List[List[tuple]] = []
            used_indices = set()
            
            for i, (name1, sig1, obj1) in enumerate(single_objects):
                if i in used_indices:
                    continue
                    
                current_group = [(name1, sig1, obj1)]
                used_indices.add(i)
                parent1 = get_parent_path(obj1.get("path", ""))
                
                for j, (name2, sig2, obj2) in enumerate(single_objects):
                    if j in used_indices or i == j:
                        continue
                    
                    parent2 = get_parent_path(obj2.get("path", ""))
                    
                    # Проверяем: одинаковые компоненты, одинаковый родитель, сходство имен >= 75%
                    if (sig1 == sig2 and 
                        parent1 == parent2 and 
                        name_similarity(name1, name2) >= 0.75):
                        current_group.append((name2, sig2, obj2))
                        used_indices.add(j)
                
                similarity_groups.append(current_group)
            
            # Формируем окончательный результат
            formatted_children: List[Dict] = []
            
            # Добавляем уже сгруппированные объекты (точное совпадение)
            formatted_children.extend(grouped_objects)
            
            # Обрабатываем группы по сходству
            for group in similarity_groups:
                if len(group) == 1:
                    # Одиночный объект
                    name, sig, obj = group[0]
                    formatted_children.append(format_object(obj))
                else:
                    # Группа объектов с похожими именами
                    names = [item[0] for item in group]
                    objects = [item[2] for item in group]
                    sig = group[0][1]  # Компоненты одинаковые
                    
                    # Объединяем детей всех объектов группы
                    merged_children_raw: List[Dict] = []
                    for obj in objects:
                        merged_children_raw.extend(obj.get("children", []))
                    
                    # Определяем базовое имя для группы (самое частое или первое)
                    name_counter = Counter(names)
                    base_name = name_counter.most_common(1)[0][0] if name_counter else names[0]
                    
                    grouped_node = {
                        "name": base_name,
                        "names": sorted(list(set(names))),  # Список уникальных имен
                        "count": len(objects),
                        "components": rebuild_components_from_signature(sig),
                        "children": format_grouped_list(merged_children_raw)
                    }
                    
                    formatted_children.append(grouped_node)
            
            return formatted_children
        
        def format_object(obj: Dict) -> Dict:
            return {
                "name": obj.get("name"),
                "path": obj.get("path"),
                "active": obj.get("active", True),
                "components": obj.get("components", []) or [],
                "children": format_grouped_list(obj.get("children", []))
            }
        
        return {
            "scene_name": hierarchy.get("sceneName", "Unknown"),
            "root_objects": format_grouped_list(hierarchy.get("rootObjects", [])),
            "total_objects": hierarchy.get("totalObjects", 0)
        }
    
    def _filter_inspector_properties(self, components_data) -> Dict:
        """Обрабатывает данные компонентов, полученные от Unity API.
        Основная фильтрация теперь происходит на стороне Unity C# кода."""
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
 
    # Специальный тест: получить иерархию начиная с узла, путь которого содержит "Enemies"
    subtree_request = {
        "action": "get_hierarchy",
        "params": {"from_path": "Enemies"}
    }
    subtree_response = unity.execute_command(subtree_request)
    print("Subtree (from_path='Enemies'):")
    print(json.dumps(subtree_response, indent=2, ensure_ascii=False))

    print("Log saved to:", unity.get_log_file_path())

if __name__ == "__main__":
    main()