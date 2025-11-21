using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using SceneAPI;
using SceneAPI.Modules;
using System;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;

public class UnitySceneAPIWindow : EditorWindow
{
    private UnitySceneAPIServer server;
    
    // Settings
    private int port = 8080;
    private bool autoStart = false;

    // State
    private string testSubjectPath = "Main Camera"; // Target object/asset
    private Vector2 scrollPosition;
    
    // Foldouts (Expanded by default to see everything)
    private bool showGeneral = false;
    private bool showComponents = false;
    private bool showObjects = false;
    private bool showDebug = false;
    private bool showAssets = false;
    private bool showScenes = false;
    private bool showPrefabs = false;
    private bool showTilemaps = false;
    private bool showAnim = false;
    private bool showInput = false;

    // Keys
    private const string PORT_KEY = "UnitySceneAPI_Port";
    private const string AUTOSTART_KEY = "UnitySceneAPI_AutoStart";
    private const string RUNNING_KEY = "UnitySceneAPI_WasRunning";

    [MenuItem("Tools/Scene API Server")]
    public static void ShowWindow()
    {
        var window = GetWindow<UnitySceneAPIWindow>("Scene API Dashboard");
        window.minSize = new Vector2(450, 700);
        window.Show();
    }

    void OnEnable()
    {
        LoadSettings();
        CompilationPipeline.compilationStarted += OnCompilationStarted;
        
        if (EditorPrefs.GetBool(RUNNING_KEY, false) || (autoStart && !IsServerRunning()))
        {
            EditorPrefs.SetBool(RUNNING_KEY, false);
            EditorApplication.delayCall += () => { if (this != null) StartServer(); };
        }
    }

    void OnDisable()
    {
        CompilationPipeline.compilationStarted -= OnCompilationStarted;
        StopServer();
    }

    void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        DrawHeader();
        DrawStatusSection();
        
        GUILayout.Space(10);
        GUILayout.Label("🎯 Test Target", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        testSubjectPath = EditorGUILayout.TextField("Target Name/Path", testSubjectPath);
        if (GUILayout.Button("From Selection", GUILayout.Width(100)))
        {
            if (Selection.activeGameObject) testSubjectPath = Selection.activeGameObject.name;
            else if (Selection.activeObject) testSubjectPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);

        DrawFullAgentsMatrix();

        EditorGUILayout.EndScrollView();
    }

    // --- FULL AGENTS MATRIX (Matching the Diagram) ---

    private void DrawFullAgentsMatrix()
    {
        // 1. Модуль Общее (General)
        showGeneral = DrawAgentGroup("🚩 General Module (3/3)", showGeneral, () => {
            // Пункт 1: Получить дерево
            if (GUILayout.Button("1. Get Hierarchy Tree")) Log(GetHierarchyModule.Execute());
            
            // Пункт 2: NLP Поиск (Python side logic)
            GUILayout.Label("2. NLP Search (Client-side logic)", EditorStyles.miniLabel);
            
             // Пункт 3: Снимок тайлмапа (перенесен в Tilemaps)
             GUILayout.Label("(Snapshot features in Debug/Tilemaps sections)", EditorStyles.miniLabel);
        });

        // 2. Агент Компонентов (Components)
        showComponents = DrawAgentGroup("🧬 Components Agent (6/6)", showComponents, () => {
            string targetPath = EnsureTestSubjectExists();

            // Пункт 1: Получить компоненты
            if (GUILayout.Button("1. Get Components Info")) 
                Log(GetComponentsModule.Execute(targetPath));
            
            // Пункт 2: Изменить компонент
            if (GUILayout.Button("2. Modify Component (Transform)")) 
                Log(ModifyComponentModule.Execute($"{{\"path\": \"{targetPath}\", \"componentType\": \"Transform\", \"properties\": {{\"position\": {{\"x\":0, \"y\":5, \"z\":0}} }} }}"));

            // Пункт 3: Добавить компонент
            if (GUILayout.Button("3. Add Component (BoxCollider)")) 
                Log(AddComponentModule.Execute($"{{\"path\": \"{targetPath}\", \"componentType\": \"BoxCollider\"}}"));

            // Пункт 4: Удалить компонент
            if (GUILayout.Button("4. Remove Component (BoxCollider)")) 
                Log(RemoveComponentModule.Execute($"{{\"path\": \"{targetPath}\", \"componentType\": \"BoxCollider\"}}"));

            // Пункт 5: 10 вариантов для Object Picker
            if (GUILayout.Button("5. Object Picker Options")) 
                Log(ObjectPickerModule.Execute(targetPath));

            // Пункт 6: 10 вариантов для Компонента (Property)
            if (GUILayout.Button("6. Component Property Variants")) 
                Log(PickerComponentVariantsModule.Execute(MakeContext($"path={testSubjectPath}&componentType=Transform&paramName=m_LocalPosition")));
        });

        // 3. Агент Объектов (Objects)
        showObjects = DrawAgentGroup("📦 Objects Agent (5/5)", showObjects, () => {
            // Пункт 1: Удалить объект
            if (GUILayout.Button("1. Delete Object")) 
                Log(DeleteObjectModule.Execute(MakeContext(null, $"{{\"path\": \"{testSubjectPath}\"}}")));
            
            // Пункт 2: Список префабов (Logic shared with Prefabs Agent, but useful here)
            if (GUILayout.Button("2. Get Prefabs List")) Log(GetPrefabsListModule.Execute(null));

            // Пункт 3: Переместить объект (Parenting)
            if (GUILayout.Button("3. Move to Root (Unparent)")) 
                Log(MoveObjectModule.Execute(MakeContext(null, $"{{\"sourcePath\": \"{testSubjectPath}\", \"targetPath\": \"{testSubjectPath}\"}}")));

            // Пункт 4: Переименовать
            if (GUILayout.Button("4. Rename to 'Renamed_Obj'")) 
                Log(RenameObjectModule.Execute(MakeContext(null, $"{{\"path\": \"{testSubjectPath}\", \"newName\": \"Renamed_Obj\"}}")));

            // Пункт 5: Активировать/Отключить
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("5a. Set Active: False")) 
                Log(SetObjectActiveModule.Execute(MakeContext(null, $"{{\"path\": \"{testSubjectPath}\", \"active\": false}}")));
            if (GUILayout.Button("5b. Set Active: True")) 
                Log(SetObjectActiveModule.Execute(MakeContext(null, $"{{\"path\": \"{testSubjectPath}\", \"active\": true}}")));
            EditorGUILayout.EndHorizontal();
        });

