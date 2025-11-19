using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

namespace SceneAPI.Modules
{
    public class GetLogsModule
    {
        private static List<LogEntry> logEntries = new List<LogEntry>();
        private static bool isListening = false;

        [Serializable]
        public class LogEntry
        {
            public string message;
            public string stackTrace;
            public string type;
            public string timestamp;

            public LogEntry(string message, string stackTrace, LogType type)
            {
                this.message = message;
                this.stackTrace = stackTrace;
                this.type = type.ToString();
                this.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            }
        }

        static GetLogsModule()
        {
            StartListening();
        }

        private static void StartListening()
        {
            if (!isListening)
            {
                Application.logMessageReceived += OnLogMessageReceived;
                isListening = true;
            }
        }

        private static void OnLogMessageReceived(string message, string stackTrace, LogType type)
        {
            logEntries.Add(new LogEntry(message, stackTrace, type));

            // Ограничиваем количество логов до 1000 для производительности
            if (logEntries.Count > 1000)
            {
                logEntries.RemoveAt(0);
            }
        }

        public string Execute(string requestBody)
        {
            try
            {
                // Получаем последние 100 записей
                var recentLogs = logEntries.TakeLast(100).ToList();

                var response = new
                {
                    success = true,
                    logs = recentLogs,
                    totalCount = logEntries.Count,
                    returnedCount = recentLogs.Count
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