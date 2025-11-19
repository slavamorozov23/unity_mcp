from test_config import get_client, print_result

client = get_client()

print("--- TEST 10: INPUT AGENT ---")

# 1. Получить константы (подсказки)
res = client.execute_command({
    "action": "manage_input",
    "params": {"sub_action": "constants"}
})
print_result("Get Input Constants", res)

# 2. Создать новую ось
res = client.execute_command({
    "action": "manage_input",
    "params": {
        "sub_action": "create",
        "name": "Test_Fire_Axis",
        "pos_btn": "space",
        "neg_btn": "",
        "type": 0 # KeyOrMouseButton
    }
})
print_result("Create Input Axis", res)

# 3. Удалить ось (чистка)
res = client.execute_command({
    "action": "manage_input",
    "params": {
        "sub_action": "delete",
        "name": "Test_Fire_Axis"
    }
})
print_result("Delete Input Axis", res)