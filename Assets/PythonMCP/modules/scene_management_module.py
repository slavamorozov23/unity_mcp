import requests
import json
from typing import Dict, Optional

class SceneManagementModule:
    def __init__(self, base_url: str):
        self.base_url = base_url
    
    def open_scene(self, scene_path: str) -> Dict:
        """Открывает указанную сцену"""
        try:
            if not scene_path:
                return {
                    "success": False,
                    "action": "open_scene",
                    "error": "scene_path is required"
                }
            
            response = requests.post(
                f"{self.base_url}/scene/open", 
                json={"scenePath": scene_path}
            )
            response.raise_for_status()
            result = response.json()
            
            return {
                "success": result.get("success", False),
                "action": "open_scene",
                "data": result if result.get("success") else None,
                "error": result.get("error")
            }
            
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "open_scene",
                "error": f"Request error: {str(e)}"
            }
    
    def get_build_scenes(self) -> Dict:
        """Получает список сцен в настройках сборки"""
        try:
            response = requests.get(f"{self.base_url}/build/scenes")
            response.raise_for_status()
            result = response.json()
            
            return {
                "success": True,
                "action": "get_build_scenes",
                "data": result,
                "error": result.get("error") if "error" in result else None
            }
            
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "get_build_scenes",
                "error": f"Request error: {str(e)}"
            }
    
    def add_scene_to_build(self, scene_path: str) -> Dict:
        """Добавляет сцену в настройки сборки"""
        try:
            if not scene_path:
                return {
                    "success": False,
                    "action": "add_scene_to_build",
                    "error": "scene_path is required"
                }
            
            response = requests.post(
                f"{self.base_url}/build/scenes/add", 
                json={"scenePath": scene_path}
            )
            response.raise_for_status()
            result = response.json()
            
            return {
                "success": result.get("success", False),
                "action": "add_scene_to_build",
                "data": result if result.get("success") else None,
                "error": result.get("error")
            }
            
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "add_scene_to_build",
                "error": f"Request error: {str(e)}"
            }
    
    def remove_scene_from_build(self, scene_path: str) -> Dict:
        """Удаляет сцену из настроек сборки"""
        try:
            if not scene_path:
                return {
                    "success": False,
                    "action": "remove_scene_from_build",
                    "error": "scene_path is required"
                }
            
            response = requests.delete(
                f"{self.base_url}/build/scenes/remove", 
                json={"scenePath": scene_path}
            )
            response.raise_for_status()
            result = response.json()
            
            return {
                "success": result.get("success", False),
                "action": "remove_scene_from_build",
                "data": result if result.get("success") else None,
                "error": result.get("error")
            }
            
        except requests.exceptions.RequestException as e:
            return {
                "success": False,
                "action": "remove_scene_from_build",
                "error": f"Request error: {str(e)}"
            }