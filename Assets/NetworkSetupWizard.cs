// File: Assets/Editor/RPG/NetworkSetupWizard.cs
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;

namespace RPG.Editor
{
    /// <summary>
    /// Comprehensive setup wizard for the WebSocket multiplayer system.
    /// Automates: Scene setup, player prefab creation, module migration, testing tools.
    /// </summary>
    public class NetworkSetupWizard : EditorWindow
    {
        private enum WizardPage
        {
            Welcome,
            SceneSetup,
            PlayerPrefab,
            ModuleMigration,
            Testing,
            Complete
        }

        private WizardPage _currentPage = WizardPage.Welcome;
        private Vector2 _scrollPosition;

        // Scene Setup
        private string _sceneName = "MultiplayerWorld";
        private bool _createSpawnPoints = true;
        private int _spawnPointCount = 4;
        private bool _createNetworkManager = true;

        // Player Prefab
        private GameObject _existingPlayerPrefab;
        // private MovementMode _defaultMovementMode = MovementMode.ThirdPerson;
        private bool _enableVoiceChat = false;
        private bool _enableTextChat = true;
        private bool _enableClientPrediction = true;

        // Module Migration
        private List<MonoBehaviour> _modulesToMigrate = new List<MonoBehaviour>();
        private bool _scanForModules = true;

        // Testing
        private bool _createTestScene = true;
        private int _testPlayerCount = 2;

        [MenuItem("RPG Tools/Network Setup Wizard", priority = 0)]
        public static void ShowWizard()
        {
            NetworkSetupWizard window = GetWindow<NetworkSetupWizard>("Network Setup Wizard");
            window.minSize = new Vector2(600, 500);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            switch (_currentPage)
            {
                case WizardPage.Welcome:
                    DrawWelcomePage();
                    break;
                case WizardPage.SceneSetup:
                    DrawSceneSetupPage();
                    break;
                case WizardPage.PlayerPrefab:
                    DrawPlayerPrefabPage();
                    break;
                case WizardPage.ModuleMigration:
                    DrawModuleMigrationPage();
                    break;
                case WizardPage.Testing:
                    DrawTestingPage();
                    break;
                case WizardPage.Complete:
                    DrawCompletePage();
                    break;
            }

            EditorGUILayout.EndScrollView();

            DrawNavigationButtons();
        }

        #region Header

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("WebSocket Multiplayer Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            // Progress bar
            Rect progressRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            float progress = ((int)_currentPage) / 5f;
            EditorGUI.ProgressBar(progressRect, progress, $"Step {(int)_currentPage + 1} of 6");

            EditorGUILayout.Space();
        }

        #endregion

        #region Pages

        private void DrawWelcomePage()
        {
            GUILayout.Label("Welcome to the Network Setup Wizard!", EditorStyles.largeLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This wizard will help you:\n" +
                "• Setup multiplayer scene structure\n" +
                "• Create/configure player prefabs\n" +
                "• Migrate existing modules to WebSocket\n" +
                "• Setup testing environment\n\n" +
                "Click 'Next' to begin!",
                MessageType.Info
            );

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("System Features:", EditorStyles.boldLabel);
            DrawFeatureList();
        }

        private void DrawFeatureList()
        {
            EditorGUILayout.BeginVertical("box");
            DrawFeature("✓", "Native C# WebSocket server (no external dependencies)");
            DrawFeature("✓", "Host/Client architecture (first player is host)");
            DrawFeature("✓", "Client prediction with server reconciliation");
            DrawFeature("✓", "Runtime movement switching (FPS/TPS/TopDown)");
            DrawFeature("✓", "Voice & text chat support");
            DrawFeature("✓", "Modular architecture (Health, Economy, etc.)");
            DrawFeature("✓", "AES-256 encryption support");
            EditorGUILayout.EndVertical();
        }

