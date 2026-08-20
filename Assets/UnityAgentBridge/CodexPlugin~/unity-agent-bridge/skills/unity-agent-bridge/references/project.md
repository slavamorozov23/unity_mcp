# Структура и настройки проекта

Стандартные пути: `Assets/Scripts`, `Assets/Scenes`, `Assets/Animations`, `Assets/Tiles`, `Assets/UnityAgentBridge/Prefabs`, `ProjectSettings`.

## Package Manager

```text
packages
packages-search --query "новая система ввода"
package-install --name <имя> [--version <версия>]
package-update --name <имя> [--version <версия>]
package-remove --name <имя>
```

## InputManager

```text
input-axes
input-axis-create --name <имя> [--set <property> <JSON>]
input-axis-delete --name <имя>
```

Поля Axis: `descriptiveName`, `descriptiveNegativeName`, `negativeButton`, `positiveButton`, `altNegativeButton`, `altPositiveButton`, `gravity`, `dead`, `sensitivity`, `snap`, `invert`, `type`, `axis`, `joyNum`.

`type`: `0` KeyOrMouseButton, `1` MouseMovement, `2` JoystickAxis. `axis`: `0..27`. `joyNum`: `0` все, `1..11` конкретный джойстик.

При стандартизации сцены использовать только набор компонентов, явно заданный проектом.
