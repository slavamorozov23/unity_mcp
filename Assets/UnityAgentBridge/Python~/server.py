from __future__ import annotations

import argparse
import json
import math
import os
import re
import threading
import time
import traceback
import urllib.parse
import uuid
from datetime import datetime, timedelta, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any

import numpy as np
import wordninja
from fastembed import TextEmbedding
from PIL import Image, ImageDraw


MODEL_NAME = "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"
MODEL_READY_FILE = ".unity-agent-bridge-model-ready"
MAX_BODY_BYTES = 32 * 1024 * 1024
RESULT_COUNT = 10
GAME_BUTTONS = {"left", "right", "middle"}
LANDSCAPE_SCREENSHOT_LIMIT = (1280, 720)
PORTRAIT_SCREENSHOT_LIMIT = (720, 1280)


class UnityGameInput:
    def __init__(self, unity: Any, frame_width: int, frame_height: int) -> None:
        self.unity = unity
        self.frame_width = positive_integer(frame_width, "frame width")
        self.frame_height = positive_integer(frame_height, "frame height")
        self.held_keys: list[str] = []
        self.held_button: str | None = None
        self.last_point = (0.0, 0.0)

    def perform(self, actions: list[dict[str, Any]]) -> None:
        for action in actions:
            kind = action["action"]
            if kind == "click":
                self.click(action, 1)
            elif kind == "double_click":
                self.click(action, 2)
            elif kind == "hover":
                self.point_action("hover", action, include_button=False)
            elif kind == "drag":
                self.drag(action)
            elif kind == "scroll":
                self.point_action(
                    "scroll",
                    action,
                    include_button=False,
                    extra={"deltaX": action["scrollX"], "deltaY": action["scrollY"]},
                )
            elif kind == "press_key":
                self.press_key(action["key"], action["duration"])
            elif kind == "key_down":
                self.dispatch("key-down", name=action["key"])
                self.held_keys.append(action["key"])
            elif kind == "key_up":
                self.dispatch("key-up", name=action["key"])
                if action["key"] in self.held_keys:
                    self.held_keys.remove(action["key"])
            elif kind == "type_text":
                self.dispatch("type-text", name=action["text"])
            elif kind == "wait":
                previous_scale = required_string(
                    self.unity.call("multiply-game-time-scale", action="10"),
                    "message",
                )
                try:
                    if float(previous_scale) == 0.0:
                        time.sleep(action["seconds"])
                        continue
                    started = float(required_string(self.unity.call("get-game-time"), "message"))
                    deadline = time.monotonic() + max(5.0, action["seconds"] + 5.0)
                    while True:
                        current = float(required_string(self.unity.call("get-game-time"), "message"))
                        if current - started >= action["seconds"]:
                            break
                        if time.monotonic() >= deadline:
                            raise TimeoutError("Unity game time did not advance.")
                        time.sleep(0.01)
                finally:
                    self.unity.call("set-game-time-scale", action=previous_scale)
            else:
                raise RuntimeError(f"Validated action was not implemented: {kind}")

    def release_all(self) -> None:
        if self.held_button is not None:
            self.dispatch(
                "mouse-up",
                values={"x": self.last_point[0], "y": self.last_point[1], "button": self.held_button},
            )
            self.held_button = None
        for key in reversed(self.held_keys):
            self.dispatch("key-up", name=key)
        self.held_keys.clear()

    def point_action(
        self,
        action: str,
        source: dict[str, Any],
        include_button: bool = True,
        extra: dict[str, Any] | None = None,
    ) -> None:
        values: dict[str, Any] = {"x": source["x"], "y": source["y"]}
        if include_button:
            values["button"] = source.get("button", "left")
        if extra:
            values.update(extra)
        self.dispatch(action, values=values)

    def click(self, action: dict[str, Any], count: int) -> None:
        button = action.get("button", "left")
        point = {"x": action["x"], "y": action["y"]}
        self.dispatch("hover", values=point)
        for index in range(count):
            values = {"x": action["x"], "y": action["y"], "button": button, "clickCount": index + 1}
            self.dispatch("mouse-down", values=values)
            self.held_button = button
            self.last_point = (action["x"], action["y"])
            time.sleep(0.2)
            self.dispatch("mouse-up", values=values)
            self.held_button = None
            if index + 1 < count:
                time.sleep(0.1)

    def press_key(self, chord: str, duration: float) -> None:
        keys = [part.strip() for part in chord.split("+")]
        if any(not key for key in keys):
            raise ValueError("A key chord contains an empty key.")
        pressed: list[str] = []
        try:
            for key in keys:
                self.dispatch("key-down", name=key)
                pressed.append(key)
            time.sleep(duration)
        finally:
            for key in reversed(pressed):
                self.dispatch("key-up", name=key)

    def drag(self, action: dict[str, Any]) -> None:
        button = action.get("button", "left")
        start = (action["x"], action["y"])
        target = (action["targetX"], action["targetY"])
        duration = action["duration"]
        self.dispatch("hover", values={"x": start[0], "y": start[1]})
        self.dispatch("mouse-down", values={"x": start[0], "y": start[1], "button": button})
        self.held_button = button
        self.last_point = start
        try:
            time.sleep(0.2)
            started = time.monotonic()
            steps = max(1, round(duration * 30))
            for step in range(1, steps + 1):
                target_time = started + duration * step / steps
                delay = target_time - time.monotonic()
                if delay > 0:
                    time.sleep(delay)
                progress = step / steps
                self.last_point = (
                    start[0] + (target[0] - start[0]) * progress,
                    start[1] + (target[1] - start[1]) * progress,
                )
                self.dispatch(
                    "mouse-drag",
                    values={"x": self.last_point[0], "y": self.last_point[1], "button": button},
                )
        finally:
            self.dispatch(
                "mouse-up",
                values={"x": self.last_point[0], "y": self.last_point[1], "button": button},
            )
            self.held_button = None

    def dispatch(self, action: str, name: str | None = None, values: dict[str, Any] | None = None) -> None:
        entries = {
            "frameWidth": self.frame_width,
            "frameHeight": self.frame_height,
            **(values or {}),
        }
        self.unity.call(
            "dispatch-game-input",
            action=action,
            name=name,
            values=[{"path": key, "value": str(value)} for key, value in entries.items()],
        )


def write_atomic(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False), encoding="utf-8")
    temporary.replace(path)


def unlink_when_available(path: Path, timeout: float = 5.0) -> None:
    deadline = time.monotonic() + timeout
    while True:
        try:
            path.unlink(missing_ok=True)
            return
        except PermissionError:
            if time.monotonic() >= deadline:
                raise
            time.sleep(0.05)


def resize_game_screenshot(path: Path, width: int, height: int) -> tuple[int, int]:
    with Image.open(path) as source:
        source.load()
        if source.size != (width, height):
            raise RuntimeError("Unity screenshot size does not match its metadata.")
        limit = LANDSCAPE_SCREENSHOT_LIMIT if width >= height else PORTRAIT_SCREENSHOT_LIMIT
        if width <= limit[0] and height <= limit[1]:
            return width, height
        resized = source.convert("RGB")

    try:
        resized.thumbnail(limit, Image.Resampling.LANCZOS)
        temporary = path.with_suffix(path.suffix + ".tmp")
        resized.save(temporary, format="PNG", compress_level=6)
        temporary.replace(path)
        return resized.size
    finally:
        resized.close()


def discovery_file(name: str) -> Path:
    local_app_data = os.environ.get("LOCALAPPDATA")
    if not local_app_data:
        raise RuntimeError("LOCALAPPDATA is unavailable.")
    return Path(local_app_data) / "UnityAgentBridge" / name


def clear_active_project(active_project_file: Path, project: Path) -> None:
    try:
        value = json.loads(active_project_file.read_text(encoding="utf-8"))
        active_project = Path(value["projectRoot"]).resolve()
    except (FileNotFoundError, KeyError, TypeError, ValueError, json.JSONDecodeError, OSError):
        return
    if active_project == project:
        unlink_when_available(active_project_file)


def load_model(model_cache: Path) -> TextEmbedding:
    model_cache.mkdir(parents=True, exist_ok=True)
    ready_file = model_cache / MODEL_READY_FILE
    installed = ready_file.is_file() and ready_file.read_text(encoding="utf-8").strip() == MODEL_NAME
    model = TextEmbedding(
        model_name=MODEL_NAME,
        cache_dir=str(model_cache),
        local_files_only=installed,
    )
    next(model.embed(["Unity Agent Bridge"]))
    if not installed:
        temporary = ready_file.with_suffix(".tmp")
        temporary.write_text(MODEL_NAME, encoding="utf-8")
        temporary.replace(ready_file)
    return model


class UnityRpc:
    def __init__(self, project: Path) -> None:
        self.requests = project / "Library" / "UnityAgentBridge" / "Requests"
        self.responses = project / "Library" / "UnityAgentBridge" / "Responses"

    def call(self, command: str, _timeout_seconds: float = 120.0, **arguments: Any) -> dict[str, Any]:
        request_id = f"{time.time_ns():020d}-{uuid.uuid4().hex}"
        payload = {"id": request_id, "command": command, **arguments}
        self.requests.mkdir(parents=True, exist_ok=True)
        self.responses.mkdir(parents=True, exist_ok=True)
        request_path = self.requests / f"{request_id}.request.json"
        response_path = self.responses / f"{request_id}.response.json"
        cancellation_path = request_path.with_suffix(request_path.suffix + ".cancel")
        write_atomic(request_path, payload)

        deadline = time.monotonic() + _timeout_seconds
        while time.monotonic() < deadline:
            if response_path.is_file():
                result = json.loads(response_path.read_text(encoding="utf-8"))
                response_path.unlink()
                if not result.get("ok", False):
                    raise RuntimeError(result.get("error", "Unity command failed without an error message."))
                return result
            time.sleep(0.025)
        cancellation_path.write_text("cancelled", encoding="utf-8")
        response_path.unlink(missing_ok=True)
        raise TimeoutError(f"Unity did not become ready for the queued request within {_timeout_seconds:g} seconds; the request was cancelled.")


