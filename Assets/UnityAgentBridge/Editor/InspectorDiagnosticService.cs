using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityAgentBridge.Editor
{
    internal static class InspectorDiagnosticService
    {
        private const string LegacyInputModule = "UnityEngine.EventSystems.StandaloneInputModule";
        private const string InputSystemUiModule = "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";
        private const string ReplaceInputModule = "replace-with-input-system-ui-module";

        internal static InspectorWarningData[] Warnings(Component component)
        {
            if (!IsLegacyInputModule(component) || !InputSystemSetupService.IsReady)
                return Array.Empty<InspectorWarningData>();

            var oldInputDisabled = PackageService.ActiveInputHandler == 1;
            return new[]
            {
                new InspectorWarningData
                {
                    severity = oldInputDisabled ? "error" : "info",
                    message = oldInputDisabled
                        ? "StandaloneInputModule uses the old InputManager, but the old InputManager is disabled. It will not work."
                        : "StandaloneInputModule uses the old InputManager while the new Input System is enabled."
                }
            };
        }

        internal static InspectorActionData[] Actions(Component component)
        {
            if (!IsLegacyInputModule(component) || !InputSystemSetupService.IsReady)
                return Array.Empty<InspectorActionData>();

            return new[]
            {
                new InspectorActionData
                {
                    id = ReplaceInputModule,
                    label = "Replace with InputSystemUIInputModule"
                }
            };
        }

        internal static string Execute(BridgeRequest request)
        {
            ComponentService.EnsureEditMode();
            if (!string.Equals(request.action, ReplaceInputModule, StringComparison.Ordinal))
                throw new InvalidOperationException("Unknown Inspector action: " + request.action);

            var gameObject = ScenePath.ResolveObject(request.path);
            EditorPresentationService.ShowComponentOwner(gameObject);
            var component = ComponentService.ResolveAttachedComponent(gameObject, request.componentType, request.componentIndex);
            if (!IsLegacyInputModule(component))
                throw new InvalidOperationException("The action belongs to StandaloneInputModule, not " + component.GetType().FullName + ".");
            InputSystemSetupService.RequireReady();

            var replacementType = Type.GetType(InputSystemUiModule, false);
            if (replacementType == null || !typeof(Component).IsAssignableFrom(replacementType))
                throw new InvalidOperationException("InputSystemUIInputModule is unavailable after Input System setup.");

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Unity Agent Bridge: Replace Input Module");
            try
            {
                Undo.DestroyObjectImmediate(component);
                var replacement = gameObject.GetComponent(replacementType) ?? Undo.AddComponent(gameObject, replacementType);
                if (replacement == null)
                    throw new InvalidOperationException("Unity did not create InputSystemUIInputModule.");
                EditorUtility.SetDirty(gameObject);
                if (gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                Undo.CollapseUndoOperations(group);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }

            return "Replaced StandaloneInputModule with InputSystemUIInputModule on " + ScenePath.For(gameObject) + ".";
        }

        private static bool IsLegacyInputModule(Component component)
        {
            return component != null && string.Equals(component.GetType().FullName, LegacyInputModule, StringComparison.Ordinal);
        }
    }
}
