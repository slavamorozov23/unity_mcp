using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityAgentBridge.Editor
{
    internal static class GameInteractionService
    {
        [Serializable]
        private sealed class GameViewState
        {
            public string state;
            public int renderWidth;
            public int renderHeight;
        }

        [Serializable]
        private sealed class GameFrame
        {
            public string screenshot;
            public int width;
            public int height;
        }

        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type GameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true);
        private static readonly PropertyInfo TargetInView = GameViewType.GetProperty("targetInView", InstanceMembers);
        private static readonly FieldInfo GameRenderTexture = GameViewType.GetField("m_RenderTexture", InstanceMembers);
        private static readonly Dictionary<string, long> PendingDispatches =
            new Dictionary<string, long>(StringComparer.Ordinal);

        internal static bool InputSettled
        {
            get { return !EditorApplication.isPlaying || GameInputDriverBehaviour.AllSettled; }
        }

        public static string Prepare()
        {
            InputSystemSetupService.RequireReady();
            if (EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying)
                return JsonUtility.ToJson(new GameViewState { state = "starting" });
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                GameInputDriverBehaviour.ResetState();
                PendingDispatches.Clear();
                InputSystemGameInput.Reset();
                ScenePersistenceService.SaveBeforeTransition();
                EditorApplication.EnterPlaymode();
                return JsonUtility.ToJson(new GameViewState { state = "starting" });
            }

            if (!EditorApplication.isPlaying)
                return JsonUtility.ToJson(new GameViewState { state = "starting" });

            try
            {
                var gameView = GetGameView(false);
                GameInputDriverBehaviour.Ensure();
                InputSystemGameInput.EnsureAvailable();
                RuntimeUiInputSetup.Ensure();
                Application.runInBackground = true;
                gameView.Repaint();
                var target = GetTargetRect(gameView);
                var renderTexture = GetRenderTexture(gameView);
                if (target.width <= 0f || target.height <= 0f || renderTexture == null ||
                    renderTexture.width <= 0 || renderTexture.height <= 0)
                    return JsonUtility.ToJson(new GameViewState { state = "layout" });

                if (EditorApplication.isPaused)
                {
                    EditorApplication.isPaused = false;
                }
                return JsonUtility.ToJson(new GameViewState
                {
                    state = "ready",
                    renderWidth = renderTexture.width,
                    renderHeight = renderTexture.height
                });
            }
            catch
            {
                EditorApplication.isPaused = true;
                throw;
            }
        }

        internal static bool CanCompleteDispatch(BridgeRequest request)
        {
            if (!EditorApplication.isPlaying)
                return true;

            long serial;
            if (!PendingDispatches.TryGetValue(request.id, out serial))
            {
                serial = Schedule(request);
                PendingDispatches.Add(request.id, serial);
                return false;
            }
            return GameInputDriverBehaviour.IsSettled(serial);
        }

        public static void CompleteDispatch(BridgeRequest request)
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Game input requires a running game.");

            long serial;
            if (!PendingDispatches.TryGetValue(request.id, out serial))
                throw new InvalidOperationException("Game input was not scheduled.");
            PendingDispatches.Remove(request.id);
            GameInputDriverBehaviour.ThrowIfFailed(serial);
        }

        private static long Schedule(BridgeRequest request)
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Game input requires a running game.");

            if (EditorApplication.isPaused)
            {
                EditorApplication.isPaused = false;
                EditorApplication.QueuePlayerLoopUpdate();
            }

            var gameView = GetGameView(false);
            GameInputDriverBehaviour.Ensure();
            RuntimeUiInputSetup.Ensure();
            var frameWidth = PositiveInt(request, "frameWidth");
            var frameHeight = PositiveInt(request, "frameHeight");
            var action = request.action ?? string.Empty;
            Action input;
            switch (action)
            {
                case "click":
                    input = Click(gameView, Point(request, gameView, frameWidth, frameHeight), Button(request), 1);
                    break;
                case "double-click":
                    input = Click(gameView, Point(request, gameView, frameWidth, frameHeight), Button(request), 2);
                    break;
                case "hover":
                    input = MouseInput(gameView, EventType.MouseMove, Point(request, gameView, frameWidth, frameHeight), 0, 0, Vector2.zero);
                    break;
                case "mouse-down":
                    input = MouseInput(gameView, EventType.MouseDown, Point(request, gameView, frameWidth, frameHeight), Button(request), ClickCount(request), Vector2.zero);
                    break;
                case "mouse-drag":
                    input = MouseInput(gameView, EventType.MouseDrag, Point(request, gameView, frameWidth, frameHeight), Button(request), 0, Vector2.zero);
                    break;
                case "mouse-up":
                    input = MouseInput(gameView, EventType.MouseUp, Point(request, gameView, frameWidth, frameHeight), Button(request), ClickCount(request), Vector2.zero);
                    break;
                case "scroll":
                    input = MouseInput(
                        gameView,
                        EventType.ScrollWheel,
                        Point(request, gameView, frameWidth, frameHeight),
                        0,
                        0,
                        new Vector2(Number(request, "deltaX"), Number(request, "deltaY")));
                    break;
                case "key-down":
                    input = KeyInput(EventType.KeyDown, request.name, true);
                    break;
                case "key-up":
                    input = KeyInput(EventType.KeyUp, request.name, false);
                    break;
                case "type-text":
                    input = TextInput(request.name);
                    break;
                default:
                    throw new ArgumentException("Unknown game input action: " + action, "action");
            }
            gameView.Repaint();
            return GameInputDriverBehaviour.Enqueue(input);
        }

        public static string PauseAndCapture()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("The game is not running.");

            var texture = ReadGameViewTexture();
            InputSystemGameInput.ReleaseControl();
            EditorApplication.isPaused = true;
            try
            {
                var directory = Path.Combine(BridgePaths.RuntimeRoot, "Screenshots");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "game.png");
                var temporary = path + ".tmp";
                File.WriteAllBytes(temporary, texture.EncodeToPNG());
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(temporary, path);
                return JsonUtility.ToJson(new GameFrame
                {
                    screenshot = path,
                    width = texture.width,
                    height = texture.height
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        public static string Pause()
        {
            if (!EditorApplication.isPlaying)
                return "Game is offline.";
            InputSystemGameInput.ReleaseControl();
            EditorApplication.isPaused = true;
            return "Game paused.";
        }

        public static string SetTimeScale(string rawValue)
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Game time scale requires Play Mode.");
            float value;
            if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 100f)
                throw new ArgumentException("Game time scale must be between 0 and 100.", "rawValue");
            var previous = Time.timeScale;
            Time.timeScale = value;
            return previous.ToString("R", CultureInfo.InvariantCulture);
        }

        public static string MultiplyTimeScale(string rawValue)
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Game time scale requires Play Mode.");
            float multiplier;
            if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out multiplier) ||
                float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0f)
                throw new ArgumentException("Game time scale multiplier must be positive.", "rawValue");
            var previous = Time.timeScale;
            Time.timeScale = Mathf.Min(100f, previous * multiplier);
            return previous.ToString("R", CultureInfo.InvariantCulture);
        }

        public static string GameTime()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Game time requires Play Mode.");
            return Time.timeAsDouble.ToString("R", CultureInfo.InvariantCulture);
        }

        private static EditorWindow GetGameView(bool focus)
        {
            return EditorWindow.GetWindow(GameViewType, false, "Game", focus);
        }

        private static Rect GetTargetRect(EditorWindow gameView)
        {
            if (TargetInView == null)
                throw new MissingMemberException("This Unity version does not expose the Game View target rectangle.");
            return (Rect)TargetInView.GetValue(gameView, null);
        }

        private static RenderTexture GetRenderTexture(EditorWindow gameView)
        {
            if (GameRenderTexture == null)
                throw new MissingMemberException("This Unity version does not expose the Game View render texture.");
            return GameRenderTexture.GetValue(gameView) as RenderTexture;
        }

        internal static bool HasRenderedFrame()
        {
            if (!EditorApplication.isPlaying)
                return false;
            var gameView = GetGameView(false);
            Application.runInBackground = true;
            EditorApplication.QueuePlayerLoopUpdate();
            gameView.Repaint();
            var target = GetTargetRect(gameView);
            var renderTexture = GetRenderTexture(gameView);
            return target.width > 0f && target.height > 0f && renderTexture != null &&
                renderTexture.width > 0 && renderTexture.height > 0;
        }

        private static Vector2 Point(BridgeRequest request, EditorWindow gameView, int frameWidth, int frameHeight)
        {
            var x = Number(request, "x");
            var y = Number(request, "y");
            if (x < 0f || x >= frameWidth || y < 0f || y >= frameHeight)
                throw new ArgumentOutOfRangeException("values", "Game input point is outside the last screenshot.");
            var renderTexture = GetRenderTexture(gameView);
            if (renderTexture == null || renderTexture.width <= 0 || renderTexture.height <= 0)
                throw new BridgeNotReadyException("Unity Game View has no rendered frame yet.");
            return new Vector2(
                (x + 0.5f) * renderTexture.width / frameWidth,
                (y + 0.5f) * renderTexture.height / frameHeight);
        }

        private static int Button(BridgeRequest request)
        {
            var value = Text(request, "button");
            if (value == "left") return 0;
            if (value == "right") return 1;
            if (value == "middle") return 2;
            throw new ArgumentException("Mouse button must be left, right, or middle.", "values");
        }

        private static int ClickCount(BridgeRequest request)
        {
            foreach (var entry in request.values ?? Array.Empty<PropertyValue>())
            {
                int value;
                if (string.Equals(entry.path, "clickCount", StringComparison.Ordinal) &&
                    int.TryParse(entry.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0)
                    return value;
            }
            return 1;
        }

        private static Action Click(EditorWindow gameView, Vector2 point, int button, int count)
        {
            return delegate
            {
                for (var click = 1; click <= count; click++)
                {
                    SendMouse(gameView, EventType.MouseDown, point, button, click, Vector2.zero);
                    SendMouse(gameView, EventType.MouseUp, point, button, click, Vector2.zero);
                }
            };
        }

        private static Action MouseInput(EditorWindow gameView, EventType type, Vector2 point, int button, int clickCount, Vector2 delta)
        {
            return delegate { SendMouse(gameView, type, point, button, clickCount, delta); };
        }

        private static void SendMouse(EditorWindow gameView, EventType type, Vector2 point, int button, int clickCount, Vector2 delta)
        {
            var renderTexture = GetRenderTexture(gameView);
            if (renderTexture == null || renderTexture.width <= 0 || renderTexture.height <= 0)
                throw new BridgeNotReadyException("Unity Game View has no rendered frame yet.");
            var screenPoint = new Vector2(point.x, renderTexture.height - point.y);
            InputSystemGameInput.SetMouse(type, screenPoint, button, clickCount, delta);
        }

        private static Action KeyInput(EventType type, string name, bool pressed)
        {
            return delegate { InputSystemGameInput.SetKey(name, pressed); };
        }

        private static Action TextInput(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("Text is required.", "name");
            return delegate { InputSystemGameInput.TypeText(text); };
        }

        private static Texture2D ReadGameViewTexture()
        {
            var renderTexture = GetRenderTexture(GetGameView(false));
            if (renderTexture == null || renderTexture.width <= 0 || renderTexture.height <= 0)
                throw new BridgeNotReadyException("Unity Game View has no rendered frame yet.");

            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = renderTexture;
                var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0, false);
                texture.Apply(false, false);
                FlipVertically(texture);
                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static void FlipVertically(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            var width = texture.width;
            var height = texture.height;
            for (var y = 0; y < height / 2; y++)
            {
                var opposite = height - y - 1;
                for (var x = 0; x < width; x++)
                {
                    var top = y * width + x;
                    var bottom = opposite * width + x;
                    var swap = pixels[top];
                    pixels[top] = pixels[bottom];
                    pixels[bottom] = swap;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }


        private static int PositiveInt(BridgeRequest request, string name)
        {
            int value;
            if (!int.TryParse(Text(request, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value <= 0)
                throw new ArgumentException(name + " must be a positive integer.", "values");
            return value;
        }

        private static float Number(BridgeRequest request, string name)
        {
            float value;
            if (!float.TryParse(Text(request, name), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException(name + " must be a finite number.", "values");
            return value;
        }

        private static string Text(BridgeRequest request, string name)
        {
            foreach (var value in request.values ?? Array.Empty<PropertyValue>())
                if (string.Equals(value.path, name, StringComparison.Ordinal))
                    return value.value;
            throw new ArgumentException("Missing game input value: " + name, "values");
        }
    }
}
