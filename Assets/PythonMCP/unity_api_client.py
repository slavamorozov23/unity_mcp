import requests
import json
from typing import Dict, List, Optional, Any

class UnitySceneAPI:
    def __init__(self, host: str = "localhost", port: int = 8081):
        self.base_url = f"http://{host}:{port}"
        
    def get_scene_hierarchy(self) -> Optional[Dict]:
        try:
            response = requests.get(f"{self.base_url}/scene")
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            print(f"Error getting scene hierarchy: {e}")
            return None
    
    def open_scene(self, scene_path: str) -> bool:
        try:
            response = requests.post(f"{self.base_url}/scene/open", 
                                   json={"scenePath": scene_path})
            response.raise_for_status()
            result = response.json()
            return result.get("success", False)
        except requests.exceptions.RequestException as e:
            print(f"Error opening scene: {e}")
            return False
    
    def get_build_scenes(self) -> Optional[List[Dict]]:
        try:
            response = requests.get(f"{self.base_url}/build/scenes")
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            print(f"Error getting build scenes: {e}")
            return None
    
    def add_scene_to_build(self, scene_path: str) -> bool:
        try:
            response = requests.post(f"{self.base_url}/build/scenes/add", 
                                   json={"scenePath": scene_path})
            response.raise_for_status()
            result = response.json()
            return result.get("success", False)
        except requests.exceptions.RequestException as e:
            print(f"Error adding scene to build: {e}")
            return False
    
    def remove_scene_from_build(self, scene_identifier: str) -> bool:
        try:
            response = requests.delete(f"{self.base_url}/build/scenes/remove", 
                                     json={"sceneIdentifier": scene_identifier})
            response.raise_for_status()
            result = response.json()
            return result.get("success", False)
        except requests.exceptions.RequestException as e:
            print(f"Error removing scene from build: {e}")
            return False
    
    def create_object(self, name: str = "GameObject", parent_path: str = "") -> Optional[str]:
        try:
            response = requests.post(f"{self.base_url}/objects/create", 
                                   json={"name": name, "parentPath": parent_path})
            response.raise_for_status()
            result = response.json()
            if result.get("success"):
                return result.get("path")
            return None
        except requests.exceptions.RequestException as e:
            print(f"Error creating object: {e}")
            return None
    
    def delete_object(self, object_path: str) -> bool:
        try:
            response = requests.delete(f"{self.base_url}/objects/delete", 
                                     json={"path": object_path})
            response.raise_for_status()
            result = response.json()
            return result.get("success", False)
        except requests.exceptions.RequestException as e:
            print(f"Error deleting object: {e}")
            return False
    
    def get_object_components(self, object_path: str) -> Optional[Dict]:
        try:
            response = requests.get(f"{self.base_url}/objects/components", 
                                  params={"path": object_path})
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            print(f"Error getting object components: {e}")
            return None
    
    def add_component(self, object_path: str, component_type: str) -> bool:
        try:
            response = requests.post(f"{self.base_url}/objects/components/add", 
                                   json={"path": object_path, "componentType": component_type})
            response.raise_for_status()
            result = response.json()
            return result.get("success", False)
        except requests.exceptions.RequestException as e:
            print(f"Error adding component: {e}")
            return False
    
    def modify_component(self, object_path: str, component_type: str, properties: Dict[str, Any]) -> bool:
        try:
            response = requests.put(f"{self.base_url}/objects/components/modify", 
                                  json={
                                      "path": object_path, 
                                      "componentType": component_type, 
                                      "properties": properties
                                  })
            response.raise_for_status()
            result = response.json()
            return result.get("success", False)
        except requests.exceptions.RequestException as e:
            print(f"Error modifying component: {e}")
            return False
    
    def remove_component(self, object_path: str, component_type: str) -> bool:
        try:
            response = requests.delete(f"{self.base_url}/objects/components/remove", 
                                     json={"path": object_path, "componentType": component_type})
            response.raise_for_status()
            result = response.json()
            return result.get("success", False)
        except requests.exceptions.RequestException as e:
            print(f"Error removing component: {e}")
            return False
    
    def move_object(self, object_path: str, x: float, y: float, z: float) -> bool:
        return self.modify_component(object_path, "Transform", {
            "position": {"x": x, "y": y, "z": z}
        })
    
    def rotate_object(self, object_path: str, x: float, y: float, z: float, w: float) -> bool:
        return self.modify_component(object_path, "Transform", {
            "rotation": {"x": x, "y": y, "z": z, "w": w}
        })
    
    def scale_object(self, object_path: str, x: float, y: float, z: float) -> bool:
        return self.modify_component(object_path, "Transform", {
            "localScale": {"x": x, "y": y, "z": z}
        })
    
    def find_objects_by_name(self, name: str) -> List[str]:
        hierarchy = self.get_scene_hierarchy()
        if not hierarchy:
            return []
        
        paths = []
        
        def search_recursive(obj_data):
            if name.lower() in obj_data["name"].lower():
                paths.append(obj_data["path"])
            
            for child in obj_data.get("children", []):
                search_recursive(child)
        
        for root_obj in hierarchy.get("rootObjects", []):
            search_recursive(root_obj)
        
        return paths
    
    def print_hierarchy(self, obj: Dict = None, level: int = 0):
        if obj is None:
            data = self.get_scene_hierarchy()
            if data is None:
                print("Failed to get scene hierarchy")
                return
            print(f"Scene: {data['sceneName']} ({data['scenePath']})")
            for root_obj in data['rootObjects']:
                self.print_hierarchy(root_obj, 0)
            return
        
        indent = "  " * level
        print(f"{indent}{obj['name']} (Path: {obj['path']}, Active: {obj['active']})")
        
        for child in obj['children']:
            self.print_hierarchy(child, level + 1)
    
    def print_object_info(self, object_path: str):
        components_data = self.get_object_components(object_path)
        if not components_data:
            print(f"Object not found: {object_path}")
            return
        
        print(f"Object: {object_path}")
        print("Components:")
        
        for comp in components_data.get("components", []):
            print(f"  {comp['name']}:")
            for prop_name, prop_value in comp.get("properties", {}).items():
                print(f"    {prop_name}: {prop_value}")

    def print_build_scenes(self):
        scenes = self.get_build_scenes()
        if not scenes:
            print("No scenes in build or failed to get build scenes")
            return
        
        print("Scenes in Build Settings:")
        if isinstance(scenes, dict) and 'scenes' in scenes:
            scenes = scenes['scenes']
        
        for i, scene in enumerate(scenes):
            if isinstance(scene, dict):
                name = scene.get('name', 'Unknown')
                path = scene.get('path', 'Unknown')
                enabled = scene.get('enabled', False)
                print(f"  {i}: {name} - {path} {'(enabled)' if enabled else '(disabled)'}")
            else:
                print(f"  {i}: {scene}")

