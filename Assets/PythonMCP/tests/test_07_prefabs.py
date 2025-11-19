from test_config import get_client, print_result

client = get_client()
obj_name = "Prefab_Source"
prefab_name = "TestPrefab"

print("--- TEST 7: PREFAB AGENT ---")

# 1. Создаем исходник
client.execute_command({"action": "create_object", "params": {"name": obj_name}})

# 2. Сохраняем как префаб
res = client.execute_command({
    "action": "save_as_prefab",
    "params": {
        "object_path": obj_name,
        "prefab_name": prefab_name,
        "target_folder": "Assets"
    }
})
print_result("Save as Prefab", res)
prefab_path = res.get("data", {}).get("prefabPath", "")

# 3. Инстанцируем
if prefab_path:
    res = client.execute_command({
        "action": "create_from_prefab",
        "params": {
            "prefab_path": prefab_path,
            "object_name": "Instance_From_Test"
        }
    })
    print_result("Instantiate Prefab", res)

# Cleanup (удаляем объекты со сцены, файл остается в Assets для истории или ручной чистки)
client.execute_command({"action": "delete_object", "params": {"object_path": obj_name}})
client.execute_command({"action": "delete_object", "params": {"object_path": "Instance_From_Test"}})