class Operations:
    def __init__(self, project: Path, model_cache: Path) -> None:
        self.project = project.resolve()
        self.plugin_version, self.plugin_revision = project_plugin_identity(project)
        self.unity = UnityRpc(project)
        self.model = load_model(model_cache)
        self.queue_condition = threading.Condition()
        self.next_ticket = 0
        self.serving_ticket = 0
        self.last_game_frame_size: tuple[int, int] | None = None

    def invoke(self, request: dict[str, Any]) -> dict[str, Any]:
        if request.get("operation") == "health":
            return self._invoke(request)
        with self.queue_condition:
            ticket = self.next_ticket
            self.next_ticket += 1
            while ticket != self.serving_ticket:
                self.queue_condition.wait()
        try:
            return self._invoke(request)
        finally:
            with self.queue_condition:
                self.serving_ticket += 1
                self.queue_condition.notify_all()

    def _invoke(self, request: dict[str, Any]) -> dict[str, Any]:
        operation = required_string(request, "operation")
        if operation == "health":
            return {"ok": True, "status": "running", "model": MODEL_NAME, "pid": os.getpid()}
        if operation == "tree":
            objects = self.unity.call("get-scene-tree").get("objects", [])
            scope = optional_string(request.get("path"))
            decoded_scope = urllib.parse.unquote(scope)
            scene_scope = decoded_scope if decoded_scope and decoded_scope.count("/") == 1 else ""
            if scene_scope:
                scope = scene_scope
            if scope and not scene_scope:
                scope = self.resolve_object_path(scope)
            depth = request.get("depth")
            if depth is not None:
                if isinstance(depth, bool) or not isinstance(depth, int) or depth < 0:
                    raise ValueError("depth must be a non-negative integer.")
            if scope:
                matches = [item for item in objects if item.get("path", "") == scope]
                if scene_scope:
                    prefix = scene_scope.rstrip("/") + "/"
                    matches = [item for item in objects if item.get("path", "").startswith(prefix)]
                    if not matches:
                        raise ValueError("Tree scene was not found: " + scene_scope)
                    base_depth = 0
                    objects = [
                        item for item in matches
                        if depth is None or int(item.get("depth", 0)) <= base_depth + depth
                    ]
                elif len(matches) != 1:
                    raise ValueError("Tree path was not found or is ambiguous: " + scope)
                else:
                    base_depth = int(matches[0].get("depth", 0))
                    prefix = scope.rstrip("/") + "/"
                    objects = [
                        item for item in objects
                        if (item.get("path", "") == scope or item.get("path", "").startswith(prefix))
                        and (depth is None or int(item.get("depth", 0)) <= base_depth + depth)
                    ]
            elif depth is not None:
                objects = [item for item in objects if int(item.get("depth", 0)) <= depth]
            return {"ok": True, "objects": [tree_entry(item) for item in objects]}
        if operation == "object-children":
            scope = self.resolve_object_path(required_string(request, "path"))
            objects = self.unity.call("get-scene-tree").get("objects", [])
            matches = [item for item in objects if item.get("path", "") == scope]
            if len(matches) != 1:
                raise ValueError("Object path was not found or is ambiguous: " + scope)
            return {
                "ok": True,
                "objects": [tree_entry(item) for item in objects if item.get("parentPath", "") == scope],
            }
        if operation == "find":
            return self.find_objects(request)
        if operation == "object-info":
            result = self.unity.call(
                "get-object-info",
                path=required_string(request, "path"),
                componentType=optional_string(request.get("componentType")),
                componentIndex=int(request.get("componentIndex", -1)),
            )
            return object_result(
                result,
                optional_string(request.get("componentType")),
                int(request.get("componentIndex", -1)),
                optional_string(request.get("propertyPath")),
            )
        if operation == "component-modify":
            return self.component_mutation("modify-component", request)
        if operation == "component-add":
            return self.component_mutation("add-component", request)
        if operation == "component-remove":
            return self.component_mutation("remove-component", request, include_values=False)
        if operation == "scene-save":
            return message_result(self.unity.call("save-scenes"))
        if operation == "component-action":
            path = required_string(request, "path")
            result = message_result(self.unity.call(
                "execute-component-action",
                path=path,
                componentType=required_string(request, "componentType"),
                componentIndex=int(request.get("componentIndex", -1)),
                action=required_string(request, "action"),
            ))
            result["path"] = path
            return result
        if operation == "asset-action":
            path = required_string(request, "path")
            result = message_result(self.unity.call(
                "execute-asset-action",
                path=path,
                action=required_string(request, "action"),
            ))
            result["path"] = path
            return result
        if operation == "component-suggest":
            return self.suggest_components(request)
        if operation == "object-picker":
            result = self.unity.call(
                "object-picker",
                path=required_string(request, "path"),
                componentType=required_string(request, "componentType"),
                componentIndex=int(request.get("componentIndex", -1)),
                propertyPath=required_string(request, "propertyPath"),
                limit=RESULT_COUNT,
            )
            return {
                "ok": True,
                "candidates": result.get("candidates", []),
            }
        if operation == "object-delete":
            return message_result(self.unity.call("delete-object", path=required_string(request, "path")))
        if operation == "object-duplicate":
            return object_result(self.unity.call("duplicate-object", path=required_string(request, "path")))
        if operation == "prefabs":
            result = self.unity.call("list-prefabs", path=optional_string(request.get("path")))
            return {"ok": True, "prefabs": result.get("prefabs", [])}
        if operation == "object-create":
            return object_result(self.unity.call("create-empty-object", destinationPath=required_string(request, "parentPath"), name=required_string(request, "name")))
        if operation == "prefab-save":
            result = self.unity.call(
                "save-prefab",
                path=optional_string(request.get("path")),
                destinationPath=optional_string(request.get("prefab")),
            )
            return {"ok": True, "prefab": (result.get("prefabs") or [{}])[0]}
        if operation == "prefab-apply":
            result = self.unity.call(
                "apply-prefab",
                path=required_string(request, "path"),
                componentType=optional_string(request.get("componentType")),
                componentIndex=request.get("componentIndex", -1),
                propertyPath=optional_string(request.get("propertyPath")),
            )
            response = {"ok": True, "path": (result.get("objectInfo") or {}).get("path", request["path"])}
            if result.get("message"):
                response["message"] = result["message"]
            return response
        if operation == "prefab-instantiate":
            return object_result(self.unity.call("instantiate-prefab", path=required_string(request, "prefab"), destinationPath=required_string(request, "parentPath")))
        if operation == "prefab-revert":
            result = self.unity.call(
                "revert-prefab",
                path=required_string(request, "path"),
                componentType=optional_string(request.get("componentType")),
                componentIndex=request.get("componentIndex", -1),
                propertyPath=optional_string(request.get("propertyPath")),
            )
            return {"ok": True, "path": (result.get("objectInfo") or {}).get("path", request["path"])}
        if operation == "prefab-open":
            result = self.unity.call("open-prefab", path=required_string(request, "prefab"))
            return {"ok": True, "prefab": (result.get("prefabs") or [{}])[0]}
        if operation == "prefab-close":
            result = self.unity.call("close-prefab")
            return {"ok": True, "prefab": (result.get("prefabs") or [{}])[0]}
        if operation == "scenes-search":
            scenes = self.unity.call("list-scenes").get("scenes", [])
            documents = [f"Unity scene {item.get('name', '')} path {item.get('assetPath', '')}" for item in scenes]
            ranked = self.rank(required_string(request, "query"), documents, RESULT_COUNT)
            return {"ok": True, "candidates": [{"score": score, "scene": scenes[index]} for index, score in ranked]}
        if operation == "scene-open":
            self.ensure_edit_mode()
            result = self.unity.call("open-scene", path=required_string(request, "scene"))
            return {"ok": True, "scene": (result.get("scenes") or [{}])[0]}
        if operation == "creation-templates":
            templates = self.unity.call("list-creation-templates").get("templates", [])
            query = optional_string(request.get("query"))
            if not query:
                return {"ok": True, "candidates": [{"template": item} for item in templates[:RESULT_COUNT]]}
            documents = [f"Unity Creation Template {item.get('name', '')} extension {item.get('extension', '')}" for item in templates]
            ranked = self.rank(query, documents, RESULT_COUNT)
            return {"ok": True, "candidates": [{"score": score, "template": templates[index]} for index, score in ranked]}
        if operation == "asset-create":
            template = required_string(request, "template")
            path = required_string(request, "path")
            component_type = component_class_name(path) if template.casefold() == "c# script".casefold() else None
            if component_type is not None:
                existing = self.unity.call("list-component-types").get("componentTypes", [])
                if component_type in existing:
                    raise ValueError(f"Component type already exists: {component_type}")
            result = self.unity.call("create-asset", templateName=template, path=path)
            response = asset_result(result)
            if component_type is not None:
                self.wait_for_component_type(component_type)
                response["componentType"] = component_type
            return response
        if operation == "asset-info":
            property_path = optional_string(request.get("propertyPath"))
            return asset_result(
                self.unity.call(
                    "get-asset-info",
                    path=required_string(request, "path"),
                    propertyPath=property_path,
                ),
                property_path,
            )
        if operation == "asset-modify":
            values = request.get("values", [])
            if not isinstance(values, list) or not values:
                raise ValueError("values must be a non-empty list.")
            return asset_result(self.unity.call("modify-asset", path=required_string(request, "path"), values=values))
        if operation == "asset-reimport":
            return asset_result(self.unity.call("reimport-asset", path=required_string(request, "path")))
        if operation == "refresh":
            marker = required_string(self.unity.call("refresh-assets", _timeout_seconds=360.0), "message")
            return {"ok": True, "refreshMarker": marker}
        if operation == "sprite-editor":
            return self.sprite_editor(request)
        if operation == "asset-move":
            return asset_result(self.unity.call("move-asset", path=required_string(request, "path"), destinationPath=required_string(request, "destinationPath")))
        if operation == "asset-delete":
            return message_result(self.unity.call("delete-asset", path=required_string(request, "path")))
        if operation == "asset-object-picker":
            result = self.unity.call("asset-object-picker", path=required_string(request, "path"), propertyPath=required_string(request, "propertyPath"), limit=RESULT_COUNT)
            return {"ok": True, "candidates": result.get("candidates", [])}
        if operation == "object-move":
            sibling_index = request.get("siblingIndex")
            return object_result(self.unity.call(
                "move-object",
                path=required_string(request, "path"),
                destinationPath=required_string(request, "destinationPath"),
                siblingIndex=-1 if sibling_index is None else int(sibling_index),
            ))
        if operation == "object-rename":
            return object_result(self.unity.call("rename-object", path=required_string(request, "path"), destinationPath=required_string(request, "destinationPath")))
        if operation == "object-active":
            return object_result(self.unity.call("set-active", path=required_string(request, "path"), boolValue=required_bool(request, "boolValue")))
        if operation == "object-tag":
            path = required_string(request, "path")
            tag = required_string(request, "tag")
            result = self.unity.call("set-tag", path=path, name=tag)
            return {"ok": True, "path": path, "tag": result.get("message", "")}
        if operation == "logs":
            if request.get("clear") is True:
                return message_result(self.unity.call("clear-logs"))
            return self.logs(request)
        if operation == "status":
            result = self.unity.call("get-status")
            return {"ok": True, "status": result.get("status", "")}
        if operation == "play":
            action = required_string(request, "action")
            result = message_result(self.unity.call("set-play-mode", action=action))
            self.wait_for_play_mode(action == "stop")
            return result
        if operation == "game-resolutions":
            result = self.unity.call("list-game-resolutions")
            return {"ok": True, "resolutions": result.get("resolutions", [])}
        if operation == "game-resolution":
            width = positive_integer(request.get("width"), "width")
            height = positive_integer(request.get("height"), "height")
            result = self.unity.call("set-game-resolution", width=width, height=height)
            self.last_game_frame_size = None
            return {"ok": True, "resolution": result.get("resolution", {})}
        if operation == "packages":
            result = self.unity.call("list-packages")
            return {"ok": True, "packages": [installed_package_entry(item) for item in result.get("packages", [])]}
        if operation == "packages-search":
            result = self.wait_for_package_operation("search-packages")
            packages = result.get("packages", [])
            documents = [package_document(item) for item in packages]
            ranked = self.rank(required_string(request, "query"), documents, RESULT_COUNT)
            return {
                "ok": True,
                "candidates": [
                    {"score": round(score, 4), "package": searched_package_entry(packages[index])}
                    for index, score in ranked
                ],
            }
        if operation in {"package-install", "package-update"}:
            result = self.wait_for_package_operation(
                "add-package",
                name=required_string(request, "name"),
                action=optional_string(request.get("version")),
            )
            return {"ok": True, "package": result.get("package", {})}
        if operation == "package-remove":
            result = self.wait_for_package_operation("remove-package", name=required_string(request, "name"))
            return message_result(result)
        if operation == "input-axes":
            result = self.unity.call("list-input-axes")
            return {"ok": True, "axes": result.get("axes", [])}
        if operation == "input-axis-create":
            values = request.get("values", [])
            if not isinstance(values, list):
                raise ValueError("values must be a list.")
            result = self.unity.call("create-input-axis", name=required_string(request, "name"), values=values)
            return {"ok": True, "axis": result.get("axis", {})}
        if operation == "input-axis-delete":
            return message_result(self.unity.call("delete-input-axis", name=required_string(request, "name")))
        if operation == "scene-screenshot":
            query = required_string(request, "query")
            mode = required_string(request, "mode").casefold()
            if mode not in {"grid", "flat"}:
                raise ValueError("mode must be grid or flat.")
            objects = [
                item for item in self.unity.call("get-scene-tree").get("objects", [])
                if item.get("activeInHierarchy", False) and item.get("visual", False)
            ]
            if not objects:
                raise RuntimeError("The current scene has no active 2D or 3D geometry to capture.")
            ranked = self.rank(query, [scene_document(item) for item in objects], 1 if mode == "flat" else 4)
            paths = [objects[index].get("path", "") for index, _score in ranked]
            result = self.unity.call("capture-scene", paths=paths, action=mode)
            return {
                "ok": True,
                "screenshots": result.get("screenshots", []),
                "labels": result.get("screenshotLabels", []),
            }
        if operation == "animation-table":
            search_path = optional_string(request.get("path")) or "Assets/Animations"
            if search_path.casefold().endswith(".anim"):
                table = unity_message_json(self.unity.call("get-animation-table", clip=search_path))
                return {"ok": True, "table": table}
            clips = unity_message_json(self.unity.call("list-animation-clips", path=search_path)).get("clips", [])
            ranked = self.rank(required_string(request, "query"), [animation_clip_document(item) for item in clips], 1)
            if not ranked:
                raise RuntimeError(search_path + " contains no .anim clips.")
            clip = clips[ranked[0][0]]
            table = unity_message_json(self.unity.call("get-animation-table", clip=clip.get("path", "")))
            return {"ok": True, "table": table}
        if operation == "animation-properties":
            properties = unity_message_json(self.unity.call("get-animation-properties", path=required_string(request, "path"))).get("properties", [])
            query = optional_string(request.get("query"))
            if query:
                ranked = self.rank(query, [animation_property_document(item) for item in properties], RESULT_COUNT)
                properties = [{"score": round(score, 4), "property": properties[index]} for index, score in ranked]
            return {"ok": True, "properties": properties}
        if operation == "animation-clip-create":
            path = required_string(request, "path")
            name = optional_string(request.get("name"))
            if path.casefold().endswith(".anim"):
                path_name = Path(path).stem
                if name and Path(name).stem.casefold() != path_name.casefold():
                    raise ValueError("name must match the .anim filename in path.")
                name = path_name
            if not name:
                raise ValueError("name is required when path is a folder.")
            result = self.unity.call("create-animation-clip", name=name, path=path)
            return {"ok": True, "clip": unity_message_json(result)}
        if operation == "animation-clip-delete":
            result = self.unity.call("delete-animation-clip", clip=required_string(request, "clip"))
            return {"ok": True, "clip": unity_message_json(result)}
        if operation == "animation-property":
            action = required_action(request, {"get", "create", "modify", "delete"})
            if action == "get":
                table = unity_message_json(self.unity.call("get-animation-table", clip=required_string(request, "clip")))
                object_path = optional_string(request.get("objectPath"))
                property_path = required_string(request, "property")
                rows = [
                    row for row in table.get("rows", [])
                    if animation_property_matches(row, property_path)
                    and row.get("objectPath", "") == object_path
                ]
                if len(rows) != 1:
                    raise ValueError("Animation property was not found or is ambiguous.")
                row = rows[0]
                return {
                    "ok": True,
                    "property": {
                        "id": row.get("id", ""),
                        "objectPath": row.get("objectPath", ""),
                        "kind": row.get("kind", ""),
                        "keys": [cell for cell in row.get("cells", []) if cell.get("hasKey")],
                    },
                }
            keys = request.get("keys", [])
            if not isinstance(keys, list):
                raise ValueError("keys must be a list.")
            normalized_keys = []
            for key in keys:
                if not isinstance(key, dict):
                    raise ValueError("Each animation key must be an object.")
                normalized_keys.append({**key, "hasTime": "time" in key})
            result = self.unity.call(
                "mutate-animation-property",
                action=action,
                clip=required_string(request, "clip"),
                objectPath=optional_string(request.get("objectPath")),
                propertyPath=required_string(request, "property"),
                json=json.dumps({"keys": normalized_keys}, ensure_ascii=False),
            )
            return {"ok": True, "clip": unity_message_json(result), "property": request["property"]}
        if operation == "animation-clip-setting":
            action = required_action(request, {"get", "set"})
            if action == "set" and "value" not in request:
                raise ValueError("value is required for set.")
            result = self.unity.call(
                "animation-clip-setting",
                action=action,
                clip=required_string(request, "clip"),
                propertyPath=required_string(request, "parameter"),
                value=None if action == "get" else str(request.get("value")),
            )
            return {"ok": True, "setting": unity_message_json(result)}
        if operation == "animator-find":
            path = optional_string(request.get("path"))
            if path:
                return {"ok": True, "animator": unity_message_json(self.unity.call("get-animator", path=path))}
            query = required_string(request, "query")
            animators = unity_message_json(self.unity.call("list-animators")).get("animators", [])
            ranked = self.rank(query, [animator_document(item) for item in animators], RESULT_COUNT)
            return {"ok": True, "candidates": [{"score": round(score, 4), "animator": animators[index]} for index, score in ranked]}
        if operation == "animator-component":
            action = required_action(request, {"create", "delete"})
            result = self.unity.call(
                "mutate-animator",
                action=action,
                path=required_string(request, "path"),
                controller=optional_string(request.get("controller")),
            )
            return {"ok": True, "animator": unity_message_json(result)}
        if operation == "animator-controller-assign":
            action = required_action(request, {"assign", "detach"})
            result = self.unity.call(
                "assign-animator-controller",
                action=action,
                path=required_string(request, "path"),
                controller=optional_string(request.get("controller")),
            )
            return {"ok": True, "animator": unity_message_json(result)}
        if operation == "animator-motions":
            path, controller = self.animator_source(request)
            result = self.unity.call("get-animator-motions", path=path, controller=controller)
            data = unity_message_json(result)
            return {"ok": True, "states": [compact_motion_state(item) for item in data.get("states", [])]}
        if operation == "animator-graph":
            path = optional_string(request.get("path"))
            controller = optional_string(request.get("controller"))
            if path and path.replace("\\", "/").startswith("Assets/"):
                controller = path
                path = None
            result = self.unity.call(
                "get-animator-controller",
                path=path,
                controller=controller,
            )
            return {"ok": True, "controller": compact_controller_graph(unity_message_json(result))}
        if operation == "animator-controller":
            action = required_action(request, {"create", "delete"})
            result = self.unity.call(
                "mutate-animator-controller",
                action=action,
                name=optional_string(request.get("name")),
                path=optional_string(request.get("path")),
                controller=optional_string(request.get("controller")),
            )
            return {"ok": True, "controller": unity_message_json(result)}
        if operation == "animator-state":
            result = self.unity.call(
                "mutate-animator-state",
                action=required_action(request, {"create", "modify", "delete"}),
                controller=required_string(request, "controller"),
                layer=required_string(request, "layer"),
                state=required_string(request, "state"),
                stateMachine=optional_string(request.get("stateMachine")),
                motion=optional_string(request.get("motion")),
                values=request_values(request),
            )
            return {"ok": True, "controller": unity_message_json(result)}
        if operation == "animator-state-motion":
            result = self.unity.call(
                "assign-animator-state-motion",
                action=required_action(request, {"assign", "detach"}),
                controller=required_string(request, "controller"),
                layer=required_string(request, "layer"),
                state=required_string(request, "state"),
                stateMachine=optional_string(request.get("stateMachine")),
                motion=optional_string(request.get("motion")),
            )
            return {"ok": True, "controller": unity_message_json(result)}
        if operation == "animator-transition":
            conditions = request.get("conditions")
            if conditions is not None and not isinstance(conditions, list):
                raise ValueError("conditions must be a list.")
            result = self.unity.call(
                "mutate-animator-transition",
                action=required_action(request, {"create", "modify", "delete"}),
                controller=required_string(request, "controller"),
                layer=required_string(request, "layer"),
                stateMachine=optional_string(request.get("stateMachine")),
                fromState=required_string(request, "fromState"),
                toState=required_string(request, "toState"),
                componentIndex=int(request.get("transitionIndex", -1)),
                values=request_values(request),
                json="" if conditions is None else json.dumps({"conditions": conditions}, ensure_ascii=False),
            )
            return {"ok": True, "controller": unity_message_json(result)}
        if operation == "animator-parameter":
            result = self.unity.call(
                "mutate-animator-parameter",
                action=required_action(request, {"create", "modify", "delete"}),
                controller=required_string(request, "controller"),
                name=required_string(request, "name"),
                parameterType=optional_string(request.get("type")),
                value=None if request.get("value") is None else str(request.get("value")),
                destinationPath=optional_string(request.get("newName")),
            )
            return {"ok": True, "controller": unity_message_json(result)}
        if operation == "animator-layer":
            result = self.unity.call(
                "mutate-animator-layer",
                action=required_action(request, {"create", "modify", "delete"}),
                controller=required_string(request, "controller"),
                layer=required_string(request, "layer"),
                values=request_values(request),
            )
            return {"ok": True, "controller": unity_message_json(result)}
        if operation == "animator-state-machine":
            result = self.unity.call(
                "mutate-animator-state-machine",
                action=required_action(request, {"create", "modify", "delete"}),
                controller=required_string(request, "controller"),
                layer=required_string(request, "layer"),
                stateMachine=required_string(request, "name"),
                objectPath=optional_string(request.get("parent")),
                name=optional_string(request.get("newName")),
            )
            return {"ok": True, "controller": unity_message_json(result)}
        if operation == "animator-blend-tree":
            settings = request.get("settings")
            if settings is not None and not isinstance(settings, dict):
                raise ValueError("settings must be an object.")
            result = self.unity.call(
                "mutate-animator-blend-tree",
                action=required_action(request, {"create", "modify", "delete"}),
                controller=required_string(request, "controller"),
                layer=required_string(request, "layer"),
                state=required_string(request, "state"),
                stateMachine=optional_string(request.get("stateMachine")),
                name=required_string(request, "name"),
                json="" if settings is None else json.dumps(settings, ensure_ascii=False),
            )
            return {"ok": True, "controller": unity_message_json(result)}
        if operation == "animator-control":
            result = self.unity.call(
                "control-animator",
                path=required_string(request, "path"),
                state=optional_string(request.get("state")),
                layer=optional_string(request.get("layer")),
                values=request_values(request),
            )
            return {"ok": True, "animator": compact_runtime_animator(unity_message_json(result))}
        if operation == "animator-runtime-state":
            result = self.unity.call("get-animator-runtime-state", path=required_string(request, "path"))
            return {"ok": True, "animator": compact_runtime_animator(unity_message_json(result))}
        if operation == "game-actions":
            return self.game_actions(request)
        raise ValueError(f"Unknown or excluded operation: {operation}")

    def game_actions(self, request: dict[str, Any]) -> dict[str, Any]:
        try:
            return self._game_actions(request)
        except BaseException as error:
            try:
                self.unity.call("pause-game")
            except BaseException as pause_error:
                raise RuntimeError(f"{error}; Unity also failed to pause: {pause_error}") from error
            raise

    def sprite_editor(self, request: dict[str, Any]) -> dict[str, Any]:
        asset_path = urllib.parse.unquote(required_string(request, "path")).replace("\\", "/")
        action = required_string(request, "action")
        if action == "preview":
            result = self.unity.call("get-sprite-layout", path=asset_path)
        elif action in {"auto", "manual", "border"}:
            payload: dict[str, Any] = {}
            if action == "manual":
                slices = request.get("slices")
                if not isinstance(slices, list) or not slices:
                    raise ValueError("manual action requires a non-empty slices array.")
                payload["slices"] = slices
            if action == "border":
                border = request.get("border")
                if not isinstance(border, dict):
                    raise ValueError("border action requires border.")
                payload["border"] = border
            result = self.unity.call(
                "mutate-sprite-layout",
                path=asset_path,
                action=action,
                json=json.dumps(payload, ensure_ascii=False, separators=(",", ":")),
            )
        else:
            raise ValueError("action must be preview, auto, manual or border.")

        try:
            layout = json.loads(required_string(result, "message"))
        except json.JSONDecodeError as error:
            raise RuntimeError("Unity returned an invalid sprite layout.") from error
        screenshot = self.render_sprite_layout(asset_path, layout)
        return {"ok": True, "screenshot": str(screenshot)}

    def render_sprite_layout(self, asset_path: str, layout: dict[str, Any]) -> Path:
        if not asset_path.startswith("Assets/") or ".." in Path(asset_path).parts:
            raise ValueError("Sprite path must point below Assets.")
        source_path = (self.project / Path(asset_path)).resolve()
        assets_root = (self.project / "Assets").resolve()
        if assets_root not in source_path.parents or not source_path.is_file():
            raise ValueError("Sprite asset was not found below Assets.")
        output_folder = self.project / "Library" / "UnityAgentBridge" / "SpritePreviews"
        output_folder.mkdir(parents=True, exist_ok=True)
        output_path = output_folder / f"sprite-{time.time_ns()}-{uuid.uuid4().hex}.png"

        with Image.open(source_path) as source:
            image = source.convert("RGBA")
        draw = ImageDraw.Draw(image)
        green = (0, 255, 64, 255)
        black = (0, 0, 0, 220)
        line_width = max(2, round(max(image.size) / 400))
        if layout.get("mode") == "multiple":
            slices = layout.get("slices")
            if not isinstance(slices, list):
                raise RuntimeError("Unity returned an invalid sprite slice list.")
            for index, item in enumerate(slices):
                if not isinstance(item, dict):
                    raise RuntimeError("Unity returned an invalid sprite slice.")
                x = float(item.get("x", 0))
                y = float(item.get("y", 0))
                width = float(item.get("width", 0))
                height = float(item.get("height", 0))
                top = image.height - y - height
                box = (round(x), round(top), round(x + width), round(top + height))
                draw.rectangle(box, outline=green, width=line_width)
                label = str(index)
                label_box = draw.textbbox((box[0] + 2, box[1] + 2), label)
                draw.rectangle((label_box[0] - 2, label_box[1] - 2, label_box[2] + 2, label_box[3] + 2), fill=black)
                draw.text((box[0] + 2, box[1] + 2), label, fill=green)
        else:
            border = layout.get("border")
            if not isinstance(border, dict):
                raise RuntimeError("Unity returned an invalid sprite border.")
            left = round(float(border.get("left", 0)))
            right = image.width - round(float(border.get("right", 0)))
            top = round(float(border.get("top", 0)))
            bottom = image.height - round(float(border.get("bottom", 0)))
            draw.line((left, 0, left, image.height), fill=green, width=line_width)
            draw.line((right, 0, right, image.height), fill=green, width=line_width)
            draw.line((0, top, image.width, top), fill=green, width=line_width)
            draw.line((0, bottom, image.width, bottom), fill=green, width=line_width)
        image.save(output_path, format="PNG", compress_level=6)
        image.close()
        return output_path

    def _game_actions(self, request: dict[str, Any]) -> dict[str, Any]:
        actions = validated_game_actions(request.get("actions"))
        first_state = unity_message_json(self.unity.call("prepare-game-interaction"))
        started = first_state.get("state") == "starting"
        if started:
            self.last_game_frame_size = None
        view = self.wait_for_game_view(first_state)

        if self.last_game_frame_size is None:
            frame_size = (
                positive_integer(view.get("renderWidth"), "Game View render width"),
                positive_integer(view.get("renderHeight"), "Game View render height"),
            )
        else:
            frame_size = self.last_game_frame_size

        game_input = UnityGameInput(self.unity, frame_size[0], frame_size[1])
        action_error: BaseException | None = None
        try:
            game_input.perform(actions)
        except BaseException as error:
            action_error = error
        try:
            game_input.release_all()
        except BaseException as error:
            if action_error is None:
                action_error = error

        time.sleep(0.1)
        frame = unity_message_json(self.unity.call("pause-and-capture-game"))
        width = positive_integer(frame.get("width"), "screenshot width")
        height = positive_integer(frame.get("height"), "screenshot height")
        screenshot = required_string(frame, "screenshot")
        width, height = resize_game_screenshot(Path(screenshot), width, height)
        self.last_game_frame_size = (width, height)
        if action_error is not None:
            return {
                "ok": False,
                "error": f"{type(action_error).__name__}: {action_error}",
                "screenshot": screenshot,
            }
        return {"ok": True, "screenshot": screenshot}

    def wait_for_game_view(self, state: dict[str, Any]) -> dict[str, Any]:
        deadline = time.monotonic() + 30.0
        while state.get("state") != "ready":
            if state.get("state") not in {"starting", "layout"}:
                raise RuntimeError(f"Unexpected Unity game state: {state.get('state')}")
            if time.monotonic() >= deadline:
                raise TimeoutError("Unity did not enter Play Mode with a usable Game View within 30 seconds.")
            time.sleep(0.1)
            state = unity_message_json(self.unity.call("prepare-game-interaction"))
        return state

    def ensure_edit_mode(self) -> None:
        status = str(self.unity.call("get-status").get("status", ""))
        if status == "игра оффлайн":
            return
        if status != "игра останавливается":
            self.unity.call("set-play-mode", action="stop")
        self.wait_for_play_mode(True)

    def wait_for_play_mode(self, offline: bool) -> None:
        expected = "игра оффлайн" if offline else "игра запущена"
        deadline = time.monotonic() + 60.0
        while True:
            status = str(self.unity.call("get-status").get("status", ""))
            if status == expected:
                return
            if time.monotonic() >= deadline:
                raise TimeoutError(f"Unity did not reach expected Play Mode state: {expected}.")
            time.sleep(0.1)

    def wait_for_package_operation(self, command: str, **arguments: Any) -> dict[str, Any]:
        deadline = time.monotonic() + 180.0
        while True:
            try:
                result = self.unity.call(command, **arguments)
            except RuntimeError as error:
                if "Cannot connect to 'api.unity.com'" in str(error):
                    raise RuntimeError("Unity Package Manager cannot reach api.unity.com.") from None
                raise
            if not result.get("pending", False):
                return result
            if time.monotonic() >= deadline:
                raise TimeoutError("Unity Package Manager did not finish within 180 seconds.")
            time.sleep(0.2)

    def component_mutation(self, command: str, request: dict[str, Any], include_values: bool = True) -> dict[str, Any]:
        arguments: dict[str, Any] = {
            "path": required_string(request, "path"),
            "componentType": required_string(request, "componentType"),
            "componentIndex": int(request.get("componentIndex", -1)),
        }
        if include_values:
            values = request.get("values", [])
            if not isinstance(values, list):
                raise ValueError("values must be a list.")
            arguments["values"] = values
        result = self.unity.call(command, **arguments)
        if command == "remove-component":
            compact_result = dict(result)
            object_info = dict(compact_result.get("objectInfo") or {})
            object_info["components"] = []
            compact_result["objectInfo"] = object_info
            return object_result(compact_result)
        component_index = int(result.get("componentIndex", arguments["componentIndex"]))
        response = object_result(result, arguments["componentType"], component_index)
        if command == "add-component":
            response["componentIndex"] = component_index
        return response

    def wait_for_component_type(self, component_type: str) -> None:
        deadline = time.monotonic() + 90.0
        while time.monotonic() < deadline:
            types = self.unity.call("list-component-types").get("componentTypes", [])
            if component_type in types:
                return
            time.sleep(0.5)
        raise RuntimeError(f"Unity did not compile and register component type within 90 seconds: {component_type}")

    def animator_source(self, request: dict[str, Any]) -> tuple[str, str]:
        path = optional_string(request.get("path"))
        controller = optional_string(request.get("controller"))
        query = optional_string(request.get("query"))
        supplied = sum(bool(value) for value in (path, controller, query))
        if supplied != 1:
            raise ValueError("Provide exactly one of path, controller, or query.")
        if not query:
            return path, controller
        animators = [item for item in unity_message_json(self.unity.call("list-animators")).get("animators", []) if item.get("controller")]
        ranked = self.rank(query, [animator_document(item) for item in animators], 1)
        if not ranked:
            raise RuntimeError("The current scene has no Animator with an Animator Controller.")
        return animators[ranked[0][0]].get("path", ""), ""

    def find_objects(self, request: dict[str, Any]) -> dict[str, Any]:
        query = optional_string(request.get("query"))
        component_type = optional_string(request.get("componentType"))
        if not query and not component_type:
            raise ValueError("Provide name or component.")
        limit = request.get("limit", RESULT_COUNT)
        if isinstance(limit, bool) or not isinstance(limit, int) or limit < 1 or limit > RESULT_COUNT:
            raise ValueError("limit must be between 1 and 10.")
        scope = request.get("path")
        tree = self.unity.call("get-scene-tree").get("objects", [])
        if scope:
            scope = self.resolve_object_path(str(scope))
            tree = [item for item in tree if item.get("path") == scope or item.get("path", "").startswith(scope + "/")]
        if component_type:
            tree = [
                item for item in tree
                if any(component_type_matches(str(component.get("type", "")), component_type) for component in item.get("components", []))
            ]
        if not query:
            return {"ok": True, "candidates": [{"object": tree_entry(item)} for item in tree[:limit]]}
        documents = [scene_document(item) for item in tree]
        ranked = self.rank(query, documents, limit)
        return {
            "ok": True,
            "candidates": [
                {"score": score, "object": search_entry(tree[index])}
                for index, score in ranked
            ],
        }

    def resolve_object_path(self, path: str) -> str:
        decoded = urllib.parse.unquote(path)
        return required_string(self.unity.call("resolve-object-path", path=decoded), "message")

    def suggest_components(self, request: dict[str, Any]) -> dict[str, Any]:
        path = required_string(request, "path")
        component_name = required_string(request, "componentName")
        query = required_string(request, "query")
        type_names = self.unity.call("list-component-types").get("componentTypes", [])
        object_info = self.unity.call("get-object-info", path=path).get("objectInfo", {})
        attached = {item.get("type") for item in object_info.get("components", [])}
        documents = [component_document(name) for name in type_names]
        ranked = self.rank(f"{component_name} {query}", documents, RESULT_COUNT)
        return {
            "ok": True,
            "candidates": [
                {"score": score, "type": type_names[index], "alreadyAttached": type_names[index] in attached}
                for index, score in ranked
            ],
        }

    def logs(self, request: dict[str, Any]) -> dict[str, Any]:
        unity_result = self.unity.call("get-logs")
        logs = collapse_logs(unity_result.get("logs", []))
        compilation_errors = collapse_logs(unity_result.get("currentCompilationErrors", []))
        level = optional_string(request.get("level"))
        if level:
            logs = [item for item in logs if str(item.get("type", "")).casefold() == level.casefold()]
        since_minutes = request.get("sinceMinutes")
        if since_minutes is not None:
            if isinstance(since_minutes, bool) or not isinstance(since_minutes, (int, float)) or since_minutes <= 0:
                raise ValueError("sinceMinutes must be positive.")
            cutoff = datetime.now(timezone.utc) - timedelta(minutes=float(since_minutes))
            logs = [item for item in logs if log_timestamp(item) >= cutoff]
        limit = request.get("limit", 20)
        if isinstance(limit, bool) or not isinstance(limit, int) or limit < 1 or limit > 100:
            raise ValueError("limit must be between 1 and 100.")
        include_stack = request.get("stackTrace") is True
        query = request.get("query")
        if not query:
            result = {"ok": True, "logs": [compact_log(item, include_stack) for item in logs[-limit:]]}
            if compilation_errors and (not level or level in {"error", "exception"}):
                result["currentCompilationErrors"] = [
                    compact_log(item, include_stack) for item in compilation_errors[-min(limit, 10):]
                ]
            return result
        documents = [f"{item.get('type', '')} {item.get('message', '')} {item.get('stackTrace', '')}" for item in logs]
        ranked = self.rank(str(query), documents, min(RESULT_COUNT, limit))
        return {
            "ok": True,
            "logs": [{"score": score, "entry": compact_log(logs[index], include_stack)} for index, score in ranked],
        }

    def rank(self, query: str, documents: list[str], limit: int) -> list[tuple[int, float]]:
        if not documents:
            return []
        vectors = list(self.model.embed([query, *documents]))
        query_vector = np.asarray(vectors[0], dtype=np.float32)
        query_norm = float(np.linalg.norm(query_vector))
        if query_norm == 0.0:
            raise RuntimeError("The NLP model produced a zero query vector.")
        scores: list[tuple[int, float]] = []
        for index, vector in enumerate(vectors[1:]):
            document_vector = np.asarray(vector, dtype=np.float32)
            denominator = query_norm * float(np.linalg.norm(document_vector))
            score = float(np.dot(query_vector, document_vector) / denominator) if denominator else -1.0
            if not math.isfinite(score):
                raise RuntimeError("The NLP model produced a non-finite similarity score.")
            scores.append((index, score))
        scores.sort(key=lambda item: item[1], reverse=True)
        return scores[:limit]


