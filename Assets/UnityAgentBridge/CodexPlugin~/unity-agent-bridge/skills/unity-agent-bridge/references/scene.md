# Сцена, префабы и Assets

## Объекты и компоненты

```text
tree [--path|--root <ветка>] [--depth <уровни>]
object-children --path <объект>
object-find [--name "камера игрока"] [--component <тип>] [--path <ветка>] [--limit 10]
object-info --path <объект> [--component <тип> [--component-index 0] [--property <поле>]]
component-suggest --path <объект> --component "Rigidbody" --query "твёрдое тело"
component-add --path <объект> --component <тип> [--set <property> <JSON>]
component-modify --path <объект> --component <тип> [--component-index 0] --set <property> <JSON>
component-remove --path <объект> --component <тип> [--component-index 0]
component-action --path <объект> --component <тип> [--component-index 0] --action <id из object-info>
object-picker --path <объект> --component <тип> [--component-index 0] --property <поле>
object-create --parent <сцена-или-объект> --name <имя>
object-delete --path <объект>
object-duplicate --path <объект>
object-move --path <объект> --destination <новый-родитель> [--index <порядок>]
object-rename --path <объект> --name <новое-имя>
object-active --path <объект> --active true|false
object-tag --path <объект-или-prefаб> --tag <имя-или-Untagged>
scene-save
refresh
```

В `--set` строку можно передать без JSON-кавычек; несколько полей — одним `--set поле1=значение поле2=значение`; составное значение — как `--set поле x=1 y=2`. Префикс `m_` у верхнего поля можно опустить. UnityEvent задаётся целиком: `--set onClick '[{"target":"/Scene/Button","method":"Run"}]'`; необязательны `mode`, `state` и соответствующее поле `objectArgument|intArgument|floatArgument|stringArgument|boolArgument`. AnimationCurve принимает массив ключей `[{"time":0,"value":0},{"time":1,"value":1}]`. Остальные списки менять через `<поле>.Array.size` и `<поле>.Array.data[<индекс>]`. Для Object Reference использовать `references[].value` из `object-info`; `object-picker` работает только с такими полями.

Индексы в path нужны только для различения объектов с одинаковыми именами.

## Префабы, сцены и Assets

```text
prefabs [--path <папка-в-Assets>]
prefab-save [--path <объект>] [--prefab <asset-path>]
prefab-apply --path <экземпляр> [--component <тип> [--component-index 0] [--property <поле>]]
prefab-instantiate --prefab <имя-или-asset-path> --parent <сцена-или-объект>
prefab-revert --path <экземпляр> [--component <тип> [--component-index 0] [--property <поле>]]
prefab-open --prefab <имя-или-asset-path>
prefab-close
scenes-search --query "тестовая сцена"
scene-open --scene <имя-или-asset-path>
creation-templates [--query "C# script"]
asset-create --template <точное-имя-шаблона> --path <Assets/...>
asset-info --path <Assets/...> [--property <поле>]
asset-action --path <Assets/...> --action <id из asset-info>
asset-modify --path <Assets/...> --set property=JSON
asset-reimport --path <Assets/...>
asset-move --path <Assets/...> --destination <Assets/...>
asset-delete --path <Assets/...>
asset-object-picker --path <Assets/...> --property <поле>
```

`prefab-open` и `scene-open` сохраняют текущее содержимое перед переходом. После `prefab-open` команды сцены работают с префабом. Относительные пути префабов считаются от стандартной папки; явный путь может указывать на любой префаб внутри `Assets`. В Prefab Mode `prefab-save` без `--path` сохраняет корень. При частичном `prefab-apply` внешние ссылки сцены пропускаются и остаются override экземпляра.

Пути из `asset-info` вида `asset:<property>` и `importer:<property>` передавать без изменений; элемент массива читается как `<поле>.Array.data[i]`. `asset-create` с шаблоном `C# Script` возвращает `componentType` после регистрации MonoBehaviour в Unity.
