import requests
import json
from typing import Dict

class CreateFromTemplateModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self, template_name: str, target_path: str, file_name: str = "") -> Dict:
        """Создает ассет из шаблона"""
        try:
            data = {
                "templateName": template_name,
                "targetPath": target_path,
                "fileName": file_name
            }
            response = requests.post(
                f"{self.base_url}/templates/create",
                json=data
            )
            response.raise_for_status()
            return response.json()
        except Exception as e:
            return {"success": False, "error": str(e)}