def component_document(type_name: str) -> str:
    simple_name = type_name.rsplit(".", 1)[-1]
    words = " ".join(wordninja.split(simple_name))
    namespace = type_name.rsplit(".", 1)[0].replace(".", " ") if "." in type_name else ""
    return f"Unity component {type_name} {namespace} {words}"


def animation_clip_document(item: dict[str, Any]) -> str:
    return f"Unity animation clip {item.get('name', '')} {item.get('path', '')}"


def animation_property_document(item: dict[str, Any]) -> str:
    return (
        f"Unity animation property {item.get('property', '')} {item.get('componentType', '')} "
        f"object {item.get('objectPath', '')} kind {item.get('kind', '')}"
    )


def animator_document(item: dict[str, Any]) -> str:
    return f"Unity Animator object {item.get('path', '')} controller {item.get('controller', '')}"


def compact_motion_state(item: dict[str, Any]) -> dict[str, Any]:
    return {
        "layer": item.get("layer", ""),
        "state": item.get("state", ""),
        "path": item.get("path", ""),
        "motion": compact_motion(item.get("motion")),
    }


def compact_motion(value: Any) -> dict[str, Any] | None:
    if not isinstance(value, dict) or value.get("kind") in {None, "", "None"}:
        return None
    result = {"kind": value.get("kind"), "name": value.get("name", ""), "path": value.get("path", "")}
    if value.get("kind") != "BlendTree":
        return result
    result.update({
        "blendType": value.get("blendType", ""),
        "blendParameter": value.get("blendParameter", ""),
        "blendParameterY": value.get("blendParameterY", ""),
        "useAutomaticThresholds": value.get("useAutomaticThresholds", False),
        "minThreshold": value.get("minThreshold", 0.0),
        "maxThreshold": value.get("maxThreshold", 0.0),
        "children": [
            {
                "motion": compact_motion(child.get("motion")),
                "threshold": child.get("threshold", 0.0),
                "position": child.get("position", {}),
                "timeScale": child.get("timeScale", 1.0),
                "cycleOffset": child.get("cycleOffset", 0.0),
                "directBlendParameter": child.get("directBlendParameter", ""),
                "mirror": child.get("mirror", False),
            }
            for child in value.get("children", [])
        ],
    })
    return result


