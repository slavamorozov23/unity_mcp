using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

namespace SceneAPI.Modules
{
    public class PlayControlModule
    {
        [Serializable]
        public class PlayControlRequest
        {
            public string action; // "start", "stop", "pause", "resume"
        }

        private delegate bool PlayControlAction(out string newState);

        private readonly Dictionary<string, PlayControlAction> _actions;

        public PlayControlModule()
        {
            _actions = new Dictionary<string, PlayControlAction>(StringComparer.OrdinalIgnoreCase)
            {
                { "start", StartPlayback },
                { "запустить", StartPlayback },
                { "stop", StopPlayback },
                { "остановить", StopPlayback },
                { "pause", PausePlayback },
                { "пауза", PausePlayback },
                { "resume", ResumePlayback },
                { "продолжить", ResumePlayback }
            };
        }

        private bool StartPlayback(out string newState)
        {
            newState = "starting";
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
                return true;
            }
            return false;
        }

        private bool StopPlayback(out string newState)
        {
            newState = "stopping";
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return true;
            }
            return false;
        }

        private bool PausePlayback(out string newState)
        {
            newState = "paused";
            if (EditorApplication.isPlaying && !EditorApplication.isPaused)
            {
                EditorApplication.isPaused = true;
                return true;
            }
            return false;
        }

        private bool ResumePlayback(out string newState)
        {
            newState = "resumed";
            if (EditorApplication.isPlaying && EditorApplication.isPaused)
            {
                EditorApplication.isPaused = false;
                return true;
            }
            return false;
        }

        public string Execute(string requestBody)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<PlayControlRequest>(requestBody);

                if (string.IsNullOrEmpty(request.action))
                {
                    var errorResponse = new
                    {
                        success = false,
                        error = "Action parameter is required. Valid actions: " + string.Join(", ", _actions.Keys.Select(k => k.ToLower()).Distinct())
                    };
                    return JsonConvert.SerializeObject(errorResponse, Formatting.Indented);
                }

                string previousState = GetCurrentState();
                
                if (_actions.TryGetValue(request.action, out var playAction))
                {
                    bool actionPerformed = playAction(out string newState);
                    
                    var response = new
                    {
                        success = true,
                        action = request.action,
                        actionPerformed,
                        previousState,
                        newState,
                        currentStatus = new
                        {
                            isPlaying = EditorApplication.isPlaying,
                            isPaused = EditorApplication.isPaused,
                            isCompiling = EditorApplication.isCompiling
                        },
                        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    };
                    return JsonConvert.SerializeObject(response, Formatting.Indented);
                }
                else
                {
                    var invalidActionResponse = new
                    {
                        success = false,
                        error = $"Invalid action '{request.action}'. Valid actions: " + string.Join(", ", _actions.Keys.Select(k => k.ToLower()).Distinct())
                    };
                    return JsonConvert.SerializeObject(invalidActionResponse, Formatting.Indented);
                }
            }
            catch (Exception ex)
            {
                var errorResponse = new
                {
                    success = false,
                    error = ex.Message
                };
                return JsonConvert.SerializeObject(errorResponse, Formatting.Indented);
            }
        }

        private string GetCurrentState()
        {
            if (EditorApplication.isCompiling)
                return "compiling";
            if (EditorApplication.isPlaying)
                return EditorApplication.isPaused ? "paused" : "playing";
            return "stopped";
        }
    }
}