        private void DrawFeature(string icon, string text)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(icon, GUILayout.Width(20));
            GUILayout.Label(text);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSceneSetupPage()
        {
            GUILayout.Label("Scene Setup", EditorStyles.largeLabel);
            EditorGUILayout.Space();

            _sceneName = EditorGUILayout.TextField("Scene Name", _sceneName);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Components to Create:", EditorStyles.boldLabel);

            _createNetworkManager = EditorGUILayout.Toggle("Network Manager", _createNetworkManager);
            _createSpawnPoints = EditorGUILayout.Toggle("Spawn Points", _createSpawnPoints);

            if (_createSpawnPoints)
            {
                EditorGUI.indentLevel++;
                _spawnPointCount = EditorGUILayout.IntSlider("Count", _spawnPointCount, 2, 16);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Create Scene Now", GUILayout.Height(30)))
            {
                CreateMultiplayerScene();
            }
        }

        private void DrawPlayerPrefabPage()
        {
            GUILayout.Label("Player Prefab Configuration", EditorStyles.largeLabel);
            EditorGUILayout.Space();

            _existingPlayerPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Existing Prefab (Optional)",
                _existingPlayerPrefab,
                typeof(GameObject),
                false
            );

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Movement Settings:", EditorStyles.boldLabel);

            _defaultMovementMode = (MovementMode)EditorGUILayout.EnumPopup("Default Mode", _defaultMovementMode);
            _enableClientPrediction = EditorGUILayout.Toggle("Client Prediction", _enableClientPrediction);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Chat Settings:", EditorStyles.boldLabel);

            _enableVoiceChat = EditorGUILayout.Toggle("Voice Chat", _enableVoiceChat);
            _enableTextChat = EditorGUILayout.Toggle("Text Chat", _enableTextChat);

            EditorGUILayout.Space();

            if (GUILayout.Button("Create Player Prefab", GUILayout.Height(30)))
            {
                CreatePlayerPrefab();
            }

            if (_existingPlayerPrefab != null)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Add Networking to Existing Prefab", GUILayout.Height(30)))
                {
                    AddNetworkingToExistingPrefab();
                }
            }
        }

