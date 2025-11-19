from test_config import get_client, print_result

client = get_client()

print("--- TEST 11: PICKERS (HELPER AGENT) ---")

# 1. Object Picker (10 вариантов)
res = client.execute_command({
    "action": "object_picker_options",
    "params": {"object_path": ""} # Корень
})
print_result("Object Picker Options", res)

# 2. Asset Picker (10 вариантов для материала)
res = client.execute_command({
    "action": "asset_picker_options",
    "params": {
        "asset_path": "Assets", # Контекст (не обязательно валидный путь к ассету для общего поиска)
        "property_name": "Material" 
    }
})
print_result("Asset Picker Options", res)

# 3. Component Variants (для параметра)
# Пытаемся узнать варианты для Transform.localPosition
res = client.execute_command({
    "action": "component_variants",
    "params": {
        "object_path": "Main Camera",
        "component_type": "Transform",
        "param_name": "m_LocalPosition"
    }
})
print_result("Component Param Variants", res)