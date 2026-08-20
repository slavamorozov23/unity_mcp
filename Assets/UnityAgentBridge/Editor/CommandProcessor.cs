using System;
using UnityEditor;

namespace UnityAgentBridge.Editor
{
    internal static class CommandProcessor
    {
        private static double playStartRequestedUntil;

        public static bool CanExecute(BridgeRequest request)
        {
            if (request == null)
                return true;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return false;
            if (EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying)
                return request.command == "get-status" || request.command == "prepare-game-interaction";

            switch (request.command)
            {
                case "dispatch-game-input":
                    return !EditorApplication.isPlaying || GameInteractionService.CanCompleteDispatch(request);
                case "pause-and-capture-game":
                    return !EditorApplication.isPlaying ||
                        (GameInteractionService.InputSettled && GameInteractionService.HasRenderedFrame());
                case "control-animator":
                case "get-animator-runtime-state":
                    return EditorApplication.isPlaying || EditorApplication.timeSinceStartup >= playStartRequestedUntil;
                default:
                    return true;
            }
        }

        public static BridgeResponse Execute(BridgeRequest request)
        {
            if (request == null)
                throw new ArgumentNullException("request");
            if (string.IsNullOrWhiteSpace(request.id))
                throw new InvalidOperationException("Request id is required.");
            if (string.IsNullOrWhiteSpace(request.command))
                throw new InvalidOperationException("Request command is required.");

            var response = new BridgeResponse { id = request.id, ok = true };
            var saveAfterMutation = false;
            switch (request.command)
            {
                case "get-scene-tree":
                    response.objects = SceneService.GetTree();
                    break;
                case "get-object-info":
                    response.objectInfo = SceneService.GetObjectInfo(request);
                    break;
                case "resolve-object-path":
                    var resolvedObject = ScenePath.ResolveObject(request.path);
                    EditorPresentationService.ShowSceneObject(resolvedObject);
                    response.message = ScenePath.For(resolvedObject);
                    break;
                case "list-component-types":
                    response.componentTypes = ComponentService.GetAllComponentTypeNames();
                    break;
                case "modify-component":
                    string modifyWarning;
                    response.objectInfo = ComponentService.Modify(request, out modifyWarning);
                    response.message = modifyWarning;
                    saveAfterMutation = true;
                    break;
                case "add-component":
                    string warning;
                    int addedComponentIndex;
                    response.objectInfo = ComponentService.Add(request, out warning, out addedComponentIndex);
                    response.componentIndex = addedComponentIndex;
                    response.message = warning;
                    saveAfterMutation = true;
                    break;
                case "remove-component":
                    response.objectInfo = ComponentService.Remove(request);
                    saveAfterMutation = true;
                    break;
                case "execute-component-action":
                    response.message = InspectorDiagnosticService.Execute(request);
                    saveAfterMutation = true;
                    break;
                case "execute-asset-action":
                    response.message = AssetService.ExecuteAction(request.path, request.action);
                    break;
                case "delete-object":
                    ObjectService.Delete(request.path);
                    response.message = "Object deleted.";
                    saveAfterMutation = true;
                    break;
                case "duplicate-object":
                    response.objectInfo = ObjectService.Duplicate(request.path);
                    saveAfterMutation = true;
                    break;
                case "list-prefabs":
                    response.prefabs = ObjectService.ListPrefabs(request.path);
                    break;
                case "create-empty-object":
                    response.objectInfo = PrefabService.CreateEmpty(request.destinationPath, request.name);
                    saveAfterMutation = true;
                    break;
                case "save-scenes":
                    ScenePersistenceService.SaveBeforeTransition();
                    response.message = "Open scenes saved.";
                    break;
                case "save-prefab":
                    response.prefabs = new[] { PrefabService.Save(request.path, request.destinationPath) };
                    saveAfterMutation = true;
                    break;
                case "apply-prefab":
                    string applyWarning;
                    response.objectInfo = PrefabService.Apply(
                        request.path, request.componentType, request.componentIndex, request.propertyPath, out applyWarning);
                    response.message = applyWarning;
                    saveAfterMutation = true;
                    break;
                case "instantiate-prefab":
                    response.objectInfo = PrefabService.Instantiate(request.path, request.destinationPath);
                    saveAfterMutation = true;
                    break;
                case "revert-prefab":
                    response.objectInfo = PrefabService.Revert(
                        request.path, request.componentType, request.componentIndex, request.propertyPath);
                    saveAfterMutation = true;
                    break;
                case "open-prefab":
                    response.prefabs = new[] { PrefabService.Open(request.path) };
                    break;
                case "close-prefab":
                    response.prefabs = new[] { PrefabService.Close() };
                    break;
                case "list-scenes":
                    response.scenes = SceneService.ListSceneAssets();
                    break;
                case "open-scene":
                    response.scenes = new[] { SceneService.OpenScene(request.path) };
                    break;
                case "list-creation-templates":
                    response.templates = AssetService.ListCreationTemplates();
                    break;
                case "create-asset":
                    response.assetInfo = AssetService.Create(request.templateName, request.path);
                    break;
                case "get-asset-info":
                    response.assetInfo = AssetService.GetInfo(request.path, request.propertyPath);
                    break;
                case "modify-asset":
                    response.assetInfo = AssetService.Modify(request.path, request.values);
                    break;
                case "reimport-asset":
                    response.assetInfo = AssetService.Reimport(request.path);
                    break;
                case "refresh-assets":
                    response.message = AssetRefreshService.Schedule(request.id);
                    break;
                case "get-sprite-layout":
                    response.message = AssetService.GetSpriteLayout(request.path);
                    break;
                case "mutate-sprite-layout":
                    response.message = AssetService.MutateSpriteLayout(request.path, request.action, request.json);
                    break;
                case "move-asset":
                    response.assetInfo = AssetService.Move(request.path, request.destinationPath);
                    break;
                case "delete-asset":
                    response.message = AssetService.Delete(request.path);
                    break;
                case "asset-object-picker":
                    response.candidates = AssetService.GetObjectPickerCandidates(request.path, request.propertyPath, 10);
                    break;
                case "move-object":
                    response.objectInfo = ObjectService.Move(request.path, request.destinationPath, request.siblingIndex);
                    saveAfterMutation = true;
                    break;
                case "rename-object":
                    response.objectInfo = ObjectService.Rename(request.path, request.destinationPath);
                    saveAfterMutation = true;
                    break;
                case "set-active":
                    response.objectInfo = ObjectService.SetActive(request.path, request.boolValue);
                    saveAfterMutation = true;
                    break;
                case "set-tag":
                    response.message = ObjectService.SetTag(request.path, request.name);
                    saveAfterMutation = !request.path.StartsWith("Assets/", StringComparison.Ordinal);
                    break;
                case "object-picker":
                    response.candidates = ComponentService.GetObjectPickerCandidates(request);
                    break;
                case "get-logs":
                    response.logs = DebugService.GetLast(100);
                    response.currentCompilationErrors = DebugService.GetCurrentCompilationErrors();
                    break;
                case "clear-logs":
                    DebugService.Clear();
                    response.message = "Logs cleared.";
                    break;
                case "get-status":
                    response.status = DebugService.Status();
                    break;
                case "set-play-mode":
                    bool playTransitionRequested;
                    response.message = DebugService.SetPlayMode(request.action, out playTransitionRequested);
                    playStartRequestedUntil = playTransitionRequested &&
                        (string.Equals(request.action, "start", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(request.action, "запустить", StringComparison.OrdinalIgnoreCase))
                        ? EditorApplication.timeSinceStartup + 120d
                        : 0d;
                    break;
                case "list-game-resolutions":
                    response.resolutions = GameResolutionService.List();
                    break;
                case "set-game-resolution":
                    response.resolution = GameResolutionService.Set(request.width, request.height);
                    break;
                case "list-packages":
                    response.packages = PackageService.List();
                    break;
                case "search-packages":
                    PackageService.Search(response);
                    break;
                case "add-package":
                    PackageService.AddOrUpdate(response, request.name, request.action);
                    break;
                case "remove-package":
                    PackageService.Remove(response, request.name);
                    break;
                case "list-input-axes":
                    response.axes = InputManagerService.List();
                    break;
                case "create-input-axis":
                    response.axis = InputManagerService.Create(request.name, request.values);
                    break;
                case "delete-input-axis":
                    response.message = "Axes deleted: " + InputManagerService.Delete(request.name);
                    break;
                case "prepare-game-interaction":
                    response.message = GameInteractionService.Prepare();
                    break;
                case "dispatch-game-input":
                    GameInteractionService.CompleteDispatch(request);
                    break;
                case "pause-and-capture-game":
                    response.message = GameInteractionService.PauseAndCapture();
                    break;
                case "pause-game":
                    response.message = GameInteractionService.Pause();
                    break;
                case "set-game-time-scale":
                    response.message = GameInteractionService.SetTimeScale(request.action);
                    break;
                case "multiply-game-time-scale":
                    response.message = GameInteractionService.MultiplyTimeScale(request.action);
                    break;
                case "get-game-time":
                    response.message = GameInteractionService.GameTime();
                    break;
                case "capture-scene":
                {
                    string[] screenshotLabels;
                    response.screenshots = SceneScreenshotService.Capture(request.paths, request.action, out screenshotLabels);
                    response.screenshotLabels = screenshotLabels;
                    break;
                }
                case "list-animation-clips":
                    response.message = AnimationClipService.ListClips(request.path);
                    break;
                case "get-animation-table":
                    response.message = AnimationClipService.GetTable(request.clip);
                    break;
                case "get-animation-properties":
                    response.message = AnimationClipService.GetAvailableProperties(request.path);
                    break;
                case "create-animation-clip":
                    response.message = AnimationClipService.CreateClip(request.name, request.path);
                    break;
                case "delete-animation-clip":
                    response.message = AnimationClipService.DeleteClip(request.clip);
                    break;
                case "mutate-animation-property":
                    response.message = AnimationClipService.MutateProperty(request);
                    break;
                case "animation-clip-setting":
                    response.message = AnimationClipService.ClipSetting(request);
                    break;
                case "list-animators":
                    response.message = AnimatorControllerService.ListAnimators();
                    break;
                case "get-animator":
                    response.message = AnimatorControllerService.GetAnimator(request.path);
                    break;
                case "mutate-animator":
                    response.message = AnimatorControllerService.MutateAnimator(request);
                    saveAfterMutation = true;
                    break;
                case "assign-animator-controller":
                    response.message = AnimatorControllerService.AssignController(request);
                    saveAfterMutation = true;
                    break;
                case "get-animator-motions":
                    response.message = AnimatorControllerService.GetMotions(request);
                    break;
                case "get-animator-controller":
                    response.message = AnimatorControllerService.GetController(request);
                    break;
                case "mutate-animator-controller":
                    response.message = AnimatorControllerService.MutateController(request);
                    break;
                case "mutate-animator-state":
                    response.message = AnimatorControllerService.MutateState(request);
                    break;
                case "assign-animator-state-motion":
                    response.message = AnimatorControllerService.AssignStateMotion(request);
                    break;
                case "mutate-animator-transition":
                    response.message = AnimatorControllerService.MutateTransition(request);
                    break;
                case "mutate-animator-parameter":
                    response.message = AnimatorControllerService.MutateParameter(request);
                    break;
                case "mutate-animator-layer":
                    response.message = AnimatorControllerService.MutateLayer(request);
                    break;
                case "mutate-animator-state-machine":
                    response.message = AnimatorControllerService.MutateStateMachine(request);
                    break;
                case "mutate-animator-blend-tree":
                    response.message = AnimatorControllerService.MutateBlendTree(request);
                    break;
                case "control-animator":
                    response.message = AnimatorRuntimeService.Control(request);
                    break;
                case "get-animator-runtime-state":
                    response.message = AnimatorRuntimeService.GetState(request.path);
                    break;
                default:
                    throw new InvalidOperationException("Unknown or excluded command: " + request.command);
            }

            if (saveAfterMutation)
                ScenePersistenceService.SaveAfterBridgeMutation();

            return response;
        }
    }
}
