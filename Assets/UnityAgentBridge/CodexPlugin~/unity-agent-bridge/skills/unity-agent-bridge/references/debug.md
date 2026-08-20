# Диагностика и Play Mode

```text
health
version
logs [--query "ошибка загрузки"] [--level Error|Assert|Warning|Log|Exception] [--since-minutes N] [--limit N] [--stacktrace]
logs --clear
status
play start|stop
game-resolutions
game-resolution --width <ширина> --height <высота>
```

`logs` сворачивает одинаковые записи среди последних 100 и возвращает `count`; stack trace включается только через `--stacktrace`.
