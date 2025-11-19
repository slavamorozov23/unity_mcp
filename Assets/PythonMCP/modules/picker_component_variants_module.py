import requests
import json
from typing import Dict, Any

class PickerComponentVariantsModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self, object_path: str, component_type: str, param_name: str, query: str = "") -> Dict:
        """Получить 10 вариантов значений для параметра компонента (на сцене)"""
        try:
            if not all([object_path, component_type, param_name]):
                return {
                    "success": False,
                    "action": "picker_component_variants",
                    "error": "object_path, component_type and param_name are required"
                }

            # Получаем компоненты объекта
            resp = requests.get(f"{self.base_url}/objects/components", params={"path": object_path})
            resp.raise_for_status()
            data = resp.json()

            comps = data.get("components", {})
            comp = comps.get(component_type) or self._find_component_case_insensitive(comps, component_type)
            if not isinstance(comp, dict):
                return {"success": False, "action": "picker_component_variants", "error": "Component not found"}

            # Сгенерировать варианты на основе типа параметра (простая эвристика)
            prop = comp.get(param_name) or self._find_key_case_insensitive(comp, param_name)
            options = []
            if isinstance(prop, dict) and all(k in prop for k in ("x","y","z")):
                # Vector3-подобные предложения
                options = [
                    {"x":0,"y":0,"z":0},
                    {"x":1,"y":1,"z":1},
                    {"x":0,"y":1,"z":0},
                    {"x":-1,"y":0,"z":0},
                    {"x":0,"y":0,"z":1},
                    {"x":5,"y":0,"z":0},
                    {"x":0,"y":5,"z":0},
                    {"x":0,"y":0,"z":5},
                    {"x":-5,"y":0,"z":0},
                    {"x":0,"y":-5,"z":0}
                ]
            elif isinstance(prop, bool):
                options = [True, False]
            elif isinstance(prop, (int, float)):
                base = float(prop)
                options = [base + d for d in [-10,-5,-1,0,1,5,10,25,50,100]]
            elif isinstance(prop, str):
                options = [prop, "", query or prop]
            else:
                # По умолчанию пустые/универсальные
                options = [None] * 10

            # ограничить до 10
            options = options[:10]

            return {"success": True, "action": "picker_component_variants", "data": {"options": options}}

        except requests.exceptions.RequestException as e:
            return {"success": False, "action": "picker_component_variants", "error": f"Request error: {str(e)}"}
        except json.JSONDecodeError as e:
            return {"success": False, "action": "picker_component_variants", "error": f"JSON decode error: {str(e)}"}

    def _find_key_case_insensitive(self, d: Dict[str, Any], key: str):
        if not isinstance(d, dict):
            return None
        for k, v in d.items():
            if k.lower() == key.lower():
                return v
        return None

    def _find_component_case_insensitive(self, comps: Dict[str, Any], name: str):
        for k, v in comps.items():
            if k.lower() == name.lower():
                return v
        return None