from __future__ import annotations

import argparse
import base64
import io
import json
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


_SOURCE_RELOADING = bool(globals().get("_SOURCE_RELOADING", False))
_SOURCE_PATH = Path(__file__).resolve()
_SOURCE_MTIME_NS = int(globals().get("_SOURCE_MTIME_NS", _SOURCE_PATH.stat().st_mtime_ns))


if hasattr(sys.stdin, "reconfigure"):
    sys.stdin.reconfigure(encoding="utf-8")
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")


PROTOCOL_VERSION = "2025-06-18"
MAX_CONFIG_BYTES = 64 * 1024
MAX_RESPONSE_BYTES = 4 * 1024 * 1024
GAME_TOOL_NAME = "game_actions"
SCENE_TOOL_NAME = "scene_screenshot"
SPRITE_TOOL_NAME = "sprite_editor"
SERVER_INSTRUCTIONS = (
    "Этот MCP содержит кадры сцены, Game View и редактор разметки Sprite. Остальные возможности Unity Agent Bridge "
    "доступны через skill unity-agent-bridge и scripts/uab.ps1; список MCP tools не является списком возможностей плагина."
)
STATE_FILE_PREFIX = "mcp-state-"
LANDSCAPE_SCREENSHOT_LIMIT = (1280, 720)
PORTRAIT_SCREENSHOT_LIMIT = (720, 1280)

ACTION_PROPERTIES: dict[str, Any] = {
    "action": {
        "type": "string",
        "enum": [
            "click",
            "double_click",
            "hover",
            "drag",
            "scroll",
            "press_key",
            "key_down",
            "key_up",
            "type_text",
            "wait",
        ],
    },
    "x": {"type": "number"},
    "y": {"type": "number"},
    "targetX": {"type": "number"},
    "targetY": {"type": "number"},
    "button": {"type": "string", "enum": ["left", "right", "middle"]},
    "duration": {"type": "number", "minimum": 0, "maximum": 10},
    "scrollX": {
        "type": "number",
        "description": "Горизонтальная прокрутка: отрицательное значение — влево, положительное — вправо.",
    },
    "scrollY": {
        "type": "number",
        "description": "Вертикальная прокрутка: отрицательное значение — вверх, положительное — вниз.",
    },
    "key": {"type": "string"},
    "text": {"type": "string"},
    "seconds": {
        "type": "number",
        "minimum": 0,
        "maximum": 30,
        "description": "Игровое время ожидания; Bridge временно ускоряет Time.timeScale в 10 раз.",
    },
}

GAME_ACTIONS_TOOL = {
    "name": GAME_TOOL_NAME,
    "description": (
        "Выполняет пакет действий в Game View Unity. Первый вызов запускает игру и ждёт готового кадра. "
        "После пакета игра ставится на паузу; результатом служит только итоговый снимок."
    ),
    "inputSchema": {
        "type": "object",
        "properties": {
            "actions": {
                "type": "array",
                "maxItems": 100,
                "items": {
                    "type": "object",
                    "properties": ACTION_PROPERTIES,
                    "required": ["action"],
                    "additionalProperties": False,
                },
                "description": (
                    "Форматы: click/double_click(x,y,button?); hover(x,y); "
                    "drag(x,y,targetX,targetY,button?,duration?); scroll(x,y,scrollX,scrollY); "
                    "press_key(key,duration?); key_down/key_up(key); type_text(text); wait(seconds). "
                    "Пустой массив делает начальный снимок."
                ),
            }
        },
        "required": ["actions"],
        "additionalProperties": False,
    },
}

SPRITE_EDITOR_TOOL = {
    "name": SPRITE_TOOL_NAME,
    "description": (
        "Показывает или изменяет разметку Sprite и сразу возвращает изображение. "
        "multiple: auto либо manual с точными прямоугольниками; single: border с зелёными линиями границ."
    ),
    "inputSchema": {
        "type": "object",
        "properties": {
            "path": {"type": "string", "description": "Путь текстуры внутри Assets."},
            "action": {"type": "string", "enum": ["preview", "auto", "manual", "border"]},
            "slices": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "name": {"type": "string"},
                        "x": {"type": "number", "minimum": 0},
                        "y": {"type": "number", "minimum": 0},
                        "width": {"type": "number", "exclusiveMinimum": 0},
                        "height": {"type": "number", "exclusiveMinimum": 0},
                    },
                    "required": ["x", "y", "width", "height"],
                    "additionalProperties": False,
                },
            },
            "border": {
                "type": "object",
                "properties": {
                    "left": {"type": "number", "minimum": 0},
                    "right": {"type": "number", "minimum": 0},
                    "top": {"type": "number", "minimum": 0},
                    "bottom": {"type": "number", "minimum": 0},
                },
                "required": ["left", "right", "top", "bottom"],
                "additionalProperties": False,
            },
        },
        "required": ["path", "action"],
        "additionalProperties": False,
    },
}

