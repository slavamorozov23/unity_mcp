---
name: unity-agent-bridge
description: "Управлять Unity через локальный Unity Agent Bridge: сценами, объектами, компонентами, префабами и Assets; Package Manager, InputManager, AnimationClip и Animator Controller; диагностикой, Play Mode и кадрами сцены или игры. Использовать для чтения и изменения Unity-проекта, анимаций и действий мышью/клавиатурой в Game View. Не использовать для тайлмапов, произвольного редактирования кода и терминала."
---

# Unity Agent Bridge

CLI-команды выполнять через `scripts/uab.ps1`; в справках указана часть команды после имени скрипта. MCP содержит `game_actions`, `scene_screenshot` и `sprite_editor`; остальные возможности плагина выполняются через CLI. Не определять состав Bridge по списку MCP-инструментов. Читать только нужную справку:

- [scene.md](references/scene.md) — сцены, объекты, компоненты, префабы и Assets;
- [project.md](references/project.md) — структура и настройки проекта;
- [animation.md](references/animation.md) — AnimationClip и Animator;
- [debug.md](references/debug.md) — состояние Unity, логи, Play Mode и разрешение;
- [game-control.md](references/game-control.md) — снимки сцены и управление игрой;
- [scope-and-placement.md](references/scope-and-placement.md) — применимость широкого редактирования и позиционирование.

Если MCP-инструменты недоступны, выполнить `scripts/uab.ps1 mcp-status` и сообщить его `error`, не угадывая причину.

## Правила

1. Выполнять `tree` только когда нужен путь или иерархия.
2. Не выполнять `object-info` при точном path и однозначной операции.
3. После изменения проверять только изменённое значение. `tree` повторять лишь ради обновлённой иерархии.
4. НЛП-команды возвращают до 10 результатов локальной модели. Балл задаёт порядок, а не достоверность. Не подменять модель другим поиском.
5. Не изменять `.unity`, `.prefab`, `.asset` и `.meta` через файловые инструменты.
6. При одинаковых компонентах указывать `--component-index`.
