"""Minimal logging module used by the Python client."""

import os
import json
import tempfile
from datetime import datetime
from typing import Dict, Any

LOG_FILENAME = "unity_api_client.log.txt"
MAX_LOG_CHARS = 10_000_000

class LoggingModule:
    def __init__(self):
        self._log_path = os.path.join(tempfile.gettempdir(), LOG_FILENAME)
        try:
            if os.path.exists(self._log_path):
                os.remove(self._log_path)
        except Exception:
            pass

    def get_log_file_path(self) -> str:
        return self._log_path

    def log_structured(self, request_payload: Dict[str, Any], response_payload: Dict[str, Any]) -> None:
        try:
            ts = datetime.utcnow().isoformat() + "Z"
            entry = f"[{ts}] REQUEST:\\n{json.dumps(request_payload, ensure_ascii=False, indent=2)}\\nRESPONSE:\\n{json.dumps(response_payload, ensure_ascii=False, indent=2)}\\n\\n"
            try:
                with open(self._log_path, "r", encoding="utf-8") as f:
                    existing = f.read()
            except FileNotFoundError:
                existing = ""
            combined = existing + entry
            if len(combined) > MAX_LOG_CHARS:
                combined = combined[-MAX_LOG_CHARS:]
            with open(self._log_path, "w", encoding="utf-8") as f:
                f.write(combined)
        except Exception:
            pass