import json
from typing import Dict, List, Optional, Any

from modules import (
    GetHierarchyModule,
    GetComponentsModule,
    CreateObjectModule,
    DeleteObjectModule,
    ModifyComponentModule,
    AddComponentModule,
    RemoveComponentModule,
    FindObjectsModule,
    SceneManagementModule,
    LoggingModule
)

class UnitySceneAPI:
    def __init__(self, host: str = "localhost", port: int = 8080):
        self.base_url = f"http://{host}:{port}"
        
        # Инициализация модулей
        self.hierarchy_module = GetHierarchyModule(self.base_url)
        self.components_module = GetComponentsModule(self.base_url)
        self.create_object_module = CreateObjectModule(self.base_url)
        self.delete_object_module = DeleteObjectModule(self.base_url)
        self.modify_component_module = ModifyComponentModule(self.base_url)
        self.add_component_module = AddComponentModule(self.base_url)
        self.remove_component_module = RemoveComponentModule(self.base_url)
        self.find_objects_module = FindObjectsModule(self.base_url)
        self.scene_management_module = SceneManagementModule(self.base_url)
        self.logging_module = LoggingModule()
    
    # Методы для обратной совместимости
    def get_scene_hierarchy(self) -> Optional[Dict]:
        """Получает иерархию сцены"""
        result = self.hierarchy_module.execute()
        return result.get("data") if result.get("success") else {"error": result.get("error")}
    
    def get_object_components(self, object_path: str) -> Optional[Dict]:
        """Получает компоненты объекта"""
        result = self.components_module.execute(object_path)
        return result.get("data", {}).get("components") if result.get("success") else {"error": result.get("error")}
    
    def create_object(self, name: str = "GameObject", parent_path: str = "") -> Dict:
        """Создает новый объект"""
        result = self.create_object_module.execute(name, parent_path)
        return result.get("data") if result.get("success") else {"success": False, "error": result.get("error")}
    
    def delete_object(self, object_path: str) -> Dict:
        """Удаляет объект"""
        result = self.delete_object_module.execute(object_path)
        return result.get("data") if result.get("success") else {"success": False, "error": result.get("error")}
    
    def modify_component(self, object_path: str, component_type: str, properties: Dict[str, Any]) -> Dict:
        """Модифицирует компонент"""
        result = self.modify_component_module.execute(object_path, component_type, properties)
        return result.get("data") if result.get("success") else {"success": False, "error": result.get("error")}
    
    def add_component(self, object_path: str, component_type: str) -> Dict:
        """Добавляет компонент"""
        result = self.add_component_module.execute(object_path, component_type)
        return result.get("data") if result.get("success") else {"success": False, "error": result.get("error")}
    
    def remove_component(self, object_path: str, component_type: str) -> Dict:
        """Удаляет компонент"""
        result = self.remove_component_module.execute(object_path, component_type)
        return result.get("data") if result.get("success") else {"success": False, "error": result.get("error")}
    
    def find_objects_by_name(self, name: str) -> Dict:
        """Находит объекты по имени"""
        result = self.find_objects_module.execute(name)
        return result.get("data") if result.get("success") else {"error": result.get("error")}
    
    def open_scene(self, scene_path: str) -> Dict:
        """Открывает сцену"""
        result = self.scene_management_module.open_scene(scene_path)
        return result.get("data") if result.get("success") else {"success": False, "error": result.get("error")}
    
    def get_build_scenes(self) -> Optional[Dict]:
        """Получает список сцен в билде"""
        result = self.scene_management_module.get_build_scenes()
        return result.get("data") if result.get("success") else {"error": result.get("error")}
    
    def add_scene_to_build(self, scene_path: str) -> Dict:
        """Добавляет сцену в билд"""
        result = self.scene_management_module.add_scene_to_build(scene_path)
        return result.get("data") if result.get("success") else {"success": False, "error": result.get("error")}
    
    def remove_scene_from_build(self, scene_path: str) -> Dict:
        """Удаляет сцену из билда"""
        result = self.scene_management_module.remove_scene_from_build(scene_path)
        return result.get("data") if result.get("success") else {"success": False, "error": result.get("error")}
    
    # Вспомогательные методы для трансформации
    def move_object(self, object_path: str, x: float, y: float, z: float) -> Dict:
        """Перемещает объект"""
        return self.modify_component_module.move_object(object_path, x, y, z)
    
    def rotate_object(self, object_path: str, x: float, y: float, z: float, w: float) -> Dict:
        """Поворачивает объект"""
        return self.modify_component_module.rotate_object(object_path, x, y, z, w)
    
    def scale_object(self, object_path: str, x: float, y: float, z: float) -> Dict:
        """Масштабирует объект"""
        return self.modify_component_module.scale_object(object_path, x, y, z)
    
    # JSON-focused getters для совместимости с инструментами
    def get_hierarchy_json(self) -> Optional[Dict]:
        return self.get_scene_hierarchy()
    
    def get_build_scenes_json(self) -> Optional[Dict]:
        return self.get_build_scenes()
    
    def get_object_info_json(self, object_path: str) -> Optional[Dict]:
        return self.get_object_components(object_path)
    
    # Методы логирования
    def get_log_file_path(self) -> str:
        """Получить путь к лог-файлу"""
        return self.logging_module.get_log_file_path()
    
    # Структурированные JSON методы
    def execute_command(self, command: Dict) -> Dict:
        """
        Выполняет структурированную команду и возвращает структурированный ответ
        Формат запроса: {"action": "get_hierarchy|get_components|create_object|...", "params": {...}}
        """
        try:
            action = command.get("action")
            params = command.get("params", {})
            
            result = None
            
            if action == "get_hierarchy":
                result = self.hierarchy_module.execute(params)
            elif action == "get_components":
                object_path = params.get("object_path")
                if not object_path:
                    result = {"success": False, "action": action, "error": "object_path is required"}
                else:
                    result = self.components_module.execute(object_path)
            elif action == "create_object":
                name = params.get("name", "GameObject")
                parent_path = params.get("parent_path", "")
                result = self.create_object_module.execute(name, parent_path)
            elif action == "delete_object":
                object_path = params.get("object_path")
                if not object_path:
                    result = {"success": False, "action": action, "error": "object_path is required"}
                else:
                    result = self.delete_object_module.execute(object_path)
            elif action == "modify_component":
                object_path = params.get("object_path")
                component_type = params.get("component_type")
                properties = params.get("properties", {})
                
                if not all([object_path, component_type]):
                    result = {"success": False, "action": action, "error": "object_path and component_type are required"}
                else:
                    result = self.modify_component_module.execute(object_path, component_type, properties)
            elif action == "add_component":
                object_path = params.get("object_path")
                component_type = params.get("component_type")
                
                if not all([object_path, component_type]):
                    result = {"success": False, "action": action, "error": "object_path and component_type are required"}
                else:
                    result = self.add_component_module.execute(object_path, component_type)
            elif action == "remove_component":
                object_path = params.get("object_path")
                component_type = params.get("component_type")
                
                if not all([object_path, component_type]):
                    result = {"success": False, "action": action, "error": "object_path and component_type are required"}
                else:
                    result = self.remove_component_module.execute(object_path, component_type)
            elif action == "find_objects":
                name = params.get("name")
                if not name:
                    result = {"success": False, "action": action, "error": "name is required"}
                else:
                    result = self.find_objects_module.execute(name)
            elif action == "open_scene":
                scene_path = params.get("scene_path")
                if not scene_path:
                    result = {"success": False, "action": action, "error": "scene_path is required"}
                else:
                    result = self.scene_management_module.open_scene(scene_path)
            elif action == "get_build_scenes":
                result = self.scene_management_module.get_build_scenes()
            elif action == "add_scene_to_build":
                scene_path = params.get("scene_path")
                if not scene_path:
                    result = {"success": False, "action": action, "error": "scene_path is required"}
                else:
                    result = self.scene_management_module.add_scene_to_build(scene_path)
            elif action == "remove_scene_from_build":
                scene_path = params.get("scene_path")
                if not scene_path:
                    result = {"success": False, "action": action, "error": "scene_path is required"}
                else:
                    result = self.scene_management_module.remove_scene_from_build(scene_path)
            else:
                result = {"success": False, "action": action, "error": f"Unknown action: {action}"}
            
            # Логируем запрос и ответ
            self.logging_module.log_structured(command, result)
            return result
                
        except Exception as e:
            result = {"success": False, "action": command.get("action", "unknown"), "error": str(e)}
            self.logging_module.log_structured(command, result)
            return result

