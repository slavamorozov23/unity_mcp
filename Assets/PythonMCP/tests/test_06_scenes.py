from test_config import get_client, print_result

client = get_client()

print("--- TEST 6: SCENE AGENT ---")

# 1. Получить сцены в билде
res = client.execute_command({"action": "get_build_scenes"})
print_result("Get Build Scenes", res)

# Открытие сцены тестировать опасно, так как это сбросит текущее состояние,
# но эндпоинт 'open_scene' существует.