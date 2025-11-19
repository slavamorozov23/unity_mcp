from test_config import get_client, print_result

client = get_client()

print("--- TEST 1: GENERAL MODULE ---")

# 1. Получить иерархию
res = client.execute_command({"action": "get_hierarchy"})
print_result("Get Hierarchy", res)

# 2. NLP Поиск (ищем камеру, которая есть в любой сцене)
res = client.execute_command({
    "action": "nlp_search",
    "params": {"query": "camera"}
})
print_result("NLP Search 'camera'", res)

if res.get("success"):
    matches = res["data"]["matches"]
    print(f"   Found objects: {len(matches['objects'])}")