def wait_for_enter(message: str = "Нажмите Enter для продолжения..."):
    """Ожидает нажатия Enter с настраиваемым сообщением"""
    input(message)

# Пример использования
def main():
    unity = UnitySceneAPI()
    
    print("=== Тест 1: Получение иерархии ===")
    hierarchy_request = {"action": "get_hierarchy"}
    hierarchy_response = unity.execute_command(hierarchy_request)
    print(json.dumps(hierarchy_response, indent=2, ensure_ascii=False))
    wait_for_enter()
    
    print("\n=== Тест 2: Получение компонентов ===")
    components_request = {
        "action": "get_components",
        "params": {"object_path": "Main Camera"}
    }
    components_response = unity.execute_command(components_request)
    print(json.dumps(components_response, indent=2, ensure_ascii=False))
    wait_for_enter()
    
    print("\n=== Тест 3: Создание объекта ===")
    create_request = {
        "action": "create_object",
        "params": {"name": "TestObject", "parent_path": ""}
    }
    create_response = unity.execute_command(create_request)
    print(json.dumps(create_response, indent=2, ensure_ascii=False))
    wait_for_enter("Объект создан. Нажмите Enter для продолжения...")
    
    print("\n=== Тест 4: Поиск объектов ===")
    find_request = {
        "action": "find_objects",
        "params": {"name": "Camera"}
    }
    find_response = unity.execute_command(find_request)
    print(json.dumps(find_response, indent=2, ensure_ascii=False))
    wait_for_enter()
    
    print("\n=== Тест 5: Добавление компонента ===")
    add_component_request = {
        "action": "add_component",
        "params": {"object_path": "TestObject", "component_type": "Rigidbody"}
    }
    add_component_response = unity.execute_command(add_component_request)
    print(json.dumps(add_component_response, indent=2, ensure_ascii=False))
    wait_for_enter("Компонент добавлен. Нажмите Enter для продолжения...")
    
    print("\n=== Тест 6: Модификация компонента ===")
    modify_request = {
        "action": "modify_component",
        "params": {
            "object_path": "TestObject",
            "component_type": "Transform",
            "properties": {"m_LocalPosition": {"x": 1.0, "y": 2.0, "z": 3.0}}
        }
    }
    modify_response = unity.execute_command(modify_request)
    print(json.dumps(modify_response, indent=2, ensure_ascii=False))
    wait_for_enter("Позиция объекта изменена. Нажмите Enter для продолжения...")
    
    print("\n=== Тест 7: Получение сцен в билде ===")
    build_scenes_request = {"action": "get_build_scenes"}
    build_scenes_response = unity.execute_command(build_scenes_request)
    print(json.dumps(build_scenes_response, indent=2, ensure_ascii=False))
    wait_for_enter()
    
    print("\n=== Тест 8: Удаление компонента ===")
    remove_component_request = {
        "action": "remove_component",
        "params": {"object_path": "TestObject", "component_type": "Rigidbody"}
    }
    remove_component_response = unity.execute_command(remove_component_request)
    print(json.dumps(remove_component_response, indent=2, ensure_ascii=False))
    wait_for_enter("Компонент удален. Нажмите Enter для очистки (удаления объекта)...")
    
    print("\n=== Тест 9: Удаление объекта ===")
    delete_request = {
        "action": "delete_object",
        "params": {"object_path": "TestObject"}
    }
    delete_response = unity.execute_command(delete_request)
    print(json.dumps(delete_response, indent=2, ensure_ascii=False))
    wait_for_enter("Объект удален. Нажмите Enter для продолжения...")
    
    print("\n=== Тест 10: Фильтрованная иерархия ===")
    subtree_request = {
        "action": "get_hierarchy",
        "params": {"from_path": "Enemies"}
    }
    subtree_response = unity.execute_command(subtree_request)
    print("Subtree (from_path='Enemies'):")
    print(json.dumps(subtree_response, indent=2, ensure_ascii=False))
    wait_for_enter("Все тесты завершены. Нажмите Enter для завершения...")

    print("\nLog saved to:", unity.get_log_file_path())

if __name__ == "__main__":
    main()