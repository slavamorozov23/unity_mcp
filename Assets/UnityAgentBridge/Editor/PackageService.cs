using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditorInternal;
using UnityEngine;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace UnityAgentBridge.Editor
{
    internal static class PackageService
    {
        private const long OperationTimeoutTicks = TimeSpan.TicksPerMinute * 3;
        private static readonly string DomainToken = Guid.NewGuid().ToString("N");
        private static SearchRequest searchRequest;
        private static AddRequest addRequest;
        private static RemoveRequest removeRequest;
        private static string addIdentifier;
        private static string removeName;

        [Serializable]
        private sealed class PersistedOperation
        {
            public string kind;
            public string identifier;
            public string packageName;
            public string requestedVersion;
            public bool wasDirect;
            public string previousVersion;
            public bool inputHandlerChanged;
            public int previousInputHandler;
            public string domainToken;
            public long startedUtcTicks;
        }

        public static PackageData[] List()
        {
            return PackageManagerInfo.GetAllRegisteredPackages()
                .OrderBy(package => package.name, StringComparer.Ordinal)
                .Select(Convert)
                .ToArray();
        }

        internal static bool IsInstalled(string name)
        {
            return FindInstalled(name) != null;
        }

        internal static int ActiveInputHandler
        {
            get { return ReadActiveInputHandler(); }
        }

        public static void Search(BridgeResponse response)
        {
            if (searchRequest == null)
            {
                EditorApplication.ExecuteMenuItem("Window/Package Manager");
                searchRequest = Client.SearchAll(false);
                response.pending = true;
                return;
            }
            if (!searchRequest.IsCompleted)
            {
                response.pending = true;
                return;
            }
            var completed = searchRequest;
            searchRequest = null;
            EnsureSuccess(completed);
            response.packages = completed.Result.Select(Convert).ToArray();
        }

        public static void AddOrUpdate(BridgeResponse response, string name, string version)
        {
            name = RequiredName(name);
            version = OptionalVersion(version);
            var identifier = Identifier(name, version);
            var state = LoadOperation();
            if (state != null)
            {
                EnsureSameOperation(state, "add", identifier);
                if (addRequest == null && AddReachedAfterReload(state))
                {
                    var installed = FindInstalled(name);
                    UnityEditor.PackageManager.UI.Window.Open(name);
                    ClearOperation();
                    response.package = Convert(installed);
                    return;
                }
                EnsureNotExpired(state);
            }

            if (addRequest == null)
            {
                if (state != null)
                {
                    response.pending = true;
                    return;
                }
                if (removeRequest != null)
                    throw new InvalidOperationException("Another package operation is still running.");

                var installed = FindInstalled(name);
                state = new PersistedOperation
                {
                    kind = "add",
                    identifier = identifier,
                    packageName = name,
                    requestedVersion = version,
                    wasDirect = installed != null && installed.isDirectDependency,
                    previousVersion = installed == null ? null : installed.version,
                    domainToken = DomainToken,
                    startedUtcTicks = DateTime.UtcNow.Ticks
                };
                if (string.Equals(name, "com.unity.inputsystem", StringComparison.Ordinal))
                {
                    state.previousInputHandler = ReadActiveInputHandler();
                    state.inputHandlerChanged = state.previousInputHandler != 2;
                }
                SaveOperation(state);
                try
                {
                    if (state.inputHandlerChanged)
                        WriteActiveInputHandler(2);
                    UnityEditor.PackageManager.UI.Window.Open(name);
                    addIdentifier = identifier;
                    addRequest = Client.Add(identifier);
                }
                catch
                {
                    RestoreInputHandler(state);
                    ClearOperation();
                    throw;
                }
                response.pending = true;
                return;
            }

            if (!string.Equals(addIdentifier, identifier, StringComparison.Ordinal))
                throw new InvalidOperationException("Another package operation is still running: " + addIdentifier);
            if (!addRequest.IsCompleted)
            {
                response.pending = true;
                return;
            }

            var completed = addRequest;
            addRequest = null;
            addIdentifier = null;
            try
            {
                EnsureSuccess(completed);
                UnityEditor.PackageManager.UI.Window.Open(name);
                response.package = Convert(completed.Result);
                ClearOperation();
            }
            catch
            {
                RestoreInputHandler(state ?? LoadOperation());
                ClearOperation();
                throw;
            }
        }

        public static void Remove(BridgeResponse response, string name)
        {
            name = RequiredName(name);
            var state = LoadOperation();
            if (state != null)
            {
                EnsureSameOperation(state, "remove", name);
                if (removeRequest == null && RemoveReachedAfterReload(state))
                {
                    ClearOperation();
                    response.message = "Package removed: " + name;
                    return;
                }
                EnsureNotExpired(state);
            }

            if (removeRequest == null)
            {
                if (state != null)
                {
                    response.pending = true;
                    return;
                }
                if (addRequest != null)
                    throw new InvalidOperationException("Another package operation is still running.");
                var installed = FindInstalled(name);
                state = new PersistedOperation
                {
                    kind = "remove",
                    identifier = name,
                    packageName = name,
                    wasDirect = installed != null && installed.isDirectDependency,
                    previousVersion = installed == null ? null : installed.version,
                    domainToken = DomainToken,
                    startedUtcTicks = DateTime.UtcNow.Ticks
                };
                SaveOperation(state);
                try
                {
                    UnityEditor.PackageManager.UI.Window.Open(name);
                    removeName = name;
                    removeRequest = Client.Remove(name);
                }
                catch
                {
                    ClearOperation();
                    throw;
                }
                response.pending = true;
                return;
            }

            if (!string.Equals(removeName, name, StringComparison.Ordinal))
                throw new InvalidOperationException("Another package operation is still running: " + removeName);
            if (!removeRequest.IsCompleted)
            {
                response.pending = true;
                return;
            }

            var completed = removeRequest;
            removeRequest = null;
            removeName = null;
            try
            {
                EnsureSuccess(completed);
                response.message = "Package removed: " + completed.PackageIdOrName;
                ClearOperation();
            }
            catch
            {
                ClearOperation();
                throw;
            }
        }

        private static bool AddReachedAfterReload(PersistedOperation state)
        {
            if (string.Equals(state.domainToken, DomainToken, StringComparison.Ordinal))
                return false;
            var installed = FindInstalled(state.packageName);
            if (installed == null || !installed.isDirectDependency)
                return false;
            if (!string.IsNullOrEmpty(state.requestedVersion))
                return string.Equals(installed.version, state.requestedVersion, StringComparison.Ordinal);
            return !state.wasDirect || !string.Equals(installed.version, state.previousVersion, StringComparison.Ordinal) ||
                !string.Equals(state.domainToken, DomainToken, StringComparison.Ordinal);
        }

        private static bool RemoveReachedAfterReload(PersistedOperation state)
        {
            if (string.Equals(state.domainToken, DomainToken, StringComparison.Ordinal))
                return false;
            var installed = FindInstalled(state.packageName);
            return installed == null || !installed.isDirectDependency;
        }

        private static PackageManagerInfo FindInstalled(string name)
        {
            return PackageManagerInfo.GetAllRegisteredPackages()
                .FirstOrDefault(package => string.Equals(package.name, name, StringComparison.Ordinal));
        }

        private static void EnsureSameOperation(PersistedOperation state, string kind, string identifier)
        {
            if (!string.Equals(state.kind, kind, StringComparison.Ordinal) ||
                !string.Equals(state.identifier, identifier, StringComparison.Ordinal))
                throw new InvalidOperationException("Another package operation is still running: " + state.identifier);
        }

        private static void EnsureNotExpired(PersistedOperation state)
        {
            if (DateTime.UtcNow.Ticks - state.startedUtcTicks <= OperationTimeoutTicks)
                return;
            ClearOperation();
            throw new TimeoutException("Unity Package Manager operation did not finish within 180 seconds.");
        }

        private static string Identifier(string name, string version)
        {
            return string.IsNullOrEmpty(version) ? name : name + "@" + version;
        }

        private static string OptionalVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return null;
            version = version.Trim();
            if (version.IndexOfAny(new[] { '@', ' ', '\t', '\r', '\n' }) >= 0)
                throw new ArgumentException("Package version is invalid.", "version");
            return version;
        }

        private static string RequiredName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Package name is required.", "name");
            name = name.Trim();
            if (name.IndexOfAny(new[] { '@', ' ', '\t', '\r', '\n' }) >= 0)
                throw new ArgumentException("Package name is invalid.", "name");
            return name;
        }

        private static PackageData Convert(PackageManagerInfo package)
        {
            if (package == null)
                throw new InvalidOperationException("Package Manager completed without registering the package.");
            var dependencies = package.resolvedDependencies ?? Array.Empty<DependencyInfo>();
            return new PackageData
            {
                name = package.name,
                displayName = package.displayName,
                version = package.version,
                description = package.description,
                source = package.source.ToString(),
                direct = package.isDirectDependency,
                dependencies = dependencies.Select(dependency => new PackageDependencyData
                {
                    name = dependency.name,
                    version = dependency.version
                }).ToArray()
            };
        }

        private static void EnsureSuccess(Request request)
        {
            if (request.Status == StatusCode.Success)
                return;
            var message = request.Error == null ? "Package Manager operation failed." : request.Error.message;
            throw new InvalidOperationException(message);
        }

        private static string OperationPath
        {
            get { return Path.Combine(BridgePaths.RuntimeRoot, "package-operation.json"); }
        }

        private static PersistedOperation LoadOperation()
        {
            if (!File.Exists(OperationPath))
                return null;
            try
            {
                var state = JsonUtility.FromJson<PersistedOperation>(File.ReadAllText(OperationPath));
                if (state == null || string.IsNullOrEmpty(state.kind) || string.IsNullOrEmpty(state.identifier))
                    throw new InvalidDataException("Package operation state is invalid.");
                return state;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("Package operation state cannot be read: " + exception.Message, exception);
            }
        }

        private static void SaveOperation(PersistedOperation state)
        {
            Directory.CreateDirectory(BridgePaths.RuntimeRoot);
            var temporary = OperationPath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(state, false));
            if (File.Exists(OperationPath))
                File.Delete(OperationPath);
            File.Move(temporary, OperationPath);
        }

        private static void ClearOperation()
        {
            if (File.Exists(OperationPath))
                File.Delete(OperationPath);
            var temporary = OperationPath + ".tmp";
            if (File.Exists(temporary))
                File.Delete(temporary);
        }

        private static int ReadActiveInputHandler()
        {
            var path = Path.GetFullPath("ProjectSettings/ProjectSettings.asset");
            var assets = InternalEditorUtility.LoadSerializedFileAndForget(path);
            if (assets == null || assets.Length == 0)
                throw new InvalidOperationException("ProjectSettings.asset could not be loaded.");
            try
            {
                var serialized = new SerializedObject(assets[0]);
                var property = serialized.FindProperty("activeInputHandler");
                if (property == null)
                    throw new MissingMemberException("ProjectSettings", "activeInputHandler");
                return property.intValue;
            }
            finally
            {
                Destroy(assets);
            }
        }

        private static void WriteActiveInputHandler(int value)
        {
            var path = Path.GetFullPath("ProjectSettings/ProjectSettings.asset");
            var assets = InternalEditorUtility.LoadSerializedFileAndForget(path);
            if (assets == null || assets.Length == 0)
                throw new InvalidOperationException("ProjectSettings.asset could not be loaded.");
            try
            {
                var serialized = new SerializedObject(assets[0]);
                var property = serialized.FindProperty("activeInputHandler");
                if (property == null)
                    throw new MissingMemberException("ProjectSettings", "activeInputHandler");
                property.intValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                InternalEditorUtility.SaveToSerializedFileAndForget(assets, path, true);
            }
            finally
            {
                Destroy(assets);
            }
        }

        private static void RestoreInputHandler(PersistedOperation state)
        {
            if (state != null && state.inputHandlerChanged)
                WriteActiveInputHandler(state.previousInputHandler);
        }

        private static void Destroy(UnityEngine.Object[] assets)
        {
            foreach (var asset in assets)
                if (asset != null)
                    UnityEngine.Object.DestroyImmediate(asset);
        }
    }

    internal static class InputSystemSetupService
    {
        internal const string PackageName = "com.unity.inputsystem";

        internal static bool IsReady
        {
            get { return PackageService.IsInstalled(PackageName) && PackageService.ActiveInputHandler == 2; }
        }

        internal static string Status
        {
            get
            {
                if (!PackageService.IsInstalled(PackageName))
                    return "Setup required";
                return PackageService.ActiveInputHandler == 2 ? "Ready" : "Enable Both input backends";
            }
        }

        internal static bool PollSetup()
        {
            var response = new BridgeResponse();
            PackageService.AddOrUpdate(response, PackageName, null);
            return !response.pending && IsReady;
        }

        internal static void RequireReady()
        {
            if (!IsReady)
                throw new InvalidOperationException(
                    "Game input is not configured. Open Tools > Unity Agent Bridge and install the required Input System setup.");
        }
    }
}
