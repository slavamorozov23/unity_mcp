from test_config import get_client, print_result

client = get_client()

print("--- TEST 5: ASSETS AGENT ---")

# 1. Поиск шаблона
res = client.execute_command({
    "action": "search_templates",
    "params": {"query": "Material"}
})
print_result("Search Template 'Material'", res)

# 2. Создание ассета
asset_path = "Assets/TestMaterial.mat"
res = client.execute_command({
    "action": "create_asset",
    "params": {
        "template_name": "Material",
        "target_path": "Assets",
        "file_name": "TestMaterial"
    }
})
print_result("Create Asset", res)

# 3. Инфо об ассете
res = client.execute_command({
    "action": "get_asset_info",
    "params": {"asset_path": asset_path}
})
print_result("Get Asset Info", res)