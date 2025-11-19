import requests
import json
from typing import Dict, List

class GameViewSnapshotModule:
    def __init__(self, base_url: str):
        self.base_url = base_url

    def execute(self, target_paths: List[str], distance: float = 10.0, width: int = 1280, height: int = 720) -> Dict:
        """
        Делает 'Умный снимок':
        1. Unity находит центр группы объектов target_paths
        2. Unity прогоняет Raycast'ы с разных углов (Physics.SyncTransforms вызывается автоматически)
        3. Unity выбирает лучший ракурс и делает фото
        """
        try:
            data = {
                "targetPaths": target_paths,
                "distance": distance,
                "width": width, 
                "height": height,
                "angularSteps": 12,
                "minElevation": 20,
                "maxElevation": 60
            }
            
            response = requests.post(
                f"{self.base_url}/debug/snapshot",
                json=data
            )
            response.raise_for_status()
            return response.json()
        except Exception as e:
            return {"success": False, "error": str(e)}