def compact_controller_graph(controller: dict[str, Any]) -> dict[str, Any]:
    def visit(machine: dict[str, Any]) -> None:
        for state in machine.get("states", []):
            state["motion"] = compact_motion(state.get("motion"))
        for child in machine.get("stateMachines", []):
            visit(child)

    for layer in controller.get("layers", []):
        machine = layer.get("stateMachine")
        if isinstance(machine, dict):
            visit(machine)
    return controller


def compact_runtime_animator(animator: dict[str, Any]) -> dict[str, Any]:
    def state(value: Any) -> dict[str, Any] | None:
        if not isinstance(value, dict) or value.get("path") in {None, "", "0"}:
            return None
        return {
            "name": value.get("name", ""),
            "path": value.get("path", ""),
            "normalizedTime": value.get("normalizedTime", 0.0),
            "length": value.get("length", 0.0),
            "speed": value.get("speed", 0.0),
        }

    parameters = []
    for parameter in animator.get("parameters", []):
        parameter_type = parameter.get("type", "")
        value_key = {"Float": "floatValue", "Int": "intValue", "Bool": "boolValue", "Trigger": "boolValue"}.get(parameter_type)
        parameters.append({"name": parameter.get("name", ""), "type": parameter_type, "value": parameter.get(value_key)})
    return {
        "path": animator.get("path", ""),
        "enabled": animator.get("enabled", False),
        "activeInHierarchy": animator.get("activeInHierarchy", False),
        "state": animator.get("state", ""),
        "speed": animator.get("speed", 0.0),
        "layers": [
            {
                "index": layer.get("index", 0),
                "name": layer.get("name", ""),
                "weight": layer.get("weight", 0.0),
                "current": state(layer.get("current")),
                "inTransition": layer.get("inTransition", False),
                "next": state(layer.get("next")),
                "transitionTime": layer.get("transitionTime", 0.0),
            }
            for layer in animator.get("layers", [])
        ],
        "parameters": parameters,
    }


