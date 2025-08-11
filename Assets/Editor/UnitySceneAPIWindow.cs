using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Net;
using System.Threading;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;

public class UnitySceneAPIWindow : EditorWindow
{
    private HttpListener httpListener;
    private Thread httpListenerThread;
    private bool isRunning = false;
    private int port = 8080;

    [MenuItem("Tools/Scene API Server")]
    public static void ShowWindow()
    {
        GetWindow<UnitySceneAPIWindow>("Scene API Server");
    }

    void OnGUI()
    {
        GUILayout.Label("Unity Scene API Server", EditorStyles.boldLabel);

        port = EditorGUILayout.IntField("Port:", port);

        if (!isRunning)
        {
            if (GUILayout.Button("Start Server"))
            {
                StartServer();
            }
        }
        else
        {
            if (GUILayout.Button("Stop Server"))
            {
                StopServer();
            }

            GUILayout.Label($"Server running on: http://localhost:{port}");
            GUILayout.Label("Available endpoints:");
            GUILayout.Label("  GET /scene - Get scene hierarchy");
            GUILayout.Label("  POST /scene/open - Open scene");
            GUILayout.Label("  GET /build/scenes - Get build scenes");
            GUILayout.Label("  POST /build/scenes/add - Add scene to build");
            GUILayout.Label("  DELETE /build/scenes/remove - Remove scene from build");
            GUILayout.Label("  POST /objects/create - Create empty object");
            GUILayout.Label("  DELETE /objects/delete - Delete object");
            GUILayout.Label("  GET /objects/components - Get object components");
            GUILayout.Label("  POST /objects/components/add - Add component");
            GUILayout.Label("  PUT /objects/components/modify - Modify component");
            GUILayout.Label("  DELETE /objects/components/remove - Remove component");
        }
    }

    void StartServer()
    {
        MainThreadDispatcher.Initialize();

        httpListener = new HttpListener();
        httpListener.Prefixes.Add($"http://localhost:{port}/");
        httpListener.Start();

        httpListenerThread = new Thread(new ThreadStart(Listen));
        httpListenerThread.Start();

        isRunning = true;
        Debug.Log($"Scene API Server started on port {port}");
    }

    void StopServer()
    {
        if (httpListener != null)
        {
            httpListener.Stop();
            httpListener.Close();
        }

        if (httpListenerThread != null)
        {
            httpListenerThread.Abort();
        }

        isRunning = false;
        Debug.Log("Scene API Server stopped");

        MainThreadDispatcher.Cleanup();
    }

    void OnDestroy()
    {
        StopServer();
    }

    private void Listen()
    {
        while (httpListener.IsListening)
        {
            try
            {
                HttpListenerContext context = httpListener.GetContext();
                MainThreadDispatcher.Enqueue(() => Process(context));
            }
            catch (System.Exception ex)
            {
                if (!(ex is HttpListenerException))
                {
                    Debug.LogError($"HTTP Listener error: {ex.Message}");
                }
            }
        }
    }

    private void Process(HttpListenerContext context)
    {
        string response = "";
        string path = context.Request.Url.AbsolutePath;
        string method = context.Request.HttpMethod;

        try
        {
            switch ($"{method} {path}")
            {
                case "GET /scene":
                    response = GetSceneHierarchy();
                    break;
                case "POST /scene/open":
                    response = OpenScene(context);
                    break;
                case "GET /build/scenes":
                    response = GetBuildScenes();
                    break;
                case "POST /build/scenes/add":
                    response = AddSceneToBuild(context);
                    break;
                case "DELETE /build/scenes/remove":
                    response = RemoveSceneFromBuild(context);
                    break;
                case "POST /objects/create":
                    response = CreateObject(context);
                    break;
                case "DELETE /objects/delete":
                    response = DeleteObject(context);
                    break;
                case "GET /objects/components":
                    response = GetObjectComponents(context);
                    break;
                case "POST /objects/components/add":
                    response = AddComponent(context);
                    break;
                case "PUT /objects/components/modify":
                    response = ModifyComponent(context);
                    break;
                case "DELETE /objects/components/remove":
                    response = RemoveComponent(context);
                    break;
                default:
                    response = JsonConvert.SerializeObject(new { error = "Endpoint not found" });
                    break;
            }
        }
        catch (System.Exception ex)
        {
            response = JsonConvert.SerializeObject(new { error = ex.Message });
        }

        byte[] buffer = Encoding.UTF8.GetBytes(response);
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = buffer.Length;
        context.Response.AddHeader("Access-Control-Allow-Origin", "*");
        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.OutputStream.Close();
    }