SCENE_SCREENSHOT_TOOL = {
    "name": SCENE_TOOL_NAME,
    "description": "Снимает найденные по запросу объекты текущей сцены. grid возвращает коллаж 2×2, flat — один ортографический кадр.",
    "inputSchema": {
        "type": "object",
        "properties": {
            "query": {"type": "string", "minLength": 1},
            "mode": {"type": "string", "enum": ["grid", "flat"]},
        },
        "required": ["query", "mode"],
        "additionalProperties": False,
    },
}


class ToolFailure(RuntimeError):
    def __init__(self, message: str, screenshot: Path | None = None) -> None:
        super().__init__(message)
        self.screenshot = screenshot


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--client", choices=("codex", "claude"), required=True)
    parser.add_argument("--client-version", required=True)
    arguments = parser.parse_args()
    project = arguments.project.resolve()
    client = arguments.client
    client_version = arguments.client_version
    state_file = write_state(project, client, client_version, "started")
    try:
        for raw_line in sys.stdin:
            if not raw_line.strip():
                continue
            reload_source_if_changed()
            response = dispatch_line(raw_line, project, client, client_version)
            if response is not None:
                sys.stdout.write(json.dumps(response, ensure_ascii=False, separators=(",", ":")) + "\n")
                sys.stdout.flush()
        return 0
    finally:
        state_file.unlink(missing_ok=True)


def reload_source_if_changed() -> None:
    global _SOURCE_MTIME_NS, _SOURCE_RELOADING
    modified = _SOURCE_PATH.stat().st_mtime_ns
    if modified == _SOURCE_MTIME_NS:
        return
    time.sleep(0.05)
    modified = _SOURCE_PATH.stat().st_mtime_ns
    source = _SOURCE_PATH.read_text(encoding="utf-8")
    compiled = compile(source, str(_SOURCE_PATH), "exec")
    _SOURCE_RELOADING = True
    try:
        exec(compiled, globals(), globals())
    finally:
        _SOURCE_RELOADING = False
    _SOURCE_MTIME_NS = modified


def dispatch_line(raw_line: str, project: Path, client: str, client_version: str) -> dict[str, Any] | list[dict[str, Any]] | None:
    try:
        message = json.loads(raw_line)
    except json.JSONDecodeError as error:
        return rpc_error(None, -32700, f"Parse error: {error.msg}")

    if isinstance(message, list):
        if not message:
            return rpc_error(None, -32600, "Invalid Request")
        responses = [response for item in message if (response := dispatch(item, project, client, client_version)) is not None]
        return responses or None
    return dispatch(message, project, client, client_version)


def dispatch(message: Any, project: Path, client: str, client_version: str) -> dict[str, Any] | None:
    if not isinstance(message, dict) or message.get("jsonrpc") != "2.0":
        return rpc_error(message.get("id") if isinstance(message, dict) else None, -32600, "Invalid Request")

    request_id = message.get("id")
    method = message.get("method")
    if request_id is None:
        return None
    if not isinstance(method, str):
        return rpc_error(request_id, -32600, "Invalid Request")

    try:
        if method == "initialize":
            result = {
                "protocolVersion": PROTOCOL_VERSION,
                "capabilities": {"tools": {"listChanged": False}},
                "serverInfo": {"name": "unity-game", "version": client_version},
                "instructions": SERVER_INSTRUCTIONS,
            }
        elif method == "ping":
            result = {}
        elif method == "tools/list":
            write_state(project, client, client_version, "ready")
            result = {"tools": [GAME_ACTIONS_TOOL, SCENE_SCREENSHOT_TOOL, SPRITE_EDITOR_TOOL]}
        elif method == "tools/call":
            params = message.get("params")
            if not isinstance(params, dict):
                raise ValueError("params must be an object.")
            result = call_tool(params, project)
        else:
            return rpc_error(request_id, -32601, "Method not found")
        return {"jsonrpc": "2.0", "id": request_id, "result": result}
    except ValueError as error:
        return rpc_error(request_id, -32602, str(error))
    except Exception as error:
        return rpc_error(request_id, -32603, concise_error(error))