def request_values(request: dict[str, Any]) -> list[dict[str, str]]:
    values = request.get("values", [])
    if not isinstance(values, list):
        raise ValueError("values must be a list.")
    for entry in values:
        if not isinstance(entry, dict) or not isinstance(entry.get("path"), str) or not isinstance(entry.get("value"), str):
            raise ValueError("Each value must contain string path and value fields.")
    return values


def required_action(request: dict[str, Any], allowed: set[str]) -> str:
    action = required_string(request, "action").casefold()
    if action not in allowed:
        raise ValueError("action must be one of: " + ", ".join(sorted(allowed)))
    return action


def package_document(item: dict[str, Any]) -> str:
    dependencies = " ".join(dependency.get("name", "") for dependency in item.get("dependencies", []))
    return (
        f"Unity package {item.get('name', '')} {item.get('displayName', '')} "
        f"{item.get('description', '')} dependencies {dependencies}"
    )


def installed_package_entry(item: dict[str, Any]) -> dict[str, Any]:
    return {
        "name": item.get("name", ""),
        "version": item.get("version", ""),
        "dependencies": item.get("dependencies", []),
    }


def searched_package_entry(item: dict[str, Any]) -> dict[str, Any]:
    description = " ".join(str(item.get("description", "")).split())
    return {
        "name": item.get("name", ""),
        "displayName": item.get("displayName", ""),
        "version": item.get("version", ""),
        "description": description[:300],
        "dependencies": item.get("dependencies", []),
    }


