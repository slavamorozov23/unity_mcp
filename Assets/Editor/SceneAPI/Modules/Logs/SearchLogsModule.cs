using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

namespace SceneAPI.Modules
{
    public class SearchLogsModule
    {
        [Serializable]
        public class SearchRequest
        {
            public string query;
            public int maxResults = 100;
            public bool caseSensitive = false;
        }
        
        public string Execute(string requestBody)
        {
            try
            {
                // Возвращаем полный список логов без фильтрации
                var getLogsModule = new GetLogsModule();
                var logsResponse = getLogsModule.Execute("{}");
                var logsData = JsonConvert.DeserializeObject<dynamic>(logsResponse);
                
                if (logsData.success != true)
                {
                    return logsResponse; // Возвращаем ошибку от GetLogsModule
                }
                
                var allLogs = JsonConvert.DeserializeObject<List<GetLogsModule.LogEntry>>(logsData.logs.ToString());
                
                var response = new
                {
                    success = true,
                    logs = allLogs,
                    totalCount = allLogs.Count
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