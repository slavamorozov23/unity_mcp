"""NLPSearchModule - basic fuzzy/substr search over scene hierarchy."""

import difflib
from ..common import HTTPClient
from typing import Dict, Any

class NLPSearchModule:
    def __init__(self, base_url: str):
        self.client = HTTPClient(base_url)

    def execute(self, query: str, max_results: int = 50) -> Dict[str, Any]:
        """Perform simple NLP-like fuzzy and substring search across scene."""
        if not query:
            return {"success": False, "action": "nlp_search", "data": None, "error": "query is required"}

        resp = self.client.get("/scene", action="nlp_search:fetch_scene")
        if not resp.get("success"):
            return {"success": False, "action": "nlp_search", "data": None, "error": resp.get("error")}

        scene = resp.get("data") or {}
        q = query.lower()

        matches = {"objects": [], "components": []}

        def traverse(node, path=""):
            name = node.get("name", "")
            curr = f"{path}/{name}" if path else name
            name_lower = name.lower()

            name_score = difflib.SequenceMatcher(None, q, name_lower).ratio() if name_lower else 0.0
            if q in name_lower or name_score >= 0.6:
                matches["objects"].append({"name": name, "path": curr, "score": round(name_score, 3)})

            for comp in node.get("components", []) or []:
                comp_name = comp.get("name") if isinstance(comp, dict) else str(comp)
                comp_lower = (comp_name or "").lower()
                comp_score = difflib.SequenceMatcher(None, q, comp_lower).ratio() if comp_lower else 0.0
                if q in comp_lower or comp_score >= 0.6:
                    matches["components"].append({"object_path": curr, "component": comp_name, "score": round(comp_score, 3)})

            for child in node.get("children", []) or []:
                if isinstance(child, dict):
                    traverse(child, curr)

        for root in (scene.get("rootObjects", []) or []):
            if isinstance(root, dict):
                traverse(root)

        matches["objects"] = sorted(matches["objects"], key=lambda x: x.get("score", 0), reverse=True)[:max_results]
        matches["components"] = sorted(matches["components"], key=lambda x: x.get("score", 0), reverse=True)[:max_results]

        return {"success": True, "action": "nlp_search", "data": {"query": query, "matches": matches}, "error": None}