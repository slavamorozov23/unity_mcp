using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace SceneAPI.Modules.Unified
{
    public class ModuleResult
    {
        public bool success { get; set; }
        public string action { get; set; }
        public object data { get; set; }
        public string error { get; set; }
    }

    public static class ModuleUtils
    {
        public static string ReadRequestBody(HttpListenerContext context)
        {
            try
            {
                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch
            {
                return "{}";
            }
        }

        public static string JsonResponse(ModuleResult result)
        {
            try
            {
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception)
            {
                return JsonConvert.SerializeObject(new ModuleResult { success = false, action = result?.action ?? "unknown", data = null, error = "Failed to serialize response" });
            }
        }

        public static ModuleResult Success(string action, object data) => new ModuleResult { success = true, action = action, data = data, error = null };
        public static ModuleResult Error(string action, string error) => new ModuleResult { success = false, action = action, data = null, error = error };
    }

    public abstract class ModuleBase
    {
        protected static string ToJsonSuccess(string action, object data) => ModuleUtils.JsonResponse(ModuleUtils.Success(action, data));
        protected static string ToJsonError(string action, string error) => ModuleUtils.JsonResponse(ModuleUtils.Error(action, error));
        protected static string ReadBody(HttpListenerContext context) => ModuleUtils.ReadRequestBody(context);
    }
}