import os

# Скрипт для удаления зависшего мета-файла
meta_path = os.path.join("Assets", "Editor", "SceneAPI", "Modules", "SceneAPI.meta")

if os.path.exists(meta_path):
    try:
        os.remove(meta_path)
        print(f"Successfully deleted: {meta_path}")
    except Exception as e:
        print(f"Error deleting {meta_path}: {e}")
else:
    print(f"File not found (already deleted): {meta_path}")