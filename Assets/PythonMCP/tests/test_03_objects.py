from test_config import get_client, print_result
import time

client = get_client()
obj_name = "Test_Lifecycle_Obj"
renamed_name = "Test_Lifecycle_Renamed"

print("--- TEST 3: OBJECTS AGENT ---")

# 1. Создать
res = client.execute_command({"action": "create_object", "params": {"name": obj_name}})
print_result("Create Object", res)

# 2. Переименовать
res = client.execute_command({
    "action": "rename_object",
    "params": {"path": obj_name, "new_name": renamed_name}
})
print_result("Rename Object", res)

# 3. Выключить (Set Active False)
res = client.execute_command({
    "action": "set_active",
    "params": {"path": renamed_name, "active": False}
})
print_result("Set Active False", res)

# 4. Сбросить (Reset)
res = client.execute_command({
    "action": "reset_object",
    "params": {"object_path": renamed_name}
})
print_result("Reset Object", res)

# 5. Удалить
res = client.execute_command({"action": "delete_object", "params": {"object_path": renamed_name}})
print_result("Delete Object", res)