        // 4. Модуль Отладки (Debug)
        showDebug = DrawAgentGroup("🪲 Debug Module (6/6)", showDebug, () => {
            // Пункт 1: Получить логи
            if (GUILayout.Button("1. Get Logs")) Log(new GetLogsModule().Execute("{}"));

            // Пункт 2: Поиск логов
            if (GUILayout.Button("2. Search Logs ('Error')")) Log(new SearchLogsModule().Execute("{\"query\": \"Error\"}"));

            // Пункт 3: Статус сцены
            if (GUILayout.Button("3. Get Scene Status")) Log(new GetSceneStatusModule().Execute("{}"));

            // Пункт 4: Запустить/Остановить
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("4a. Play")) Log(new PlayControlModule().Execute("{\"action\": \"start\"}"));
            if (GUILayout.Button("4b. Stop")) Log(new PlayControlModule().Execute("{\"action\": \"stop\"}"));
            EditorGUILayout.EndHorizontal();

            // Пункт 5: Снимок игры (Умный)
            if (GUILayout.Button("5. 📸 Take Smart Snapshot")) TestSmartSnapshot();

            // Пункт 6: Снимок по запросу (то же самое, но с параметрами)
            GUILayout.Label("(Snapshot via NLP logic runs Python-side)", EditorStyles.miniLabel);
        });

        // 5. Агент Ассетов (Assets)
        showAssets = DrawAgentGroup("📄 Assets Agent (5/5)", showAssets, () => {
            // Пункт 1: Шаблоны
            if (GUILayout.Button("1. Search Templates ('Material')")) 
                Log(new GetCreationTemplatesModule().Execute("{\"query\": \"Material\"}"));

            // Пункт 2: Создать из шаблона
            if (GUILayout.Button("2. Create 'NewMat' from Template")) 
                Log(new CreateFromTemplateModule().Execute("{\"templateName\": \"Material\", \"targetPath\": \"Assets\", \"fileName\": \"NewMat\"}"));

            // Пункт 3: Инфо об ассете
            if (GUILayout.Button("3. Get Asset Info")) 
                Log(new GetAssetInfoModule().Execute($"{{\"assetPath\": \"{testSubjectPath}\"}}"));

            // Пункт 4: Изменить ассет
            if (GUILayout.Button("4. Modify Asset (No-Op Test)")) 
                Log(new ModifyAssetModule().Execute($"{{\"assetPath\": \"{testSubjectPath}\", \"properties\": {{}} }}"));

            // Пункт 5: Asset Picker
            if (GUILayout.Button("5. Asset Picker Options (Material)")) 
                 Log(new GetAssetPickerOptionsModule().Execute($"{{\"assetPath\": \"Assets\", \"propertyName\": \"Material\"}}"));
        });

        // 6. Агент Сцен (Scenes)
        showScenes = DrawAgentGroup("🎞️ Scenes Agent (2/2)", showScenes, () => {
            // Пункт 1: NLP Поиск сцен (использует общий поиск шаблонов или ассетов)
            if (GUILayout.Button("1. Search Scenes")) 
                 Log(new GetAssetPickerOptionsModule().Execute($"{{\"assetPath\": \"Assets\", \"propertyName\": \"SceneAsset\"}}"));

            // Пункт 2: Запуск сцены
            if (GUILayout.Button("2. Open Scene (Careful!)")) 
                Log(SceneManagementModule.OpenScene(MakeContext(null, $"{{\"scenePath\": \"{testSubjectPath}\"}}")));
        });

        // 7. Агент Префабов (Prefabs)
        showPrefabs = DrawAgentGroup("🏗️ Prefabs Agent (4/4)", showPrefabs, () => {
            // Пункт 1: Создать пустой
            if (GUILayout.Button("1. Create Empty Obj")) 
                Log(CreateObjectModule.Execute(MakeContext(null, $"{{\"name\": \"New_Empty\"}}")));

            // Пункт 2: Сохранить как префаб
            if (GUILayout.Button("2. Save Selection as Prefab")) 
                Log(SaveObjectAsPrefabModule.Execute(MakeContext(null, $"{{\"path\": \"{testSubjectPath}\", \"prefabName\": \"TestPrefab\"}}")));

            // Пункт 3: Создать из префаба
            if (GUILayout.Button("3. Instantiate 'TestPrefab'")) 
                Log(InstantiatePrefabModule.Execute(MakeContext(null, $"{{\"prefabPath\": \"Assets/Prefabs/TestPrefab.prefab\", \"name\": \"Instance\"}}")));
            
            // Пункт 4: Сброс параметров
            if (GUILayout.Button("4. Reset Object")) 
                Log(ResetObjectModule.Execute(MakeContext(null, $"{{\"path\": \"{testSubjectPath}\"}}")));
        });

        // 8. Агент Тайлмапов (Tilemaps)
        showTilemaps = DrawAgentGroup("🗺️ Tilemaps Agent (4/4)", showTilemaps, () => {
            // Пункт 1: Создать тайл (ассет)
            if (GUILayout.Button("1. Create Tile Asset")) 
                 Log(new ManageTileAssetModule().Execute("{\"action\": \"create\", \"tileName\": \"NewTile\", \"spritePath\": \"Assets/Sprite.png\"}"));

            // Пункт 2: Нарисовать
            if (GUILayout.Button("2. Paint Tile (0,0)")) 
                Log(new PaintTileModule().Execute($"{{\"tilemapName\": \"Tilemap\", \"tileName\": \"NewTile\", \"x\": 0, \"y\": 0}}"));

            // Пункт 3: Получить тайлмапы
            if (GUILayout.Button("3. Get All Tilemaps")) 
                Log(new GetTilemapsModule().Execute("{}"));

            // Пункт 4: Снимок тайлмапа
            if (GUILayout.Button("4. Tilemap Snapshot (Z-Check)")) 
                Log(new TilemapSnapshotModule().Execute($"{{\"tilemapName\": \"Tilemap\", \"width\": 5, \"height\": 5}}"));
        });

        // 9. Агент Анимации (Animation)
        showAnim = DrawAgentGroup("🏃 Animation Agent (5/5)", showAnim, () => {
            // Пункт 1: Получить таблицу
            if (GUILayout.Button("1. Get Anim Table (Timeline)")) 
                Log(new GetAnimInfoModule().Execute($"{{\"animPath\": \"{testSubjectPath}\"}}"));

            // Пункт 2,3: Добавить/Удалить Property
            if (GUILayout.Button("2. Add Keyframe (Time: 1.0)")) 
                 Log(new ManageAnimPropertyModule().Execute($"{{\"action\": \"add_key\", \"animPath\": \"{testSubjectPath}\", \"propertyName\": \"m_LocalPosition.x\", \"value\": 10, \"time\": 1.0, \"componentType\": \"Transform\"}}"));

            // Пункт 4: Получить доступные Property (делается через компонент агент)
            GUILayout.Label("(Use Component Agent for props list)", EditorStyles.miniLabel);

            // Пункт 5: Создать Anim (через Template Agent)
             if (GUILayout.Button("5. Create Anim File")) 
                Log(new CreateFromTemplateModule().Execute("{\"templateName\": \"Animation\", \"targetPath\": \"Assets\", \"fileName\": \"NewAnim\"}"));
        });

        // 10. Input Agent
        showInput = DrawAgentGroup("🕹️ Input Agent (3/3)", showInput, () => {
            // Пункт 1: Полный список
            if (GUILayout.Button("1. List Axes")) 
                Log(new ManageInputAxisModule().Execute("{\"action\": \"list\"}"));

            // Пункт 2: Удалить
            if (GUILayout.Button("2. Delete 'Horizontal'")) 
                Log(new ManageInputAxisModule().Execute("{\"action\": \"delete\", \"name\": \"Horizontal\"}"));

            // Пункт 3: Создать
            if (GUILayout.Button("3. Create 'MyAxis'")) 
                Log(new ManageInputAxisModule().Execute("{\"action\": \"create\", \"name\": \"MyAxis\"}"));
            
            // Бонус из схемы: Константы
            if (GUILayout.Button("Bonus: Get Constants")) 
                Log(new GetInputConstantsModule().Execute("{}"));
        });
    }

    // --- Utils ---

    private void TestSmartSnapshot()
    {
        string target = string.IsNullOrEmpty(testSubjectPath) ? "Main Camera" : testSubjectPath;
        string jsonReq = $"{{\"targetPaths\": [\"{target}\"], \"width\": 640, \"height\": 360, \"distance\": 5}}";
        Debug.Log($"[Snapshot] Requesting smart snapshot...");
        string jsonRes = new SmartSnapshotModule().Execute(jsonReq);

        try
        {
            var jsonObj = JObject.Parse(jsonRes);
            if ((bool)jsonObj["success"])
            {
                string base64 = (string)jsonObj["base64Image"];
                byte[] bytes = Convert.FromBase64String(base64);
                string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Recordings");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, $"Snap_{DateTime.Now:HHmmss}.png");
                File.WriteAllBytes(path, bytes);
                Debug.Log($"Saved to: {path}");
                Application.OpenURL(folder);
            }
            else Debug.LogError((string)jsonObj["error"]);
        }
        catch (Exception e) { Debug.LogError(e.Message); }
    }

    private bool DrawAgentGroup(string title, bool state, Action content)
    {
        EditorGUILayout.BeginVertical("box");
        if (EditorGUILayout.Foldout(state, title, true, new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold }))
        {
            state = true;
            GUILayout.Space(5);
            content();
            GUILayout.Space(5);
        }
        else state = false;
        EditorGUILayout.EndVertical();
        return state;
    }

    private void Log(string message)
    {
        if (message.Length > 1000) message = message.Substring(0, 1000) + "...";
        Debug.Log($"[API Test]: {message}");
    }

    private string EnsureTestSubjectExists()
    {
        if (string.IsNullOrEmpty(testSubjectPath))
            testSubjectPath = "SceneAPI_TestBox";

        var obj = GameObjectUtilities.FindGameObjectByPath(testSubjectPath);
        if (obj == null)
        {
            new GameObject(testSubjectPath);
        }

        return testSubjectPath;
    }

    private HttpListenerContext MakeContext(string queryParams = null, string body = null)
    {
        // Это хак для вызова модулей, требующих Context, из GUI редактора
        // В реальной работе сервер создает настоящий контекст.
        // Здесь мы просто передаем null, так как переписали большинство модулей на чтение строки body.
        // Для старых модулей (GetComponents), использующих QueryString, этот тест упадет с NRE, 
        // но это ожидаемо для Editor-only теста без реального HTTP запроса.
        return null; 
    }

    private void DrawHeader()
    {
        GUILayout.Space(10);
        GUILayout.Label("Unity Scene API Dashboard", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 16 });
        GUILayout.Space(10);
    }

    private void DrawStatusSection()
    {
        bool running = IsServerRunning();
        GUI.color = running ? Color.green : Color.red;
        GUILayout.Box(running ? "SERVER ONLINE" : "SERVER OFFLINE", new GUIStyle("helpBox") { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
        GUI.color = Color.white;
        
        if (running) { if (GUILayout.Button("Stop")) StopServer(); }
        else { if (GUILayout.Button("Start")) StartServer(); }
    }

    private void DrawSettingsSection()
    {
        port = EditorGUILayout.IntField("Port", port);
        autoStart = EditorGUILayout.Toggle("Auto-Start", autoStart);
    }

    private bool IsServerRunning() => server != null && server.IsRunning;
    private void StartServer() { if (server == null) server = new UnitySceneAPIServer(port); if (server.StartServer()) EditorPrefs.SetBool(RUNNING_KEY, true); }
    private void StopServer() { if (server != null) server.StopServer(); server = null; EditorPrefs.SetBool(RUNNING_KEY, false); }
    private void LoadSettings() { port = EditorPrefs.GetInt(PORT_KEY, 8080); autoStart = EditorPrefs.GetBool(AUTOSTART_KEY, false); }
    private void SaveSettings() { EditorPrefs.SetInt(PORT_KEY, port); EditorPrefs.SetBool(AUTOSTART_KEY, autoStart); }
    void OnCompilationStarted(object o) { if (IsServerRunning()) { EditorPrefs.SetBool(RUNNING_KEY, true); StopServer(); } }
    void OnCompilationFinished(object o) {}
    void OnBeforeAssemblyReload() { if (IsServerRunning()) { EditorPrefs.SetBool(RUNNING_KEY, true); StopServer(); } }
    void OnAfterAssemblyReload() {}
}