def optional_string(value: Any) -> str:
    return value.strip() if isinstance(value, str) else ""


def project_plugin_identity(project: Path) -> tuple[str, str]:
    manifest = project / "Assets" / "UnityAgentBridge" / "CodexPlugin~" / "unity-agent-bridge" / "codex-plugin" / "plugin.json"
    try:
        value = json.loads(manifest.read_text(encoding="utf-8"))["version"]
    except (FileNotFoundError, KeyError, TypeError, json.JSONDecodeError, OSError) as error:
        raise RuntimeError("Unity Agent Bridge Codex plugin manifest is invalid.") from error
    if not isinstance(value, str) or not value:
        raise RuntimeError("Unity Agent Bridge Codex plugin version is invalid.")
    return value, plugin_revision(value)


def plugin_revision(version: str) -> str:
    _base, separator, suffix = version.partition("+")
    product, dot, revision = suffix.partition(".")
    if not separator or product not in {"codex", "claude"} or not dot or not revision:
        raise RuntimeError("Unity Agent Bridge plugin version has no revision.")
    return revision


def component_class_name(asset_path: str) -> str:
    name = re.sub(r"[^A-Za-z0-9_]", "", Path(asset_path).stem)
    if not name or name[0].isdigit():
        raise ValueError(f"Asset file name cannot form a valid C# class name: {asset_path}")
    return name


def scene_document(item: dict[str, Any]) -> str:
    component_names = " ".join(component.get("type", "") for component in item.get("components", []))
    return f"Unity scene object {item.get('name', '')} path {item.get('path', '')} tag {item.get('tag', '')} layer {item.get('layer', '')} components {component_names}"


def tree_entry(item: dict[str, Any]) -> dict[str, Any]:
    return {
        "path": item.get("path", ""),
        "parentPath": item.get("parentPath", ""),
        "name": item.get("name", ""),
        "depth": item.get("depth", 0),
    }


