"""HTTP helper for Unity Scene API modules."""

import requests
import json
from typing import Optional, Any, Dict


class HTTPClient:
    """
    Minimal, clean HTTP client wrapper used by module implementations.

    Methods return a standardized dict:
      {"success": bool, "action": str, "data": Any, "error": Optional[str]}
    """

    def __init__(self, base_url: str, timeout: int = 5):
        self.base_url = base_url.rstrip("/")
        self.timeout = timeout

    def _url(self, path: str) -> str:
        if not path.startswith("/"):
            path = "/" + path
        return f"{self.base_url}{path}"

    def request(self, method: str, path: str, params: Optional[dict] = None,
                json_body: Optional[dict] = None, action: Optional[str] = None) -> Dict[str, Any]:
        url = self._url(path)
        action = action or f"{method.lower()}:{path}"
        try:
            resp = requests.request(method, url, params=params, json=json_body, timeout=self.timeout)
            resp.raise_for_status()
            try:
                data = resp.json()
            except json.JSONDecodeError:
                data = resp.text
            return {"success": True, "action": action, "data": data, "error": None}
        except requests.exceptions.RequestException as e:
            return {"success": False, "action": action, "data": None, "error": f"Request error: {str(e)}"}
        except Exception as e:
            return {"success": False, "action": action, "data": None, "error": f"Unexpected error: {str(e)}"}

    def get(self, path: str, params: Optional[dict] = None, action: Optional[str] = None):
        return self.request("GET", path, params=params, action=action)

    def post(self, path: str, json_body: Optional[dict] = None, action: Optional[str] = None):
        return self.request("POST", path, json_body=json_body, action=action)

    def put(self, path: str, json_body: Optional[dict] = None, action: Optional[str] = None):
        return self.request("PUT", path, json_body=json_body, action=action)

    def delete(self, path: str, json_body: Optional[dict] = None, action: Optional[str] = None):
        return self.request("DELETE", path, json_body=json_body, action=action)