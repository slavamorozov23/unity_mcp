import sys
import os
import json

# Добавляем родительскую директорию в путь, чтобы видеть unity_api_client_modular
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from unity_api_client_modular import UnitySceneAPI

def get_client():
    return UnitySceneAPI()

def print_result(test_name, result):
    if result.get("success"):
        print(f"✅ [PASS] {test_name}")
        # print(json.dumps(result, indent=2)) # Раскомментируйте для дебага
    else:
        print(f"❌ [FAIL] {test_name}")
        print(f"   Error: {result.get('error')}")