def example_workflow():
    unity = UnitySceneAPI()
    
    print("=== Unity Scene Management Example ===")
    
    print("\n1. Getting current scene hierarchy:")
    unity.print_hierarchy()
    
    print("\n2. Getting build scenes:")
    unity.print_build_scenes()
    
    print("\n3. Creating new object:")
    new_obj_path = unity.create_object("TestObject")
    if new_obj_path:
        print(f"Created object: {new_obj_path}")
    
    print("\n4. Adding Rigidbody component:")
    if unity.add_component(new_obj_path, "Rigidbody"):
        print("Rigidbody component added")
    
    print("\n5. Getting object components:")
    unity.print_object_info(new_obj_path)
    
    print("\n6. Moving object to position (5, 10, 0):")
    if unity.move_object(new_obj_path, 5, 10, 0):
        print("Object moved")
    
    print("\n7. Modifying Rigidbody mass:")
    if unity.modify_component(new_obj_path, "Rigidbody", {"mass": 2.5}):
        print("Rigidbody mass modified")
    
    print("\n8. Updated object info:")
    unity.print_object_info(new_obj_path)
    
    print("\n9. Removing Rigidbody component:")
    if unity.remove_component(new_obj_path, "Rigidbody"):
        print("Rigidbody component removed")
    
    print("\n10. Deleting object:")
    if unity.delete_object(new_obj_path):
        print("Object deleted")
    
    print("\n11. Final scene hierarchy:")
    unity.print_hierarchy()

def build_scenes_example():
    unity = UnitySceneAPI()
    
    print("=== Build Scenes Management ===")
    
    print("\n1. Current build scenes:")
    unity.print_build_scenes()
    
    print("\n2. Adding scene to build (if exists):")
    if unity.add_scene_to_build("Assets/Scenes/TestScene.unity"):
        print("Scene added to build")
    else:
        print("Failed to add scene to build")
    
    print("\n3. Updated build scenes:")
    unity.print_build_scenes()
    
    print("\n4. Removing scene from build:")
    if unity.remove_scene_from_build("TestScene"):
        print("Scene removed from build")
    else:
        print("Failed to remove scene from build")
    
    print("\n5. Final build scenes:")
    unity.print_build_scenes()

def complex_example():
    unity = UnitySceneAPI()
    
    print("=== Complex Scene Manipulation ===")
    
    parent_path = unity.create_object("ParentObject")
    child1_path = unity.create_object("Child1", parent_path)
    child2_path = unity.create_object("Child2", parent_path)
    
    unity.add_component(child1_path, "MeshRenderer")
    unity.add_component(child1_path, "BoxCollider")
    unity.add_component(child2_path, "Light")
    
    unity.move_object(child1_path, 2, 0, 0)
    unity.move_object(child2_path, -2, 0, 0)
    
    print("Created complex hierarchy:")
    unity.print_hierarchy()
    
    cameras = unity.find_objects_by_name("camera")
    print(f"\nFound cameras: {cameras}")
    
    for camera_path in cameras:
        unity.print_object_info(camera_path)

if __name__ == "__main__":
    example_workflow()
    print("\n" + "="*50 + "\n")
    build_scenes_example()
    print("\n" + "="*50 + "\n")
    complex_example()