        private void DrawModuleMigrationPage()
        {
            GUILayout.Label("Module Migration", EditorStyles.largeLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Scan your project for modules using Unity NGO and convert them to WebSocket.",
                MessageType.Info
            );

            if (GUILayout.Button("Scan for NGO Modules", GUILayout.Height(30)))
            {
                ScanForNGOModules();
            }

            if (_modulesToMigrate.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Found {_modulesToMigrate.Count} modules:", EditorStyles.boldLabel);

                foreach (var module in _modulesToMigrate)
                {
                    EditorGUILayout.BeginHorizontal("box");
                    EditorGUILayout.LabelField(module.GetType().Name);
                    if (GUILayout.Button("Migrate", GUILayout.Width(80)))
                    {
                        MigrateModule(module);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private void DrawTestingPage()
        {
            GUILayout.Label("Testing Setup", EditorStyles.largeLabel);
            EditorGUILayout.Space();

            _createTestScene = EditorGUILayout.Toggle("Create Test Scene", _createTestScene);
            _testPlayerCount = EditorGUILayout.IntSlider("Test Player Count", _testPlayerCount, 2, 8);

            EditorGUILayout.Space();

            if (GUILayout.Button("Create Test Environment", GUILayout.Height(30)))
            {
                CreateTestEnvironment();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quick Actions:", EditorStyles.boldLabel);

            if (GUILayout.Button("Build Test Client"))
            {
                BuildTestClient();
            }

            if (GUILayout.Button("Open Network Debugger"))
            {
                OpenNetworkDebugger();
            }
        }

        private void DrawCompletePage()
        {
            GUILayout.Label("Setup Complete!", EditorStyles.largeLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Your WebSocket multiplayer system is ready!\n\n" +
                "Next steps:\n" +
                "1. Test host/client connection in editor\n" +
                "2. Build standalone client\n" +
                "3. Configure modules (Health, Economy, etc.)\n" +
                "4. Add your game logic!",
                MessageType.Info
            );

            EditorGUILayout.Space();

            if (GUILayout.Button("Open Documentation", GUILayout.Height(30)))
            {
                Application.OpenURL("https://github.com/yourusername/unity-websocket-multiplayer/wiki");
            }

            if (GUILayout.Button("Close Wizard", GUILayout.Height(30)))
            {
                Close();
            }
        }

        #endregion

        #region Navigation

        private void DrawNavigationButtons()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = _currentPage != WizardPage.Welcome;
            if (GUILayout.Button("< Back", GUILayout.Height(30), GUILayout.Width(100)))
            {
                _currentPage--;
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            if (_currentPage != WizardPage.Complete)
            {
                if (GUILayout.Button("Next >", GUILayout.Height(30), GUILayout.Width(100)))
                {
                    _currentPage++;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Scene Creation

        private void CreateMultiplayerScene()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Create Network Manager
            if (_createNetworkManager)
            {
                GameObject networkObj = new GameObject("NetworkManager");
                networkObj.AddComponent<HybridNetworkManager>();
                networkObj.AddComponent<WebSocketPlayerSpawner>();
            }

            // Create spawn points
            if (_createSpawnPoints)
            {
                GameObject spawnParent = new GameObject("SpawnPoints");
                
                for (int i = 0; i < _spawnPointCount; i++)
                {
                    GameObject spawnPoint = new GameObject($"SpawnPoint_{i + 1}");
                    spawnPoint.transform.SetParent(spawnParent.transform);
                    
                    // Arrange in circle
                    float angle = (360f / _spawnPointCount) * i * Mathf.Deg2Rad;
                    float radius = 10f;
                    spawnPoint.transform.position = new Vector3(
                        Mathf.Cos(angle) * radius,
                        0,
                        Mathf.Sin(angle) * radius
                    );

                    // Visual indicator
                    GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    indicator.transform.SetParent(spawnPoint.transform);
                    indicator.transform.localPosition = Vector3.zero;
                    indicator.transform.localScale = new Vector3(1, 0.1f, 1);
                    DestroyImmediate(indicator.GetComponent<Collider>());

                    var renderer = indicator.GetComponent<Renderer>();
                    renderer.material.color = new Color(0, 1, 0, 0.5f);
                }
            }

            // Create ground
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10, 1, 10);

            // Save scene
            string scenePath = $"Assets/Scenes/{_sceneName}.unity";
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log($"[Wizard] Scene created: {scenePath}");
            EditorUtility.DisplayDialog("Success", $"Scene '{_sceneName}' created successfully!", "OK");
        }

        #endregion

        #region Player Prefab Creation

        private void CreatePlayerPrefab()
        {
            GameObject player = new GameObject("NetworkPlayer");

            // Add CharacterController
            var controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0, 1, 0);

            // Add movement selector
            var movementSelector = player.AddComponent<RuntimeMovementSelector>();

            // Create controller prefabs
            CreateControllerPrefabs(player);

            // Add modules
            player.AddComponent<HealthModule>();
            player.AddComponent<EconomyModule>();

            // Create visual mesh
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(player.transform);
            visual.transform.localPosition = new Vector3(0, 1, 0);
            DestroyImmediate(visual.GetComponent<Collider>());

            // Create ground check
            GameObject groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(player.transform);
            groundCheck.transform.localPosition = new Vector3(0, 0.1f, 0);

            // Save as prefab
            string prefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
            Directory.CreateDirectory("Assets/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(player, prefabPath);
            DestroyImmediate(player);

            Debug.Log($"[Wizard] Player prefab created: {prefabPath}");
            EditorUtility.DisplayDialog("Success", "Player prefab created successfully!", "OK");
        }

        private void CreateControllerPrefabs(GameObject player)
        {
            // TODO: Create individual controller prefabs
            // This would create separate prefabs for FirstPersonController, etc.
        }

        private void AddNetworkingToExistingPrefab()
        {
            if (_existingPlayerPrefab == null) return;

            // Load prefab
            GameObject prefab = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(_existingPlayerPrefab));

            // Add required components
            if (prefab.GetComponent<RuntimeMovementSelector>() == null)
            {
                prefab.AddComponent<RuntimeMovementSelector>();
            }

            if (prefab.GetComponent<CharacterController>() == null)
            {
                prefab.AddComponent<CharacterController>();
            }

            // Save changes
            PrefabUtility.SaveAsPrefabAsset(prefab, AssetDatabase.GetAssetPath(_existingPlayerPrefab));
            PrefabUtility.UnloadPrefabContents(prefab);

            Debug.Log("[Wizard] Networking added to existing prefab!");
            EditorUtility.DisplayDialog("Success", "Networking components added!", "OK");
        }

        #endregion

        #region Module Migration

        private void ScanForNGOModules()
        {
            _modulesToMigrate.Clear();

            // Search for all MonoBehaviours in project
            string[] guids = AssetDatabase.FindAssets("t:MonoBehaviour");
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (script != null)
                {
                    // Check if script contains NGO references
                    string scriptText = script.text;
                    if (scriptText.Contains("Unity.Netcode") || 
                        scriptText.Contains("NetworkBehaviour") ||
                        scriptText.Contains("NetworkVariable"))
                    {
                        Debug.Log($"[Wizard] Found NGO module: {script.name}");
                        // Add to list (would need actual instance)
                    }
                }
            }

            Debug.Log($"[Wizard] Scan complete. Found {_modulesToMigrate.Count} modules.");
        }

        private void MigrateModule(MonoBehaviour module)
        {
            // TODO: Implement automated migration
            // 1. Create backup
            // 2. Replace NetworkVariable with regular fields
            // 3. Remove ServerRpc/ClientRpc
            // 4. Add SendNetworkMessage calls
            // 5. Update lifecycle methods

            Debug.Log($"[Wizard] Migrating {module.GetType().Name}...");
            EditorUtility.DisplayDialog("Migration", "Migration tool coming soon!", "OK");
        }

        #endregion

        #region Testing

        private void CreateTestEnvironment()
        {
            Debug.Log("[Wizard] Creating test environment...");
            // TODO: Setup automated testing scene
        }

        private void BuildTestClient()
        {
            Debug.Log("[Wizard] Building test client...");
            // TODO: Quick build for testing
        }

        private void OpenNetworkDebugger()
        {
            NetworkDebuggerWindow.ShowWindow();
        }

        #endregion
    }

    /// <summary>
    /// Network debugger for testing connections
    /// </summary>
    public class NetworkDebuggerWindow : EditorWindow
    {
        private Vector2 _scrollPosition;

        [MenuItem("RPG Tools/Network Debugger", priority = 1)]
        public static void ShowWindow()
        {
            GetWindow<NetworkDebuggerWindow>("Network Debugger");
        }

        private void OnGUI()
        {
            GUILayout.Label("Network Debugger", EditorStyles.largeLabel);
            EditorGUILayout.Space();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use debugger", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Display network state
            if (HybridNetworkManager.Instance != null)
            {
                DrawNetworkState();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawNetworkState()
        {
            var manager = HybridNetworkManager.Instance;

            EditorGUILayout.LabelField("Connection Status:", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Mode:", manager.CurrentMode.ToString());
            EditorGUILayout.LabelField("Player ID:", manager.LocalPlayerId ?? "Not Connected");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            if (manager.IsHost)
            {
                var players = manager.GetAllPlayers();
                EditorGUILayout.LabelField($"Connected Players: {players.Count}", EditorStyles.boldLabel);
                
                foreach (var player in players)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField("ID:", player.playerId);
                    EditorGUILayout.LabelField("Position:", player.position.ToString());
                    EditorGUILayout.EndVertical();
                }
            }
        }
    }
}