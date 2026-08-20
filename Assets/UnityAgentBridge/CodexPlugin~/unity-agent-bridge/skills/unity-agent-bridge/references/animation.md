# Анимации

## AnimationClip

`animation-table|clip-info --path PATH.anim` или `animation-table --query ТЕКСТ [--path Assets/ПАПКА]`.

`animation-properties --path ОБЪЕКТ|PATH.anim [--query ТЕКСТ]` — доступные Property. Возвращаемый `id` передавать без изменений.

Для поворота в градусах использовать `localEulerAnglesRaw.x/y/z`.

`animation-clip-create --path Assets/Animations/ИМЯ.anim`

`animation-clip-delete --path PATH.anim`

`animation-property get|create|modify|delete --path PATH.anim --object-path PATH_ВНУТРИ_КЛИПА --property ID_ИЛИ_TYPE/PROPERTY [--keys JSON]`

`--keys` — массив ключей. Ключ задаётся `frame` или `time`; для float нужен `value`, для object — `reference` с путём Asset. Наклоны: `inTangent`/`outTangent` или `inSlope`/`outSlope`. `modify` заменяет кривую переданным набором. Удаление без `--keys` удаляет Property целиком.

`animation-clip-setting --path PATH.anim --parameter "Loop Time|Loop Pose|Cycle Offset|..."`

`animation-clip-setting --path PATH.anim --set "Loop Time" true`

## Animator

`animator-info --path ОБЪЕКТ`, `animator-info --controller CONTROLLER` или `animator-info --query ТЕКСТ`; остальные объекты ищет `object-find`.

`animator-component create|delete --path PATH [--controller ASSET_PATH]`

Если Animator уже есть, контроллер назначать через `animator-controller-assign`.

`animator-controller-assign assign|detach --path PATH [--controller ASSET_PATH]`

`animator-motions --path PATH|--controller ASSET_PATH|--query ТЕКСТ`

`animator-graph --path PATH|--controller ASSET_PATH`

`animator-controller create --name ИМЯ --path ASSETS_ПАПКА`

`animator-controller delete --controller ASSET_PATH`

`animator-state create|modify|delete --controller PATH --layer ИМЯ --state ИМЯ [--state-machine PATH] [--motion PATH_КЛИПА_ИЛИ_BLENDTREE] [--set ПАРАМЕТР=JSON]`

`animator-state-motion assign|detach --controller PATH --layer ИМЯ --state ИМЯ [--state-machine PATH] [--motion PATH_КЛИПА_ИЛИ_BLENDTREE]`

`animator-transition create|modify|delete --controller PATH --layer ИМЯ --from STATE|AnyState --to STATE|Exit [--state-machine PATH] [--transition-index N] [--set ПАРАМЕТР=JSON] [--conditions JSON]`

`animator-parameter create|modify|delete --controller PATH --name ИМЯ [--type Float|Int|Bool|Trigger] [--value ЗНАЧЕНИЕ] [--new-name ИМЯ]`

Без `--value` создаётся `0` или `false`.

`animator-layer create|modify|delete --controller PATH --layer ИМЯ [--set ПАРАМЕТР=JSON]`

`animator-state-machine create|modify|delete --controller PATH --layer ИМЯ --name ИМЯ [--parent PATH] [--new-name ИМЯ]`

`animator-blend-tree create|modify|delete --controller PATH --layer ИМЯ --state ИМЯ --name ИМЯ [--state-machine PATH] [--settings JSON]`

`animator-control --path PATH [--state STATE] [--layer ИМЯ_ИЛИ_INDEX] [--set PARAMETER=JSON]` — Play Mode.

`animator-runtime-state --path PATH` — State, переходы, время и Parameters в Play Mode.

Несколько свойств можно передать одним `--set свойство1=значение свойство2=значение`.

Допустимые `--set`: State — `name`, `speed`, `speedParameter`, `timeParameter`, `mirror`, `mirrorParameter`, `cycleOffset`, `cycleOffsetParameter`, `tag`, `writeDefaultValues`, `iKOnFeet`; Transition — `hasExitTime`, `exitTime`, `duration`, `offset`, `hasFixedDuration`, `interruptionSource`, `orderedInterruption`, `canTransitionToSelf`, `mute`, `solo`; Layer — `name`, `defaultWeight`, `avatarMask`, `blendingMode`, `iKPass`, `syncedLayerIndex`, `syncedLayerAffectsTiming`.

`--conditions`: массив `{ "mode", "parameter", "threshold" }`. `--settings`: объект с `blendType`, `blendParameter`, `blendParameterY`, `useAutomaticThresholds`, `minThreshold`, `maxThreshold`, `children`; дочерний элемент содержит `motion`, `threshold`, `x`, `y`, `timeScale`, `cycleOffset`, `directBlendParameter`, `mirror`.
