using System;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

namespace SceneAPI.Modules
{
    public class GetSceneStatusModule
    {
        public string Execute(string requestBody)
        {
            try
            {
                bool isPlaying = EditorApplication.isPlaying;
                bool isPaused = EditorApplication.isPaused;
                bool isCompiling = EditorApplication.isCompiling;
                
                string status;
                if (isPlaying)
                {
                    status = isPaused ? "игра на паузе" : "игра запущена";
                }
                else
                {
                    status = isCompiling ? "компиляция" : "игра оффлайн";
                }
                
                var response = new
                {
                    success = true,
                    status = status,
                    isPlaying = isPlaying,
                    isPaused = isPaused,
                    isCompiling = isCompiling,
                    playModeState = EditorApplication.isPlaying ? "Playing" : "Stopped",
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                };
                
                return JsonConvert.SerializeObject(response, Formatting.Indented);
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
    }
}