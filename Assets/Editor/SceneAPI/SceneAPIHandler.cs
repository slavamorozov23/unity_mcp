using System.Net;
using Newtonsoft.Json;
using SceneAPI.Handlers;

namespace SceneAPI
{
    public class SceneAPIHandler
    {
        private readonly SceneHandler sceneHandler;
        private readonly GameObjectHandler gameObjectHandler;
        private readonly ComponentHandler componentHandler;

        public SceneAPIHandler()
        {
            sceneHandler = new SceneHandler();
            gameObjectHandler = new GameObjectHandler();
            componentHandler = new ComponentHandler();
        }

        public string HandleRequest(string method, string path, HttpListenerContext context)
        {
            switch ($"{method} {path}")
            {
                // Scene endpoints
                case "GET /scene":
                    return sceneHandler.GetSceneHierarchy();
                case "POST /scene/open":
                    return sceneHandler.OpenScene(context);
                case "GET /build/scenes":
                    return sceneHandler.GetBuildScenes();
                case "POST /build/scenes/add":
                    return sceneHandler.AddSceneToBuild(context);
                case "DELETE /build/scenes/remove":
                    return sceneHandler.RemoveSceneFromBuild(context);
                
                // GameObject endpoints
                case "POST /objects/create":
                    return gameObjectHandler.CreateObject(context);
                case "DELETE /objects/delete":
                    return gameObjectHandler.DeleteObject(context);
                
                // Component endpoints
                case "GET /objects/components":
                    return componentHandler.GetObjectComponents(context);
                case "POST /objects/components/add":
                    return componentHandler.AddComponent(context);
                case "PUT /objects/components/modify":
                    return componentHandler.ModifyComponent(context);
                case "DELETE /objects/components/remove":
                    return componentHandler.RemoveComponent(context);
                
                default:
                    return JsonConvert.SerializeObject(new { error = "Endpoint not found" });
            }
        }
    }
}