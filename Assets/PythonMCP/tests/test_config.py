import sys
import os
import json

# Добавляем родительскую директорию в путь, чтобы видеть unity_api_client_modular
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from unity_api_client_modular import UnitySceneAPI

def get_client():
    return UnitySceneAPI()

def print_result(test_name, result):
    ok = result.get("success")
    prefix = "✅ [PASS]" if ok else "❌ [FAIL]"
    print(f"{prefix} {test_name}")
    print(json.dumps(result, indent=2, ensure_ascii=False))