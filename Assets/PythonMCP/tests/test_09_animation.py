from test_config import get_client, print_result

client = get_client()

print("--- TEST 9: ANIMATION AGENT ---")

# 1. Создаем .anim файл
anim_path = "Assets/TestAnim.anim"
client.execute_command({
    "action": "create_asset",
    "params": {
        "template_name": "Animation",
        "target_path": "Assets",
        "file_name": "TestAnim"
    }
})

# 2. Добавляем ключ (изменяем позицию X)
res = client.execute_command({
    "action": "modify_anim",
    "params": {
        "sub_action": "add_key",
        "anim_path": anim_path,
        "object_path": "", # Root
        "component_type": "Transform",
        "property_name": "m_LocalPosition.x",
        "time": 1.0,
        "value": 5.0
    }
})
print_result("Add Keyframe", res)

# 3. Получаем таблицу (Timeline)
res = client.execute_command({
    "action": "get_anim_info",
    "params": {"anim_path": anim_path}
})
print_result("Get Anim Timeline", res)
if res.get("success"):
    print(f"   Properties found: {len(res['data'].get('propertiesList', []))}")