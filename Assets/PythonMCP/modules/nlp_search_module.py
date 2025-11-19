import requests
import json
from typing import Dict

class NLPSearchModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def execute(self, query: str) -> Dict:
        """Выполняет "НЛП поиск" на стороне Python по полным спискам от Unity"""
        try:
            if not query:
                return {
                    "success": False,
                    "action": "nlp_search",
                    "error": "query is required"
                }
            
            # Получаем полную иерархию и компоненты для простого NLP-поиска
            scene_resp = requests.get(f"{self.base_url}/scene")
            scene_resp.raise_for_status()
            scene = scene_resp.json()
            
            # Простая эвристика: ищем объекты и компоненты, в именах которых встречается запрос
            query_lower = query.lower()
            matches = {
                "objects": [],
                "components": []
            }
            
            def traverse(node, path=""):
                name = node.get("name", "")
                current_path = f"{path}/{name}" if path else name
                if query_lower in name.lower():
                    matches["objects"].append({"name": name, "path": current_path})
                for comp in node.get("components", []):
                    comp_name = comp.get("name") or comp.get("type") or "Component"
                    if comp_name and query_lower in comp_name.lower():
                        matches["components"].append({
                            "object_path": current_path,
                            "component": comp_name
                        })
                for child in node.get("children", []) or []:
                    if isinstance(child, dict):
                        traverse(child, current_path)
            
            for root in scene.get("rootObjects", []) or []:
                if isinstance(root, dict):
                    traverse(root)
            
            return {
                "success": True,
                "action": "nlp_search",
                "data": {
                    "query": query,
                    "matches": matches
                }
            }
            
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "nlp_search",
                "error": f"Request error: {str(e)}"
            }
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "action": "nlp_search",
                "error": f"JSON decode error: {str(e)}"
            }