def call_tool(params: dict[str, Any], project: Path) -> dict[str, Any]:
    name = params.get("name")
    arguments = params.get("arguments")
    if not isinstance(arguments, dict):
        raise ValueError("arguments must be an object.")
    if name == SCENE_TOOL_NAME:
        return call_scene_screenshot(arguments, project)
    if name == SPRITE_TOOL_NAME:
        return call_sprite_editor(arguments, project)
    if name != GAME_TOOL_NAME:
        raise ValueError(f"Unknown tool: {name}")
    if set(arguments) != {"actions"}:
        raise ValueError("game_actions accepts only the actions field.")
    actions = arguments.get("actions")
    if not isinstance(actions, list):
        raise ValueError("actions must be an array.")

    try:
        result = invoke_bridge(project, {"operation": "game-actions", "actions": actions})
        screenshot = screenshot_path(result)
        content = [image_content(screenshot)]
        if result.get("ok") is False:
            content.append({"type": "text", "text": concise_text(result.get("error"), "Game action failed.")})
            return {"content": content, "isError": True}
        return {"content": content, "isError": False}
    except ToolFailure as error:
        content: list[dict[str, Any]] = []
        if error.screenshot is not None and error.screenshot.is_file():
            content.append(image_content(error.screenshot))
        content.append({"type": "text", "text": concise_error(error)})
        return {"content": content, "isError": True}


def call_sprite_editor(arguments: dict[str, Any], project: Path) -> dict[str, Any]:
    allowed = {"path", "action", "slices", "border"}
    unknown = set(arguments).difference(allowed)
    missing = {"path", "action"}.difference(arguments)
    if missing:
        raise ValueError("sprite_editor is missing: " + ", ".join(sorted(missing)))
    if unknown:
        raise ValueError("sprite_editor has unknown fields: " + ", ".join(sorted(unknown)))
    path = arguments.get("path")
    action = arguments.get("action")
    if not isinstance(path, str) or not path.strip():
        raise ValueError("path must be a non-empty string.")
    if action not in {"preview", "auto", "manual", "border"}:
        raise ValueError("action must be preview, auto, manual or border.")
    if action == "manual" and (not isinstance(arguments.get("slices"), list) or not arguments["slices"]):
        raise ValueError("manual action requires a non-empty slices array.")
    if action == "border" and not isinstance(arguments.get("border"), dict):
        raise ValueError("border action requires border.")
    if action != "manual" and "slices" in arguments:
        raise ValueError("slices is valid only for manual action.")
    if action != "border" and "border" in arguments:
        raise ValueError("border is valid only for border action.")
    try:
        payload = {"operation": "sprite-editor", **arguments}
        result = invoke_bridge(project, payload)
        return {"content": [image_content(screenshot_path(result))], "isError": False}
    except ToolFailure as error:
        return {"content": [{"type": "text", "text": concise_error(error)}], "isError": True}


def call_scene_screenshot(arguments: dict[str, Any], project: Path) -> dict[str, Any]:
    missing = {"query", "mode"}.difference(arguments)
    unknown = set(arguments).difference({"query", "mode"})
    if missing:
        raise ValueError("scene_screenshot is missing: " + ", ".join(sorted(missing)))
    if unknown:
        raise ValueError("scene_screenshot has unknown fields: " + ", ".join(sorted(unknown)))
    query = arguments.get("query")
    mode = arguments.get("mode")
    if not isinstance(query, str) or not query.strip():
        raise ValueError("query must be a non-empty string.")
    if mode not in {"grid", "flat"}:
        raise ValueError("mode must be grid or flat.")
    try:
        result = invoke_bridge(project, {"operation": "scene-screenshot", "query": query, "mode": mode})
        screenshots = result.get("screenshots")
        if not isinstance(screenshots, list) or not screenshots:
            raise ToolFailure("Unity Agent Bridge returned no scene screenshot.")
        paths = [checked_screenshot_path(path) for path in screenshots]
        labels = result.get("labels", [])
        if not isinstance(labels, list) or any(not isinstance(label, str) for label in labels):
            raise ToolFailure("Unity scene screenshot labels are invalid.")
        content = [collage_content(paths, labels)] if mode == "grid" else [image_content(paths[0])]
        return {"content": content, "isError": False}
    except ToolFailure as error:
        return {"content": [{"type": "text", "text": concise_error(error)}], "isError": True}


