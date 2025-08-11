using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using SceneAPI;

public class UnitySceneAPIWindow : EditorWindow
{
    private UnitySceneAPIServer server;
    private int port = 8080;

    private const string PORT_KEY = "UnitySceneAPI_Port";
    private const string RUNNING_KEY = "UnitySceneAPI_WasRunning";

    [MenuItem("Tools/Scene API Server")]
    public static void ShowWindow()
    {
        GetWindow<UnitySceneAPIWindow>("Scene API Server");
    }

    void OnEnable()
    {
        port = EditorPrefs.GetInt(PORT_KEY, 8080);
        server = new UnitySceneAPIServer(port);

        CompilationPipeline.compilationStarted += OnCompilationStarted;
        CompilationPipeline.compilationFinished += OnCompilationFinished;
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

        if (EditorPrefs.GetBool(RUNNING_KEY, false))
        {
            EditorPrefs.SetBool(RUNNING_KEY, false);
            EditorApplication.delayCall += () => {
                if (this != null)
                {
                    StartServer();
                }
            };
        }
    }

    void OnDisable()
    {
        CompilationPipeline.compilationStarted -= OnCompilationStarted;
        CompilationPipeline.compilationFinished -= OnCompilationFinished;
        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;

        if (server != null && server.IsRunning)
        {
            StopServer();
        }
    }

    void OnCompilationStarted(object obj)
    {
        if (server != null && server.IsRunning)
        {
            EditorPrefs.SetBool(RUNNING_KEY, true);
            StopServer();
        }
    }

    void OnCompilationFinished(object obj)
    {
    }

    void OnBeforeAssemblyReload()
    {
        if (server != null && server.IsRunning)
        {
            EditorPrefs.SetBool(RUNNING_KEY, true);
            StopServer();
        }
    }

    void OnAfterAssemblyReload()
    {
    }

    void OnGUI()
    {
        GUILayout.Label("Unity Scene API Server", EditorStyles.boldLabel);

        int newPort = EditorGUILayout.IntField("Port:", port);
        if (newPort != port)
        {
            port = newPort;
            EditorPrefs.SetInt(PORT_KEY, port);
            if (server != null)
            {
                server = new UnitySceneAPIServer(port);
            }
        }

        bool isRunning = server != null && server.IsRunning;
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
        if (server == null)
        {
            server = new UnitySceneAPIServer(port);
        }
        
        server.StartServer();
    }

    void StopServer()
    {
        if (server != null)
        {
            server.StopServer();
        }
    }

    void OnDestroy()
    {
        if (server != null && server.IsRunning)
        {
            StopServer();
        }
    }
}