def search_entry(item: dict[str, Any]) -> dict[str, Any]:
    return {
        **tree_entry(item),
        "active": item.get("activeInHierarchy", False),
        "tag": item.get("tag", ""),
        "layer": item.get("layer", 0),
        "components": [component.get("type", "") for component in item.get("components", [])],
    }


def detailed_object(item: dict[str, Any]) -> dict[str, Any]:
    result = search_entry(item)
    result["activeSelf"] = item.get("activeSelf", False)
    world_position = item.get("worldPosition") or {}
    result["worldPosition"] = {
        "x": float(world_position.get("x", 0.0)),
        "y": float(world_position.get("y", 0.0)),
        "z": float(world_position.get("z", 0.0)),
    }
    result["components"] = [component_values(component) for component in item.get("components", [])]
    prefab_asset_path = item.get("prefabAssetPath", "")
    if prefab_asset_path:
        result["prefab"] = {
            "assetPath": prefab_asset_path,
            "instanceRootPath": item.get("prefabInstanceRootPath", ""),
        }
    return result


def compact_log(item: dict[str, Any], include_stack: bool = False) -> dict[str, Any]:
    lines = str(item.get("message", "")).splitlines()
    timestamp = log_timestamp(item)
    age_seconds = max(0, int((datetime.now(timezone.utc) - timestamp).total_seconds()))
    result = {
        "ageSeconds": age_seconds,
        "type": item.get("type", ""),
        "message": lines[0] if lines else "",
        "count": int(item.get("count", 1)),
    }
    if include_stack:
        result["stackTrace"] = item.get("stackTrace", "")
    return result


def log_timestamp(item: dict[str, Any]) -> datetime:
    value = str(item.get("timestampUtc", ""))
    try:
        return datetime.fromisoformat(value.replace("Z", "+00:00")).astimezone(timezone.utc)
    except ValueError:
        return datetime.min.replace(tzinfo=timezone.utc)


def collapse_logs(logs: list[dict[str, Any]]) -> list[dict[str, Any]]:
    groups: dict[tuple[str, str], dict[str, Any]] = {}
    for index, item in enumerate(logs):
        key = (
            str(item.get("type", "")),
            str(item.get("message", "")),
        )
        group = groups.get(key)
        if group is None:
            group = dict(item)
            group["count"] = 0
            groups[key] = group
        group["count"] += 1
        group["timestampUtc"] = item.get("timestampUtc", "")
        group["stackTrace"] = item.get("stackTrace", "")
        group["lastIndex"] = index
    return sorted(groups.values(), key=lambda item: int(item["lastIndex"]))


def component_values(component: dict[str, Any]) -> dict[str, Any]:
    raw = component.get("json", "")
    references = component.get("references", [])
    values = normalize_serialized_value(json.loads(raw)) if raw else {}
    apply_reference_values(values, references)
    simplify_unity_events(values)
    result = {
        "type": component.get("type", ""),
        "values": values,
    }
    warnings = component.get("warnings", [])
    actions = component.get("actions", [])
    if warnings:
        result["warnings"] = warnings
    if actions:
        result["actions"] = actions
    return result


def normalize_serialized_value(value: Any) -> Any:
    if isinstance(value, list):
        return [normalize_serialized_value(item) for item in value]
    if not isinstance(value, dict):
        return value
    if set(value) == {"instanceID"}:
        return None
    rect_offset = {"m_Left", "m_Right", "m_Top", "m_Bottom"}
    if rect_offset.issubset(value):
        result = {key: normalize_serialized_value(item) for key, item in value.items() if key not in rect_offset}
        result.update({
            "left": value["m_Left"],
            "right": value["m_Right"],
            "top": value["m_Top"],
            "bottom": value["m_Bottom"],
        })
        return result
    return {key: normalize_serialized_value(item) for key, item in value.items()}


def apply_reference_values(values: Any, references: Any) -> None:
    if not isinstance(values, dict) or not isinstance(references, list):
        return
    for reference in references:
        if not isinstance(reference, dict):
            continue
        path = reference.get("path")
        if not isinstance(path, str) or not path:
            continue
        assign_serialized_path(values, path, reference.get("value") or None)


def assign_serialized_path(root: Any, path: str, value: Any) -> None:
    tokens: list[str | int] = []
    parts = path.split(".")
    index = 0
    while index < len(parts):
        part = parts[index]
        if part == "Array" and index + 1 < len(parts):
            match = re.fullmatch(r"data\[(\d+)\]", parts[index + 1])
            if match:
                tokens.append(int(match.group(1)))
                index += 2
                continue
        tokens.append(part)
        index += 1
    current = root
    if tokens and isinstance(tokens[0], str) and tokens[0] not in current and len(current) == 1:
        wrapped = next(iter(current.values()))
        if isinstance(wrapped, dict):
            current = wrapped
    for token in tokens[:-1]:
        if isinstance(token, int):
            if not isinstance(current, list) or token >= len(current):
                return
            current = current[token]
        else:
            if not isinstance(current, dict) or token not in current:
                return
            current = current[token]
    final = tokens[-1]
    if isinstance(final, int):
        if isinstance(current, list) and final < len(current):
            current[final] = value
    elif isinstance(current, dict) and final in current:
        current[final] = value


def serialized_path_value(root: Any, path: str) -> Any:
    tokens: list[str | int] = []
    parts = path.split(".")
    index = 0
    while index < len(parts):
        part = parts[index]
        if part == "Array" and index + 1 < len(parts):
            match = re.fullmatch(r"data\[(\d+)\]", parts[index + 1])
            if match:
                tokens.append(int(match.group(1)))
                index += 2
                continue
        tokens.append(part)
        index += 1
    current = root
    if tokens and isinstance(tokens[0], str) and isinstance(current, dict) and tokens[0] not in current and len(current) == 1:
        wrapped = next(iter(current.values()))
        if isinstance(wrapped, dict):
            current = wrapped
    for token_index, token in enumerate(tokens):
        if isinstance(token, int):
            if not isinstance(current, list) or token >= len(current):
                raise KeyError(path)
            current = current[token]
            continue
        if not isinstance(current, dict):
            raise KeyError(path)
        candidates = [token]
        if token_index == 0 and not token.startswith("m_"):
            candidates.append("m_" + token[:1].upper() + token[1:])
        key = next((candidate for candidate in candidates if candidate in current), None)
        if key is None:
            raise KeyError(path)
        current = current[key]
    return current


def animation_property_matches(row: dict[str, Any], requested: str) -> bool:
    if row.get("id") == requested:
        return True
    component = str(row.get("componentType", ""))
    property_name = str(row.get("property", ""))
    return requested in {property_name, component + "/" + property_name, component.rsplit(".", 1)[-1] + "/" + property_name}


def simplify_unity_events(value: Any) -> None:
    if isinstance(value, list):
        for item in value:
            simplify_unity_events(item)
        return
    if not isinstance(value, dict):
        return
    for key, item in list(value.items()):
        if isinstance(item, dict) and isinstance(item.get("m_PersistentCalls"), dict):
            calls = item["m_PersistentCalls"].get("m_Calls", [])
            if isinstance(calls, list):
                value[key] = [unity_event_call(call) for call in calls if isinstance(call, dict)]
                continue
        simplify_unity_events(item)


def unity_event_call(call: dict[str, Any]) -> dict[str, Any]:
    result: dict[str, Any] = {
        "target": call.get("m_Target"),
        "method": call.get("m_MethodName", ""),
    }
    mode = int(call.get("m_Mode", 1))
    state = int(call.get("m_CallState", 2))
    mode_names = {0: "EventDefined", 1: "Void", 2: "Object", 3: "Int", 4: "Float", 5: "String", 6: "Bool"}
    state_names = {0: "Off", 1: "EditorAndRuntime", 2: "RuntimeOnly"}
    if mode != 1:
        result["mode"] = mode_names.get(mode, mode)
    if state != 2:
        result["state"] = state_names.get(state, state)
    arguments = call.get("m_Arguments", {})
    argument_names = {
        2: "m_ObjectArgument",
        3: "m_IntArgument",
        4: "m_FloatArgument",
        5: "m_StringArgument",
        6: "m_BoolArgument",
    }
    argument_name = argument_names.get(mode)
    if argument_name and isinstance(arguments, dict):
        result["argument"] = arguments.get(argument_name)
    return result


def object_result(
    result: dict[str, Any],
    component_type: str | None = None,
    component_index: int = -1,
    property_path: str | None = None,
) -> dict[str, Any]:
    object_info = result.get("objectInfo", {})
    if component_type:
        components = [
            component for component in object_info.get("components", [])
            if component_type_matches(str(component.get("type", "")), component_type)
        ]
        if not components:
            raise ValueError(f"Component was not found on object: {component_type}")
        if component_index < 0 and len(components) > 1:
            raise ValueError(f"Object has {len(components)} matching components; specify --component-index.")
        selected_index = 0 if component_index < 0 else component_index
        if selected_index >= len(components):
            raise ValueError(f"Component index {selected_index} is outside 0..{len(components) - 1}.")
        selected = component_values(components[selected_index])
        if property_path:
            try:
                value = serialized_path_value(selected.get("values", {}), property_path)
            except KeyError as error:
                raise ValueError(f"Component property was not found: {property_path}") from error
            return {
                "ok": True,
                "path": object_info.get("path", ""),
                "component": selected.get("type", ""),
                "property": property_path,
                "value": value,
            }
        object_info = dict(object_info)
        object_info["components"] = [components[selected_index]]
    elif property_path:
        raise ValueError("property requires component.")
    response: dict[str, Any] = {"ok": True, "objectInfo": detailed_object(object_info)}
    message = result.get("message")
    if message:
        response["message"] = message
    return response