def invoke_bridge(project: Path, payload: dict[str, Any]) -> dict[str, Any]:
    config_path = project / "Library" / "UnityAgentBridge" / "server.json"
    try:
        if config_path.stat().st_size > MAX_CONFIG_BYTES:
            raise ToolFailure("Unity Agent Bridge server config is invalid.")
        config = json.loads(config_path.read_text(encoding="utf-8"))
        port = config["port"]
        token = config["token"]
        configured_project = Path(config["projectRoot"]).resolve()
    except FileNotFoundError as error:
        raise ToolFailure("Unity Agent Bridge server is not running.") from error
    except (KeyError, TypeError, ValueError, json.JSONDecodeError, OSError) as error:
        raise ToolFailure("Unity Agent Bridge server config is invalid.") from error

    if configured_project != project or isinstance(port, bool) or not isinstance(port, int) or not isinstance(token, str):
        raise ToolFailure("Unity Agent Bridge server config does not match this project.")

    body = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    request = urllib.request.Request(
        f"http://127.0.0.1:{port}/invoke",
        data=body,
        method="POST",
        headers={"Content-Type": "application/json", "X-Unity-Agent-Token": token},
    )
    try:
        opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
        with opener.open(request, timeout=360) as response:
            response_body = response.read(MAX_RESPONSE_BYTES + 1)
    except urllib.error.HTTPError as error:
        response_body = error.read(MAX_RESPONSE_BYTES + 1)
    except (urllib.error.URLError, TimeoutError, OSError) as error:
        raise ToolFailure("Unity Agent Bridge server is unavailable.") from error

    if len(response_body) > MAX_RESPONSE_BYTES:
        raise ToolFailure("Unity Agent Bridge response is too large.")
    try:
        result = json.loads(response_body.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ToolFailure("Unity Agent Bridge returned an invalid response.") from error
    if not isinstance(result, dict):
        raise ToolFailure("Unity Agent Bridge returned an invalid response.")
    if result.get("ok") is False and not result.get("screenshot"):
        raise ToolFailure(concise_text(result.get("error"), "Unity game action failed."))
    return result


def screenshot_path(result: dict[str, Any]) -> Path:
    value = result.get("screenshot")
    if not isinstance(value, str) or not value:
        raise ToolFailure("Unity Agent Bridge returned no screenshot.")
    return checked_screenshot_path(value)


def checked_screenshot_path(value: Any) -> Path:
    if not isinstance(value, str) or not value:
        raise ToolFailure("Unity Agent Bridge returned no screenshot.")
    path = Path(value).resolve()
    if not path.is_file():
        raise ToolFailure("Unity screenshot was not created.")
    return path


def image_content(path: Path) -> dict[str, str]:
    try:
        with Image.open(path) as source:
            source.load()
            return encoded_image_content(source)
    except (OSError, ValueError) as error:
        raise ToolFailure("Unity screenshot cannot be read.") from error


def collage_content(paths: list[Path], labels: list[str]) -> dict[str, str]:
    if not 1 <= len(paths) <= 4:
        raise ToolFailure("Unity grid screenshot must contain one to four views.")
    if labels and len(labels) != len(paths):
        raise ToolFailure("Unity grid screenshot labels do not match its views.")
    images: list[Image.Image] = []
    collage: Image.Image | None = None
    try:
        for path in paths:
            with Image.open(path) as source:
                source.load()
                images.append(source.convert("RGB"))
        width = max(image.width for image in images)
        height = max(image.height for image in images)
        columns = 1 if len(images) == 1 else 2
        rows = (len(images) + columns - 1) // columns
        collage = Image.new("RGB", (width * columns, height * rows))
        for index, image in enumerate(images):
            if image.size != (width, height):
                image.thumbnail((width, height), Image.Resampling.LANCZOS)
            x = (index % columns) * width + (width - image.width) // 2
            y = (index // columns) * height + (height - image.height) // 2
            collage.paste(image, (x, y))
            if labels:
                draw_scene_label(collage, labels[index], index, width, height, columns)
        return encoded_image_content(collage)
    except (OSError, ValueError) as error:
        raise ToolFailure("Unity grid screenshot cannot be composed.") from error
    finally:
        if collage is not None:
            collage.close()
        for image in images:
            image.close()


def draw_scene_label(
    collage: Image.Image,
    label: str,
    index: int,
    cell_width: int,
    cell_height: int,
    columns: int,
) -> None:
    font_size = max(18, min(30, cell_height // 24))
    font = ImageFont.truetype(scene_label_font(), font_size)
    draw = ImageDraw.Draw(collage)
    padding = max(6, font_size // 3)
    available = cell_width - padding * 2
    visible = truncate_label_start(draw, label, font, available)
    left = (index % columns) * cell_width
    top = (index // columns) * cell_height
    bar_height = font_size + padding * 2
    draw.rectangle((left, top, left + cell_width - 1, top + bar_height), fill=(0, 0, 0))
    draw.text((left + padding, top + padding), visible, font=font, fill=(255, 255, 255))


def truncate_label_start(draw: ImageDraw.ImageDraw, label: str, font: ImageFont.FreeTypeFont, width: int) -> str:
    if draw.textlength(label, font=font) <= width:
        return label
    prefix = "…"
    low = 0
    high = len(label)
    while low < high:
        length = (low + high + 1) // 2
        if draw.textlength(prefix + label[-length:], font=font) <= width:
            low = length
        else:
            high = length - 1
    return prefix + label[-low:] if low else prefix


def scene_label_font() -> str:
    candidates = [
        Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts" / "segoeui.ttf",
        Path("/System/Library/Fonts/Supplemental/Arial Unicode.ttf"),
        Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"),
    ]
    for path in candidates:
        if path.is_file():
            return str(path)
    raise ToolFailure("A Unicode font for scene screenshot labels was not found.")


def encoded_image_content(source: Image.Image) -> dict[str, str]:
    limit = LANDSCAPE_SCREENSHOT_LIMIT if source.width >= source.height else PORTRAIT_SCREENSHOT_LIMIT
    if source.width > limit[0] or source.height > limit[1]:
        source.thumbnail(limit, Image.Resampling.LANCZOS)
    output = io.BytesIO()
    source.save(output, format="PNG", compress_level=6)
    data = base64.b64encode(output.getvalue()).decode("ascii")
    return {"type": "image", "data": data, "mimeType": "image/png"}


def concise_error(error: BaseException) -> str:
    return concise_text(str(error), type(error).__name__)


def concise_text(value: Any, default: str) -> str:
    text = value.strip() if isinstance(value, str) else ""
    return (text or default).splitlines()[0][:500]


def rpc_error(request_id: Any, code: int, message: str) -> dict[str, Any]:
    return {"jsonrpc": "2.0", "id": request_id, "error": {"code": code, "message": message}}


def state_path(client: str) -> Path:
    local_app_data = os.environ.get("LOCALAPPDATA")
    if not local_app_data:
        raise RuntimeError("LOCALAPPDATA is unavailable.")
    return Path(local_app_data) / "UnityAgentBridge" / f"{STATE_FILE_PREFIX}{client}-{os.getpid()}.json"


def write_state(project: Path, client: str, client_version: str, state: str) -> Path:
    path = state_path(client)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f"{path.name}.{os.getpid()}.tmp")
    temporary.write_text(
        json.dumps(
            {
                "state": state,
                "client": client,
                "clientVersion": client_version,
                "projectRoot": str(project),
                "pid": os.getpid(),
                "updatedAt": int(time.time()),
            },
            ensure_ascii=False,
            separators=(",", ":"),
        ),
        encoding="utf-8",
    )
    temporary.replace(path)
    return path


if __name__ == "__main__" and not _SOURCE_RELOADING:
    raise SystemExit(main())
