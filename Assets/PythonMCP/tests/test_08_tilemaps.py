from test_config import get_client, print_result

client = get_client()

print("--- TEST 8: TILEMAP AGENT ---")

# 1. Рисование (это создаст Grid и Tilemap автоматически)
# Пытаемся стереть тайл (null), так как для рисования нужен существующий Tile Asset
res = client.execute_command({
    "action": "paint_tile",
    "params": {
        "tilemap_name": "TestTilemap",
        "tile_name": "", # Пусто = ластик
        "x": 0, "y": 0
    }
})
print_result("Paint Tile (Erase/Init Grid)", res)

# 2. Получить список тайлмапов
res = client.execute_command({"action": "get_tilemaps"})
print_result("Get Tilemaps", res)

# 3. Умный снимок тайлмапа (Markdown + Z-check)
res = client.execute_command({
    "action": "get_tilemap_snapshot",
    "params": {
        "tilemap_name": "TestTilemap",
        "width": 5, "height": 5,
        "overlayObjectPaths": ["Main Camera"] # Проверим, попадет ли камера в отчет
    }
})
print_result("Tilemap Snapshot (Markdown)", res)
if res.get("success"):
    print("   Markdown Table Snippet:")
    print(res["data"]["markdown"][:100] + "...")