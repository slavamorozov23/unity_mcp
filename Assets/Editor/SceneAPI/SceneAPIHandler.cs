using System.Net;
using Newtonsoft.Json;
using SceneAPI.Modules;

namespace SceneAPI
{
    public class SceneAPIHandler
    {
        public SceneAPIHandler()
        {
        }

        public string HandleRequest(string method, string path, HttpListenerContext context)
        {
            return $"{method} {path}" switch
            {
                // Scene endpoints
                "GET /scene" => GetHierarchyModule.Execute(),
                "POST /scene/open" => SceneManagementModule.OpenScene(context),
                "GET /build/scenes" => SceneManagementModule.GetBuildScenes(),
                "POST /build/scenes/add" => SceneManagementModule.AddSceneToBuild(context),
                "DELETE /build/scenes/remove" => SceneManagementModule.RemoveSceneFromBuild(context),
                // GameObject endpoints
                "POST /objects/create" => CreateObjectModule.Execute(context),
                "DELETE /objects/delete" => DeleteObjectModule.Execute(context),
                // Component endpoints
                "GET /objects/components" => GetComponentsModule.Execute(context),
                "POST /objects/components/add" => AddComponentModule.Execute(context),
                "PUT /objects/components/modify" => ModifyComponentModule.Execute(context),
                "DELETE /objects/components/remove" => RemoveComponentModule.Execute(context),
                _ => JsonConvert.SerializeObject(new { error = "Endpoint not found" }),
            };
        }
    }
}