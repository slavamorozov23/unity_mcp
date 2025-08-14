import requests
import json
from typing import Dict, List, Optional, Any
from collections import Counter
import difflib

class GetHierarchyModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def execute(self, params: Dict = None) -> Dict:
        """Получает иерархию сцены с возможностью фильтрации"""
        try:
            response = requests.get(f"{self.base_url}/scene")
            response.raise_for_status()
            hierarchy = response.json()
            
            # Фильтрация по пути, если указан параметр from_path
            if params and hierarchy and not (isinstance(hierarchy, dict) and "error" in hierarchy):
                params_from_path = params.get("from_path") or params.get("path") or params.get("path_contains")
                
                if params_from_path:
                    sub = params_from_path.strip()
                    found_node = self._find_node_by_path(hierarchy, sub)
                    
                    if found_node:
                        hierarchy = {
                            "sceneName": hierarchy.get("sceneName", "Unknown"),
                            "rootObjects": [found_node],
                            "totalObjects": self._count_nodes(found_node)
                        }
            
            return {
                "success": True,
                "action": "get_hierarchy",
                "data": self._format_hierarchy_as_tree(hierarchy) if hierarchy else None,
                "error": hierarchy.get("error") if hierarchy and "error" in hierarchy else None
            }
            
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "get_hierarchy",
                "error": f"Request error: {str(e)}"
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "get_hierarchy",
                "error": f"JSON decode error: {str(e)}"
            }
    
    def _find_node_by_path(self, hierarchy: Dict, needle: str) -> Optional[Dict]:
        """Находит первый узел, путь которого содержит указанную подстроку"""
        def find_first_match(node: Dict, needle: str):
            p = node.get("path", "")
            if isinstance(p, str) and needle.lower() in p.lower():
                return node
            for ch in node.get("children", []) or []:
                found = find_first_match(ch, needle)
                if found:
                    return found
            return None
        
        for root in hierarchy.get("rootObjects", []) or []:
            found_node = find_first_match(root, needle)
            if found_node:
                return found_node
        return None
    
    def _count_nodes(self, node: Dict) -> int:
        """Подсчитывает общее количество узлов в дереве"""
        total = 1
        for ch in node.get("children", []) or []:
            total += self._count_nodes(ch)
        return total
    
    def _format_hierarchy_as_tree(self, hierarchy: Dict) -> Dict:
        """Форматирует иерархию сцены как JSON дерево с группировкой объектов"""
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
            return tuple(sorted(counter.items()))
        
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
            # Группировка по точному совпадению имени и компонентов
            exact_groups: Dict[tuple, List[Dict]] = {}
            for ch in children_raw or []:
                key = (ch.get("name"), components_signature(ch))
                exact_groups.setdefault(key, []).append(ch)
            
            # Разделение на одиночные и групповые объекты
            single_objects = []
            grouped_objects = []
            
            for (name, sig), items in exact_groups.items():
                if len(items) == 1:
                    single_objects.append((name, sig, items[0]))
                else:
                    # Точно одинаковые объекты - группируем
                    merged_children_raw: List[Dict] = []
                    for it in items:
                        merged_children_raw.extend(it.get("children", []))

                    path_list = [it.get("path") for it in items if it.get("path")]
                    pc = Counter(path_list)

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
            
            # Группировка одиночных объектов по сходству имен
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
            
            # Формирование окончательного результата
            formatted_children: List[Dict] = []
            
            # Добавляем уже сгруппированные объекты
            formatted_children.extend(grouped_objects)
            
            # Обрабатываем группы по сходству
            for group in similarity_groups:
                if len(group) == 1:
                    # Одиночный объект
                    name, sig, obj = group[0]
                    formatted_children.append(self._format_object(obj))
                else:
                    # Группа объектов с похожими именами
                    names = [item[0] for item in group]
                    objects = [item[2] for item in group]
                    sig = group[0][1]  # Компоненты одинаковые
                    
                    # Объединяем детей всех объектов группы
                    merged_children_raw: List[Dict] = []
                    for obj in objects:
                        merged_children_raw.extend(obj.get("children", []))
                    
                    # Определяем базовое имя для группы
                    name_counter = Counter(names)
                    base_name = name_counter.most_common(1)[0][0] if name_counter else names[0]
                    
                    grouped_node = {
                        "name": base_name,
                        "names": sorted(list(set(names))),
                        "count": len(objects),
                        "components": rebuild_components_from_signature(sig),
                        "children": format_grouped_list(merged_children_raw)
                    }
                    
                    formatted_children.append(grouped_node)
            
            return formatted_children
        
        return {
            "scene_name": hierarchy.get("sceneName", "Unknown"),
            "root_objects": format_grouped_list(hierarchy.get("rootObjects", [])),
            "total_objects": hierarchy.get("totalObjects", 0)
        }
    
    def _format_object(self, obj: Dict) -> Dict:
        """Форматирует отдельный объект"""
        return {
            "name": obj.get("name"),
            "path": obj.get("path"),
            "active": obj.get("active", True),
            "components": obj.get("components", []) or [],
            "children": self._format_children_with_grouping(obj.get("children", []))
        }
    
    def _format_children_with_grouping(self, children_raw: List[Dict]) -> List[Dict]:
        """Форматирует детей с группировкой по сходству имен"""
        if not children_raw:
            return []
        
        def name_similarity(name1: str, name2: str) -> float:
            if name1 == name2:
                return 1.0
            return difflib.SequenceMatcher(None, name1.lower(), name2.lower()).ratio()
        
        def components_signature(obj: Dict) -> tuple:
            comps = obj.get("components", []) or []
            if not isinstance(comps, list):
                return tuple()
            counter = Counter([str(c) for c in comps])
            return tuple(sorted(counter.items()))
        
        def rebuild_components_from_signature(sig: tuple) -> List[str]:
            comps: List[str] = []
            for type_name, cnt in sig:
                comps.extend([type_name] * int(cnt))
            return comps
        
        def get_parent_path(obj_path: str) -> str:
            if not obj_path or "/" not in obj_path:
                return ""
            return "/".join(obj_path.split("/")[:-1])
        
        # Группировка по точному совпадению имени и компонентов
        exact_groups: Dict[tuple, List[Dict]] = {}
        for ch in children_raw:
            key = (ch.get("name"), components_signature(ch))
            exact_groups.setdefault(key, []).append(ch)
        
        # Разделение на одиночные и групповые объекты
        single_objects = []
        grouped_objects = []
        
        for (name, sig), items in exact_groups.items():
            if len(items) == 1:
                single_objects.append((name, sig, items[0]))
            else:
                # Точно одинаковые объекты - группируем
                merged_children_raw: List[Dict] = []
                for it in items:
                    merged_children_raw.extend(it.get("children", []))

                path_list = [it.get("path") for it in items if it.get("path")]
                pc = Counter(path_list)

                grouped_node = {
                    "name": name,
                    "count": len(items),
                    "components": rebuild_components_from_signature(sig),
                    "children": self._format_children_with_grouping(merged_children_raw)
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
        
        # Группировка одиночных объектов по сходству имен
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
        
        # Формирование окончательного результата
        formatted_children: List[Dict] = []
        
        # Добавляем уже сгруппированные объекты
        formatted_children.extend(grouped_objects)
        
        # Обрабатываем группы по сходству
        for group in similarity_groups:
            if len(group) == 1:
                # Одиночный объект
                name, sig, obj = group[0]
                formatted_children.append(self._format_object(obj))
            else:
                # Группа объектов с похожими именами
                names = [item[0] for item in group]
                objects = [item[2] for item in group]
                sig = group[0][1]  # Компоненты одинаковые
                
                # Объединяем детей всех объектов группы
                merged_children_raw: List[Dict] = []
                for obj in objects:
                    merged_children_raw.extend(obj.get("children", []))
                
                # Определяем базовое имя для группы
                name_counter = Counter(names)
                base_name = name_counter.most_common(1)[0][0] if name_counter else names[0]
                
                grouped_node = {
                    "name": base_name,
                    "names": sorted(list(set(names))),  # Список уникальных имен
                    "count": len(objects),
                    "components": rebuild_components_from_signature(sig),
                    "children": self._format_children_with_grouping(merged_children_raw)
                }
                
                formatted_children.append(grouped_node)
        
        return formatted_children