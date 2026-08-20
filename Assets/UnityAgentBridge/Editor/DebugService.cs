using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnityAgentBridge.Editor
{
    [InitializeOnLoad]
    internal static class DebugService
    {
        private static readonly object FileLock = new object();
        private static readonly string SessionLogPath;
        private static readonly string CompilationLogPath;
        private static readonly List<LogData> CurrentCompilationErrors = new List<LogData>();
        private static string playModeStatus;

        static DebugService()
        {
            BridgePaths.EnsureRuntimeDirectories();
            var process = Process.GetCurrentProcess();
            SessionLogPath = Path.Combine(
                BridgePaths.RuntimeRoot,
                "Logs-" + process.Id + "-" + process.StartTime.ToUniversalTime().Ticks + ".jsonl");
            CompilationLogPath = Path.Combine(BridgePaths.RuntimeRoot, "CurrentCompilationErrors.json");
            playModeStatus = EditorApplication.isPlaying ? "игра запущена" : "игра оффлайн";
            Application.logMessageReceivedThreaded += OnLog;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
        }

        public static LogData[] GetLast(int count)
        {
            count = Mathf.Clamp(count, 1, 100);
            lock (FileLock)
            {
                if (!File.Exists(SessionLogPath))
                    return Array.Empty<LogData>();
                return File.ReadLines(SessionLogPath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Reverse()
                    .Take(count)
                    .Reverse()
                    .Select(JsonUtility.FromJson<LogData>)
                    .Where(entry => entry != null)
                    .ToArray();
            }
        }

        public static void Clear()
        {
            lock (FileLock)
                File.WriteAllText(SessionLogPath, string.Empty);
        }

        public static LogData[] GetCurrentCompilationErrors()
        {
            lock (FileLock)
            {
                if (!File.Exists(CompilationLogPath))
                    return Array.Empty<LogData>();
                var data = JsonUtility.FromJson<LogFileData>(File.ReadAllText(CompilationLogPath));
                return data == null || data.entries == null ? Array.Empty<LogData>() : data.entries;
            }
        }

        private static void OnCompilationStarted(object context)
        {
            lock (FileLock)
            {
                CurrentCompilationErrors.Clear();
                File.WriteAllText(SessionLogPath, string.Empty);
                SaveCompilationErrors();
            }
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            lock (FileLock)
            {
                foreach (var message in messages.Where(item => item.type == CompilerMessageType.Error))
                {
                    CurrentCompilationErrors.Add(new LogData
                    {
                        timestampUtc = DateTime.UtcNow.ToString("O"),
                        type = "Error",
                        message = message.message,
                        stackTrace = message.file + ":" + message.line + ":" + message.column
                    });
                }
                if (CurrentCompilationErrors.Count > 20)
                    CurrentCompilationErrors.RemoveRange(0, CurrentCompilationErrors.Count - 20);
                SaveCompilationErrors();
            }
        }

        private static void SaveCompilationErrors()
        {
            File.WriteAllText(
                CompilationLogPath,
                JsonUtility.ToJson(new LogFileData { entries = CurrentCompilationErrors.ToArray() }, false));
        }

        public static string Status()
        {
            return playModeStatus;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    playModeStatus = "игра запускается";
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    playModeStatus = "игра запущена";
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    playModeStatus = "игра останавливается";
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    playModeStatus = "игра оффлайн";
                    break;
            }
        }

        public static string SetPlayMode(string action, out bool transitionRequested)
        {
            transitionRequested = false;
            if (string.Equals(action, "запустить", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action, "start", StringComparison.OrdinalIgnoreCase))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return "Game is already running.";
                ScenePersistenceService.SaveBeforeTransition();
                EditorApplication.EnterPlaymode();
                transitionRequested = true;
                return "Play Mode start requested.";
            }

            if (string.Equals(action, "остановить", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action, "stop", StringComparison.OrdinalIgnoreCase))
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    return "Game is already stopped.";
                EditorApplication.ExitPlaymode();
                transitionRequested = true;
                return "Play Mode stop requested.";
            }

            throw new ArgumentException("Play Mode action must be 'запустить'/'start' or 'остановить'/'stop'.", "action");
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            var entry = new LogData
            {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                type = type.ToString(),
                message = condition,
                stackTrace = stackTrace
            };

            try
            {
                lock (FileLock)
                    File.AppendAllText(SessionLogPath, JsonUtility.ToJson(entry, false) + Environment.NewLine);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
            }
        }
    }
}
