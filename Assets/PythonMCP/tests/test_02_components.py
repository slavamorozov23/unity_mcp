from test_config import get_client, print_result

client = get_client()
obj_name = "Test_Components_Obj"

print("--- TEST 2: COMPONENTS AGENT ---")

# Setup
client.execute_command({"action": "create_object", "params": {"name": obj_name}})

# 1. Добавить компонент (BoxCollider)
res = client.execute_command({
    "action": "add_component",
    "params": {"object_path": obj_name, "component_type": "BoxCollider"}
})
print_result("Add Component (BoxCollider)", res)

# 2. Изменить компонент (Transform)
res = client.execute_command({
    "action": "modify_component",
    "params": {
        "object_path": obj_name,
        "component_type": "Transform",
        "properties": {"position": {"x": 10, "y": 5, "z": 0}}
    }
})
print_result("Modify Component (Transform)", res)

# 3. Получить компоненты
res = client.execute_command({
    "action": "get_components",
    "params": {"object_path": obj_name}
})
print_result("Get Components", res)

# 4. Удалить компонент (BoxCollider)
res = client.execute_command({
    "action": "remove_component",
    "params": {"object_path": obj_name, "component_type": "BoxCollider"}
})
print_result("Remove Component (BoxCollider)", res)

# 5. Object Picker Options (10 вариантов для окрестности объекта)
res = client.execute_command({
    "action": "object_picker_options",
    "params": {"object_path": obj_name}
})
print_result("Object Picker Options (from Test_Components_Obj)", res)

# 6. Component Property Variants (Transform.m_LocalPosition)
res = client.execute_command({
    "action": "component_variants",
    "params": {
        "object_path": obj_name,
        "component_type": "Transform",
        "param_name": "m_LocalPosition"
    }
})
print_result("Component Param Variants (Transform.m_LocalPosition)", res)

# Cleanup
client.execute_command({"action": "delete_object", "params": {"object_path": obj_name}})