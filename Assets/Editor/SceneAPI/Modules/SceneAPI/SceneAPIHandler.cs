using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using SceneAPI.Modules;
using UnityEngine;

namespace SceneAPI
{
    public class SceneAPIHandler
    {
        public string HandleRequest(string method, string path, HttpListenerContext context)
        {
            // Special handling for "Smart" modules that need complex parameters or logic
            if (method == "POST" && path == "/debug/snapshot")
            {
                return new SmartSnapshotModule().Execute(GetRequestBody(context));
            }
            
            if (method == "POST" && path == "/debug/tilemap/snapshot")
            {
                return new TilemapSnapshotModule().Execute(GetRequestBody(context));
            }

            return $"{method} {path}" switch
            {
                // --- Scene Management ---
                "GET /scene" => GetHierarchyModule.Execute(),
                "POST /scene/open" => SceneManagementModule.OpenScene(context),
                "GET /build/scenes" => SceneManagementModule.GetBuildScenes(),
                "POST /build/scenes/add" => SceneManagementModule.AddSceneToBuild(context),
                "DELETE /build/scenes/remove" => SceneManagementModule.RemoveSceneFromBuild(context),
                "GET /scene/status" => new GetSceneStatusModule().Execute("{}"),
                "POST /scene/control" => new PlayControlModule().Execute(GetRequestBody(context)),

                // --- Game Objects ---
                "POST /objects/create" => CreateObjectModule.Execute(context),
                "DELETE /objects/delete" => DeleteObjectModule.Execute(context),
                "PUT /objects/move" => MoveObjectModule.Execute(context),
                "PUT /objects/reset" => ResetObjectModule.Execute(context),
                "PUT /objects/rename" => RenameObjectModule.Execute(context),
                "PUT /objects/active" => SetObjectActiveModule.Execute(context),
                
                // --- Prefabs ---
                "POST /objects/create/prefab" => CreateObjectFromPrefabModule.Execute(context),
                "POST /objects/save/prefab" => SaveObjectAsPrefabModule.Execute(context),
                "POST /objects/instantiate/prefab" => InstantiatePrefabModule.Execute(context),
                "GET /prefabs" => GetPrefabsListModule.Execute(context),

                // --- Components ---
                "GET /objects/components" => GetComponentsModule.Execute(context),
                "POST /objects/components/add" => AddComponentModule.Execute(context),
                "PUT /objects/components/modify" => ModifyComponentModule.Execute(context),
                "DELETE /objects/components/remove" => RemoveComponentModule.Execute(context),
                "GET /components/variants" => PickerComponentVariantsModule.Execute(context),

                // --- Helpers & Pickers ---
                "GET /objects/picker" => ObjectPickerModule.Execute(context),
                
                // --- Logs ---
                "GET /logs" => new GetLogsModule().Execute("{}"),
                "POST /logs/search" => new SearchLogsModule().Execute(GetRequestBody(context)),

                // --- Assets & Files ---
                "POST /templates/search" => new GetCreationTemplatesModule().Execute(GetRequestBody(context)),
                "POST /templates/create" => new CreateFromTemplateModule().Execute(GetRequestBody(context)),
                "POST /templates/create/test" => new CreateFromTemplateModule().ExecuteTest(),
                "POST /assets/info" => new GetAssetInfoModule().Execute(GetRequestBody(context)),
                "PUT /assets/modify" => new ModifyAssetModule().Execute(GetRequestBody(context)),
                "POST /assets/picker/options" => new GetAssetPickerOptionsModule().Execute(GetRequestBody(context)),

                // --- Tilemaps ---
                "GET /tilemaps" => new GetTilemapsModule().Execute(GetRequestBody(context)),
                "POST /tilemaps/paint" => new PaintTileModule().Execute(GetRequestBody(context)),
                "POST /tilemaps/assets" => new ManageTileAssetModule().Execute(GetRequestBody(context)),

                // --- Animation ---
                "POST /animation/info" => new GetAnimInfoModule().Execute(GetRequestBody(context)),
                "POST /animation/modify" => new ManageAnimPropertyModule().Execute(GetRequestBody(context)),

                // --- Input ---
                "GET /input/constants" => new GetInputConstantsModule().Execute(GetRequestBody(context)),
                "POST /input/axes" => new ManageInputAxisModule().Execute(GetRequestBody(context)),

                _ => JsonConvert.SerializeObject(new { error = $"Endpoint not found: {method} {path}" }),
            };
        }

        private string GetRequestBody(HttpListenerContext context)
        {
            try
            {
                if (context.Request.HasEntityBody)
                {
                    using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                    {
                        return reader.ReadToEnd();
                    }
                }
                return "{}";
            }
            catch
            {
                return "{}";
            }
        }
    }
}