    private string GetRequestBody(HttpListenerContext context)
    {
        using (StreamReader reader = new StreamReader(context.Request.InputStream))
        {
            return reader.ReadToEnd();
        }
    }

    private string GetSceneHierarchy()
    {
        try
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return JsonConvert.SerializeObject(new { error = "No active scene found" });
            }

            var sceneData = new
            {
                sceneName = activeScene.name,
                scenePath = activeScene.path,
                rootObjects = GetRootObjectsData()
            };

            return JsonConvert.SerializeObject(sceneData, Formatting.Indented);
        }
        catch (System.Exception ex)
        {
            return JsonConvert.SerializeObject(new { error = $"Error getting scene hierarchy: {ex.Message}" });
        }
    }

    private List<object> GetRootObjectsData()
    {
        var rootObjects = new List<object>();

        try
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return rootObjects;
            }

            var sceneRootObjects = activeScene.GetRootGameObjects();
            if (sceneRootObjects == null)
            {
                return rootObjects;
            }

            foreach (GameObject rootGO in sceneRootObjects)
            {
                if (rootGO != null)
                {
                    rootObjects.Add(GetGameObjectData(rootGO, ""));
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error getting root objects: {ex.Message}");
        }

        return rootObjects;
    }

    private object GetGameObjectData(GameObject go, string parentPath)
    {
        string currentPath = string.IsNullOrEmpty(parentPath) ? go.name : $"{parentPath}/{go.name}";
        var children = new List<object>();

        for (int i = 0; i < go.transform.childCount; i++)
        {
            children.Add(GetGameObjectData(go.transform.GetChild(i).gameObject, currentPath));
        }

        return new
        {
            name = go.name,
            path = currentPath,
            instanceId = go.GetInstanceID(),
            active = go.activeInHierarchy,
            tag = go.tag,
            layer = go.layer,
            position = new { x = go.transform.position.x, y = go.transform.position.y, z = go.transform.position.z },
            rotation = new { x = go.transform.rotation.x, y = go.transform.rotation.y, z = go.transform.rotation.z, w = go.transform.rotation.w },
            scale = new { x = go.transform.localScale.x, y = go.transform.localScale.y, z = go.transform.localScale.z },
            components = go.GetComponents<Component>().Select(c => c.GetType().Name).ToArray(),
            children = children
        };
    }

    private string GetBuildScenes()
    {
        var buildScenes = EditorBuildSettings.scenes.Select((scene, index) => new
        {
            index = index,
            path = scene.path,
            enabled = scene.enabled,
            guid = scene.guid.ToString()
        }).ToArray();

        return JsonConvert.SerializeObject(new { scenes = buildScenes }, Formatting.Indented);
    }

    private string AddSceneToBuild(HttpListenerContext context)
    {
        var data = JsonConvert.DeserializeObject<dynamic>(GetRequestBody(context));
        string scenePath = data.scenePath;

        if (!File.Exists(scenePath))
        {
            return JsonConvert.SerializeObject(new { success = false, message = "Scene file not found" });
        }

        var scenes = EditorBuildSettings.scenes.ToList();

        if (scenes.Any(s => s.path == scenePath))
        {
            return JsonConvert.SerializeObject(new { success = false, message = "Scene already in build settings" });
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        return JsonConvert.SerializeObject(new { success = true, message = $"Scene added to build: {scenePath}" });
    }

    private string RemoveSceneFromBuild(HttpListenerContext context)
    {
        var data = JsonConvert.DeserializeObject<dynamic>(GetRequestBody(context));
        string scenePath = data.scenePath;

        var scenes = EditorBuildSettings.scenes.ToList();
        var sceneToRemove = scenes.FirstOrDefault(s => s.path == scenePath);

        if (sceneToRemove.path == null)
        {
            return JsonConvert.SerializeObject(new { success = false, message = "Scene not found in build settings" });
        }

        scenes.Remove(sceneToRemove);
        EditorBuildSettings.scenes = scenes.ToArray();

        return JsonConvert.SerializeObject(new { success = true, message = $"Scene removed from build: {scenePath}" });
    }

    private string OpenScene(HttpListenerContext context)
    {
        var data = JsonConvert.DeserializeObject<dynamic>(GetRequestBody(context));
        string scenePath = data.scenePath;

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(scenePath);
            return JsonConvert.SerializeObject(new { success = true, message = $"Scene opened: {scenePath}" });
        }

        return JsonConvert.SerializeObject(new { success = false, message = "Failed to open scene" });
    }

    private string CreateObject(HttpListenerContext context)
    {
        var data = JsonConvert.DeserializeObject<dynamic>(GetRequestBody(context));
        string objectName = data.name ?? "GameObject";
        string parentPath = data.parentPath ?? "";

        GameObject newObj = new GameObject(objectName);

        if (!string.IsNullOrEmpty(parentPath))
        {
            GameObject parent = FindGameObjectByPath(parentPath);
            if (parent != null)
            {
                newObj.transform.SetParent(parent.transform);
            }
        }

        string fullPath = string.IsNullOrEmpty(parentPath) ? objectName : $"{parentPath}/{objectName}";

        return JsonConvert.SerializeObject(new
        {
            success = true,
            path = fullPath,
            instanceId = newObj.GetInstanceID(),
            message = $"Object created: {objectName}"
        });
    }

    private string DeleteObject(HttpListenerContext context)
    {
        var data = JsonConvert.DeserializeObject<dynamic>(GetRequestBody(context));
        string objectPath = data.path;

        GameObject obj = FindGameObjectByPath(objectPath);
        if (obj != null)
        {
            DestroyImmediate(obj);
            return JsonConvert.SerializeObject(new { success = true, message = $"Object deleted: {objectPath}" });
        }

        return JsonConvert.SerializeObject(new { success = false, message = "Object not found" });
    }

    private string GetObjectComponents(HttpListenerContext context)
    {
        string objectPath = context.Request.QueryString["path"];

        GameObject obj = FindGameObjectByPath(objectPath);
        if (obj == null)
        {
            return JsonConvert.SerializeObject(new { error = "Object not found" });
        }

        var components = obj.GetComponents<Component>().Select(comp => new
        {
            name = comp.GetType().Name,
            type = comp.GetType().FullName,
            properties = GetComponentProperties(comp)
        }).ToArray();

        return JsonConvert.SerializeObject(new { path = objectPath, components = components }, Formatting.Indented);
    }

    private object GetComponentProperties(Component component)
    {
        var properties = new Dictionary<string, object>();

        foreach (FieldInfo field in component.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            try
            {
                var value = field.GetValue(component);
                properties[field.Name] = SerializeValue(value);
            }
            catch { }
        }

        foreach (PropertyInfo prop in component.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.CanRead && prop.GetIndexParameters().Length == 0)
            {
                try
                {
                    var value = prop.GetValue(component);
                    properties[prop.Name] = SerializeValue(value);
                }
                catch { }
            }
        }

        return properties;
    }

    private object SerializeValue(object value)
    {
        if (value == null) return null;

        Type type = value.GetType();

        if (type.IsPrimitive || type == typeof(string))
            return value;

        if (value is Vector3 vec3)
            return new { x = vec3.x, y = vec3.y, z = vec3.z };

        if (value is Vector2 vec2)
            return new { x = vec2.x, y = vec2.y };

        if (value is Quaternion quat)
            return new { x = quat.x, y = quat.y, z = quat.z, w = quat.w };

        if (value is Color color)
            return new { r = color.r, g = color.g, b = color.b, a = color.a };

        return value.ToString();
    }

    private string AddComponent(HttpListenerContext context)
    {
        var data = JsonConvert.DeserializeObject<dynamic>(GetRequestBody(context));
        string objectPath = data.path;
        string componentType = data.componentType;

        GameObject obj = FindGameObjectByPath(objectPath);
        if (obj == null)
        {
            return JsonConvert.SerializeObject(new { success = false, message = "Object not found" });
        }

        Type type = Type.GetType($"UnityEngine.{componentType}, UnityEngine") ??
                   Type.GetType($"{componentType}, Assembly-CSharp");

        if (type == null)
        {
            return JsonConvert.SerializeObject(new { success = false, message = "Component type not found" });
        }

        if (obj.GetComponent(type) != null)
        {
            return JsonConvert.SerializeObject(new { success = false, message = $"Component {componentType} already exists on object" });
        }

        try
        {
            Component comp = obj.AddComponent(type);
            return JsonConvert.SerializeObject(new { success = true, message = $"Component added: {componentType}" });
        }
        catch (System.Exception ex)
        {
            return JsonConvert.SerializeObject(new { success = false, message = $"Failed to add component: {ex.Message}" });
        }
    }

    private string ModifyComponent(HttpListenerContext context)
    {
        var data = JsonConvert.DeserializeObject<dynamic>(GetRequestBody(context));
        string objectPath = data.path;
        string componentType = data.componentType;
        var properties = data.properties;

        GameObject obj = FindGameObjectByPath(objectPath);
        if (obj == null)
        {
            return JsonConvert.SerializeObject(new { success = false, message = "Object not found" });
        }

        Component comp = obj.GetComponent(componentType);
        if (comp == null)
        {
            return JsonConvert.SerializeObject(new { success = false, message = "Component not found" });
        }

        foreach (var prop in properties)
        {
            SetComponentProperty(comp, prop.Name, prop.Value);
        }

        return JsonConvert.SerializeObject(new { success = true, message = "Component modified" });
    }

    private void SetComponentProperty(Component component, string propertyName, object value)
    {
        Type componentType = component.GetType();

        FieldInfo field = componentType.GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (field != null)
        {
            object convertedValue = ConvertValue(value, field.FieldType);
            field.SetValue(component, convertedValue);
            return;
        }

        PropertyInfo prop = componentType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            object convertedValue = ConvertValue(value, prop.PropertyType);
            prop.SetValue(component, convertedValue);
        }
    }

    private object ConvertValue(object value, Type targetType)
    {
        if (value == null) return null;

        if (targetType == typeof(Vector3))
        {
            var vec = JsonConvert.DeserializeObject<dynamic>(value.ToString());
            return new Vector3((float)vec.x, (float)vec.y, (float)vec.z);
        }

        if (targetType == typeof(Vector2))
        {
            var vec = JsonConvert.DeserializeObject<dynamic>(value.ToString());
            return new Vector2((float)vec.x, (float)vec.y);
        }

        if (targetType == typeof(Quaternion))
        {
            var quat = JsonConvert.DeserializeObject<dynamic>(value.ToString());
            return new Quaternion((float)quat.x, (float)quat.y, (float)quat.z, (float)quat.w);
        }

        return Convert.ChangeType(value, targetType);
    }

    private string RemoveComponent(HttpListenerContext context)
    {
        var data = JsonConvert.DeserializeObject<dynamic>(GetRequestBody(context));
        string objectPath = data.path;
        string componentType = data.componentType;

        GameObject obj = FindGameObjectByPath(objectPath);
        if (obj == null)
        {
            return JsonConvert.SerializeObject(new { success = false, message = "Object not found" });
        }

        Component comp = obj.GetComponent(componentType);
        if (comp == null)
        {
            return JsonConvert.SerializeObject(new { success = false, message = "Component not found" });
        }

        DestroyImmediate(comp);
        return JsonConvert.SerializeObject(new { success = true, message = $"Component removed: {componentType}" });
    }

    private GameObject FindGameObjectByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        string[] pathParts = path.Split('/');
        GameObject current = null;

        foreach (GameObject rootGO in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (rootGO.name == pathParts[0])
            {
                current = rootGO;
                break;
            }
        }

        if (current == null) return null;

        for (int i = 1; i < pathParts.Length; i++)
        {
            Transform child = current.transform.Find(pathParts[i]);
            if (child == null) return null;
            current = child.gameObject;
        }

        return current;
    }
}

public class MainThreadDispatcher
{
    private static readonly Queue<System.Action> actions = new Queue<System.Action>();
    private static bool isInitialized = false;

    public static void Initialize()
    {
        if (!isInitialized)
        {
            EditorApplication.update += Update;
            isInitialized = true;
        }
    }

    public static void Cleanup()
    {
        if (isInitialized)
        {
            EditorApplication.update -= Update;
            isInitialized = false;
            lock (actions)
            {
                actions.Clear();
            }
        }
    }

    public static void Enqueue(System.Action action)
    {
        lock (actions)
        {
            actions.Enqueue(action);
        }
    }

    private static void Update()
    {
        lock (actions)
        {
            while (actions.Count > 0)
            {
                try
                {
                    actions.Dequeue().Invoke();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Error executing main thread action: {ex.Message}");
                }
            }
        }
    }
}