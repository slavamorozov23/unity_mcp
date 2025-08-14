import os
import tempfile
import json
from datetime import datetime
from typing import Dict

MAX_LOG_CHARS = 10000000
LOG_FILENAME = "unity_api_client.log.txt"

class LoggingModule:
    def __init__(self):
        self._clear_log_file()
    
    def get_log_file_path(self) -> str:
        """Публичный метод: получить путь к лог-файлу"""
        return self._get_log_path()
    
    def log_structured(self, request_payload: Dict, response_payload: Dict) -> None:
        """Логирует структурированный запрос и ответ"""
        try:
            ts = datetime.utcnow().isoformat() + "Z"
            entry = (
                f"[{ts}] REQUEST:\n" +
                json.dumps(request_payload, ensure_ascii=False, indent=2) +
                "\nRESPONSE:\n" +
                json.dumps(response_payload, ensure_ascii=False, indent=2) +
                "\n\n"
            )
            log_path = self._get_log_path()
            try:
                with open(log_path, "r", encoding="utf-8") as f:
                    existing = f.read()
            except FileNotFoundError:
                existing = ""
            combined = existing + entry
            if len(combined) > MAX_LOG_CHARS:
                combined = combined[-MAX_LOG_CHARS:]
            with open(log_path, "w", encoding="utf-8") as f:
                f.write(combined)
        except Exception:
            # Логирование ошибок логгера не должно мешать основной работе
            pass
    
    def _get_log_path(self) -> str:
        """Получает путь к файлу логов"""
        return os.path.join(tempfile.gettempdir(), LOG_FILENAME)
    
    def _clear_log_file(self) -> None:
        """Удаляет временный файл логов перед началом работы"""
        try:
            log_path = self._get_log_path()
            if os.path.exists(log_path):
                os.remove(log_path)
        except Exception:
            # Ошибки при удалении лог-файла не должны мешать основной работе
            pass