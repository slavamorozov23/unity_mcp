"""GetHierarchyModule - minimal clean implementation.

Uses HTTPClient from modules.common.
"""

from ..common import HTTPClient
from typing import Optional, Dict, Any


class GetHierarchyModule:
    def __init__(self, base_url: str):
        self.client = HTTPClient(base_url)

    def execute(self, params: Optional[Dict] = None) -> Dict[str, Any]:
        """Fetches scene hierarchy from Unity and applies optional filtering."""
        resp = self.client.get("/scene", action="get_hierarchy")
        if not resp.get("success"):
            return {"success": False, "action": "get_hierarchy", "data": None, "error": resp.get("error")}

        data = resp.get("data")
        if params and isinstance(params, dict):
            from_path = params.get("from_path") or params.get("path") or params.get("path_contains")
            if from_path:
                node = self._find_node_by_path(data, from_path)
                if node:
                    total = self._count_nodes(node)
                    return {"success": True, "action": "get_hierarchy", "data": {"sceneName": data.get("sceneName", "Unknown"), "rootObjects": [node], "totalObjects": total}, "error": None}

        return {"success": True, "action": "get_hierarchy", "data": data, "error": None}

    def _find_node_by_path(self, hierarchy: Dict[str, Any], needle: str) -> Optional[Dict[str, Any]]:
        """Find first node whose 'path' contains needle (case-insensitive)."""
        if not hierarchy:
            return None

        def find_in_node(node: Dict[str, Any], needle_lower: str):
            p = node.get("path", "")
            if isinstance(p, str) and needle_lower in p.lower():
                return node
            for child in node.get("children", []) or []:
                if not isinstance(child, dict):
                    continue
                found = find_in_node(child, needle_lower)
                if found:
                    return found
            return None

        needle_lower = needle.lower()
        for root in (hierarchy.get("rootObjects", []) or []):
            if not isinstance(root, dict):
                continue
            f = find_in_node(root, needle_lower)
            if f:
                return f
        return None

    def _count_nodes(self, node: Dict[str, Any]) -> int:
        """Count nodes in subtree (including node)."""
        if not isinstance(node, dict):
            return 0
        total = 1
        for child in node.get("children", []) or []:
            total += self._count_nodes(child)
        return total