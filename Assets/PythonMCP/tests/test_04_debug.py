from test_config import get_client, print_result

client = get_client()

print("--- TEST 4: DEBUG AGENT ---")

# 1. Статус сцены
res = client.execute_command({"action": "scene_status"})
print_result("Get Scene Status", res)

# 2. Поиск логов
res = client.execute_command({
    "action": "search_logs",
    "params": {"query": "Scene API Server started"}
})
print_result("Search Logs", res)

# 3. Умный снимок (Smart Snapshot)
# Снимем камеру саму себя или любой объект
res = client.execute_command({
    "action": "get_snapshot",
    "params": {
        "targetPaths": ["Main Camera"], 
        "width": 640, 
        "height": 360,
        "distance": 5.0
    }
})
print_result("Smart Snapshot (Raycast optimized)", res)
if res.get("success"):
    print(f"   Image Base64 Length: {len(res['data'].get('base64Image', ''))}")