def component_type_matches(actual: str, requested: str) -> bool:
    actual_name = actual.rsplit(".", 1)[-1]
    requested_name = requested.rsplit(".", 1)[-1]
    return actual.casefold() == requested.casefold() or actual_name.casefold() == requested_name.casefold()


def message_result(result: dict[str, Any]) -> dict[str, Any]:
    response: dict[str, Any] = {"ok": True}
    message = result.get("message")
    if message:
        response["message"] = message
    return response


def asset_result(result: dict[str, Any], property_path: str | None = None) -> dict[str, Any]:
    asset_info = result.get("assetInfo", {})
    if not property_path:
        return {"ok": True, "assetInfo": asset_info}
    properties = asset_info.get("properties", [])
    candidates = {property_path}
    if not property_path.startswith(("asset:", "importer:")):
        candidates.update({"asset:" + property_path, "importer:" + property_path})
    matches = [item for item in properties if item.get("path") in candidates]
    if not matches:
        raise ValueError(f"Asset property was not found: {property_path}")
    if len(matches) > 1:
        raise ValueError(f"Asset property is ambiguous; use its asset: or importer: path: {property_path}")
    item = matches[0]
    if len(properties) > 1:
        return {
            "ok": True,
            "path": asset_info.get("assetPath", ""),
            "property": item.get("path", ""),
            "properties": properties,
        }
    return {
        "ok": True,
        "path": asset_info.get("assetPath", ""),
        "property": item.get("path", ""),
        "type": item.get("type", ""),
        "value": item.get("value", ""),
        "writable": bool(item.get("writable", False)),
    }


def unity_message_json(result: dict[str, Any]) -> dict[str, Any]:
    message = result.get("message")
    if not isinstance(message, str) or not message:
        raise RuntimeError("Unity returned no game interaction state.")
    value = json.loads(message)
    if not isinstance(value, dict):
        raise RuntimeError("Unity returned an invalid game interaction state.")
    return value


def validated_game_actions(value: Any) -> list[dict[str, Any]]:
    if not isinstance(value, list):
        raise ValueError("actions must be a JSON list.")
    if len(value) > 100:
        raise ValueError("A game action batch cannot contain more than 100 actions.")

    schemas = {
        "click": ({"x", "y"}, {"button"}),
        "double_click": ({"x", "y"}, {"button"}),
        "hover": ({"x", "y"}, set()),
        "drag": ({"x", "y", "targetX", "targetY"}, {"button", "duration"}),
        "scroll": ({"x", "y", "scrollX", "scrollY"}, set()),
        "press_key": ({"key"}, {"duration"}),
        "key_down": ({"key"}, set()),
        "key_up": ({"key"}, set()),
        "type_text": ({"text"}, set()),
        "wait": ({"seconds"}, set()),
    }
    normalized: list[dict[str, Any]] = []
    duration_total = 0.0
    for index, raw in enumerate(value):
        if not isinstance(raw, dict):
            raise ValueError(f"actions[{index}] must be an object.")
        action = required_string(raw, "action")
        if action not in schemas:
            raise ValueError(f"Unsupported game action: {action}")
        required, optional = schemas[action]
        missing = required.difference(raw)
        unknown = set(raw).difference(required | optional | {"action"})
        if missing:
            raise ValueError(f"actions[{index}] is missing: {', '.join(sorted(missing))}")
        if unknown:
            raise ValueError(f"actions[{index}] has unknown fields: {', '.join(sorted(unknown))}")

        current = dict(raw)
        for coordinate in ("x", "y", "targetX", "targetY"):
            if coordinate in current:
                current[coordinate] = finite_number(current, coordinate)
        if "button" in current:
            button = required_string(current, "button").casefold()
            if button not in GAME_BUTTONS:
                raise ValueError(f"actions[{index}].button must be left, right, or middle.")
            current["button"] = button
        if action == "scroll":
            current["scrollX"] = finite_number(current, "scrollX")
            current["scrollY"] = finite_number(current, "scrollY")
            if current["scrollX"] == 0 and current["scrollY"] == 0:
                raise ValueError(f"actions[{index}] must scroll on at least one axis.")
        if action == "drag":
            duration = finite_number(current, "duration") if "duration" in current else 0.5
            if duration < 0 or duration > 10:
                raise ValueError(f"actions[{index}].duration must be between 0 and 10 seconds.")
            current["duration"] = duration
            duration_total += duration
        if action in {"press_key", "key_down", "key_up"}:
            current["key"] = required_string(current, "key")
        if action == "press_key":
            duration = finite_number(current, "duration") if "duration" in current else 0.15
            if duration < 0 or duration > 10:
                raise ValueError(f"actions[{index}].duration must be between 0 and 10 seconds.")
            current["duration"] = duration
            duration_total += duration
        if action == "type_text":
            current["text"] = required_string(current, "text")
        if action == "wait":
            seconds = finite_number(current, "seconds")
            if seconds < 0 or seconds > 30:
                raise ValueError(f"actions[{index}].seconds must be between 0 and 30.")
            current["seconds"] = seconds
            duration_total += seconds
        normalized.append(current)

    if duration_total > 90:
        raise ValueError("The combined wait and drag duration cannot exceed 90 seconds.")
    return normalized


def finite_number(value: dict[str, Any], name: str) -> float:
    number = value.get(name)
    if isinstance(number, bool) or not isinstance(number, (int, float)) or not math.isfinite(float(number)):
        raise ValueError(f"{name} must be a finite number.")
    return float(number)


def positive_integer(value: Any, name: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
        raise ValueError(f"{name} must be a positive integer.")
    return value


def required_string(request: dict[str, Any], name: str) -> str:
    value = request.get(name)
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{name} must be a non-empty string.")
    return value


def required_bool(request: dict[str, Any], name: str) -> bool:
    value = request.get(name)
    if not isinstance(value, bool):
        raise ValueError(f"{name} must be a boolean.")
    return value


class Handler(BaseHTTPRequestHandler):
    server_version = "UnityAgentBridge/1.0"

    def do_GET(self) -> None:
        if self.path != "/health":
            self.send_json(404, {"ok": False, "error": "Not found."})
            return
        if not self.authorized():
            return
        self.send_json(200, self.server.operations.invoke({"operation": "health"}))

    def do_POST(self) -> None:
        if not self.authorized():
            return
        if self.path == "/shutdown":
            self.send_json(200, {"ok": True, "message": "Server shutdown scheduled."})
            threading.Thread(target=self.server.shutdown, daemon=True).start()
            return
        if self.path != "/invoke":
            self.send_json(404, {"ok": False, "error": "Not found."})
            return
        try:
            length = int(self.headers.get("Content-Length", "0"))
            if length <= 0 or length > MAX_BODY_BYTES:
                raise ValueError("Request body size is invalid.")
            request = json.loads(self.rfile.read(length).decode("utf-8"))
            result = self.server.operations.invoke(request)
            self.send_json(200, result)
        except Exception as error:
            message = " ".join(str(error).split())[:500]
            self.send_json(400, {"ok": False, "error": f"{type(error).__name__}: {message}"})

    def authorized(self) -> bool:
        if self.headers.get("X-Unity-Agent-Token") == self.server.token:
            return True
        self.send_json(403, {"ok": False, "error": "Invalid bridge token."})
        return False

    def send_json(self, status: int, value: dict[str, Any]) -> None:
        body = json.dumps(value, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format: str, *args: Any) -> None:
        return


class Server(ThreadingHTTPServer):
    daemon_threads = True

    def __init__(self, address: tuple[str, int], token: str, operations: Operations) -> None:
        super().__init__(address, Handler)
        self.token = token
        self.operations = operations


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--token", required=True)
    parser.add_argument("--model-cache", type=Path, required=True)
    arguments = parser.parse_args()

    project = arguments.project.resolve()
    runtime = project / "Library" / "UnityAgentBridge"
    config_path = runtime / "server.json"
    error_path = runtime / "server-error.json"
    active_project_file = discovery_file("active-project.json")
    mcp_error_files = [discovery_file("mcp-error-codex.txt"), discovery_file("mcp-error-claude.txt")]
    runtime.mkdir(parents=True, exist_ok=True)
    unlink_when_available(error_path)

    try:
        operations = Operations(project, arguments.model_cache.resolve())
        server = Server(("127.0.0.1", arguments.port), arguments.token, operations)
        write_atomic(config_path, {
            "projectRoot": str(project),
            "port": arguments.port,
            "token": arguments.token,
            "pid": os.getpid(),
            "model": MODEL_NAME,
            "bridgeVersion": operations.plugin_version,
            "bridgeRevision": operations.plugin_revision,
        })
        write_atomic(active_project_file, {"projectRoot": str(project)})
        for mcp_error_file in mcp_error_files:
            mcp_error_file.unlink(missing_ok=True)
        try:
            server.serve_forever(poll_interval=0.2)
        finally:
            server.server_close()
            unlink_when_available(config_path)
            clear_active_project(active_project_file, project)
        return 0
    except Exception as error:
        write_atomic(error_path, {"error": f"{type(error).__name__}: {error}", "traceback": traceback.format_exc()})
        unlink_when_available(config_path)
        clear_active_project(active_project_file, project)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
