
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace QuantumMechanic.Editor
{
    /// <summary>
    /// The Apple-Style "Automator" - Self-Bootstrapping Project Initializer.
    /// Single-click setup for the entire Mini-MORPG project. from scratch. this bootstrapper was the original and all implementations should be reworked in the Apple-Style "Automator" style! <-CRITICAL 
    /// Generates folder structure, scenes, prefabs, materials, and hooks all systems together.
    /// This is the "magic button" that transforms an empty Unity project into a playable game.
    /// </summary>
    public static class ProjectBootstrapper
    {
        private const string MENU_PATH = "Project/Initialize Masterpiece";
        private const string ROOT_FOLDER = "Assets/_QuantumMechanic";
        
        /// <summary>
        /// The main initialization entry point.
        /// Creates the entire project structure with one click.
        /// </summary>
        [MenuItem(MENU_PATH)]
        public static void InitializeMasterpiece()
        {
            Debug.Log("=== QUANTUM MECHANIC PROJECT BOOTSTRAPPER ===");
            Debug.Log("Initializing Mini-MORPG from scratch...");
            
            // Step 1: Create folder structure
            CreateFolderStructure();
            
            // Step 2: Generate materials
            CreateMaterials();
            
            // Step 3: Create prefabs
            CreatePrefabs();
            
            // Step 4: Build main scene
            CreateMainScene();
            
            // Step 5: Setup network manager
            SetupNetworkManager();
            
            // Refresh and finalize
            AssetDatabase.Refresh();
            Debug.Log("=== INITIALIZATION COMPLETE ===");
            Debug.Log("Your Mini-MORPG is ready! Press Play to start the server.");
            
            EditorUtility.DisplayDialog(
                "Quantum Mechanic Initialized", 
                "Project setup complete!\n\n" +
                "- Main scene created\n" +
                "- Network system configured\n" +
                "- Player prefab generated\n" +
                "- Materials created\n\n" +
                "Press Play to launch the server and client.",
                "Awesome!"
            );
        }
        
        /// <summary>
        /// Creates the project folder hierarchy.
        /// </summary>
        private static void CreateFolderStructure()
        {
            Debug.Log("[Bootstrapper] Creating folder structure...");
            
            string[] folders = new string[]
            {
                ROOT_FOLDER,
                $"{ROOT_FOLDER}/Scripts",
                $"{ROOT_FOLDER}/Scripts/Core",
                $"{ROOT_FOLDER}/Scripts/Networking",
                $"{ROOT_FOLDER}/Scripts/Economy",
                $"{ROOT_FOLDER}/Scripts/Persistence",
                $"{ROOT_FOLDER}/Scripts/Player",
                $"{ROOT_FOLDER}/Prefabs",
                $"{ROOT_FOLDER}/Materials",
                $"{ROOT_FOLDER}/Scenes"
            };
            
            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
                    string folderName = Path.GetFileName(folder);
                    AssetDatabase.CreateFolder(parent, folderName);
                    Debug.Log($"  Created: {folder}");
                }
            }
        }
        
        /// <summary>
        /// Generates URP materials programmatically.
        /// </summary>
        private static void CreateMaterials()
        {
            Debug.Log("[Bootstrapper] Creating URP materials...");
            
            // Player material - vibrant cyan
            CreateURPMaterial("PlayerMaterial", new Color(0.2f, 0.8f, 1f), 0.3f);
            
            // Ground material - earth tone
            CreateURPMaterial("GroundMaterial", new Color(0.3f, 0.5f, 0.3f), 0.8f);
            
            // Enemy material - aggressive red
            CreateURPMaterial("EnemyMaterial", new Color(1f, 0.2f, 0.2f), 0.4f);
        }
        
        /// <summary>
        /// Creates a single URP material with specified color and smoothness.
        /// </summary>
        private static Material CreateURPMaterial(string name, Color color, float smoothness)
        {
            string path = $"{ROOT_FOLDER}/Materials/{name}.mat";
            
            // Check if already exists
            Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existingMat != null)
            {
                Debug.Log($"  Material already exists: {name}");
                return existingMat;
            }
            
            // Create new URP Lit material
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null)
            {
                Debug.LogError("URP Lit shader not found! Is URP installed?");
                urpShader = Shader.Find("Standard"); // Fallback
            }
            
            Material mat = new Material(urpShader);
            mat.name = name;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0.0f);
            
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log($"  Created material: {name}");
            
            return mat;
        }
        
        /// <summary>
        /// Creates the player and ground prefabs.
        /// </summary>
        private static void CreatePrefabs()
        {
            Debug.Log("[Bootstrapper] Creating prefabs...");
            
            // Create Player prefab
            CreatePlayerPrefab();
            
            // Create Ground prefab
            CreateGroundPrefab();
        }
        
        /// <summary>
        /// Creates a capsule-based player prefab with NetworkIdentity and movement controller.
        /// </summary>
        private static void CreatePlayerPrefab()
        {
            GameObject player = new GameObject("Player");
            
            // Add capsule mesh
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(player.transform);
            capsule.transform.localPosition = Vector3.zero;
            
            // Apply material
            Material playerMat = AssetDatabase.LoadAssetAtPath<Material>($"{ROOT_FOLDER}/Materials/PlayerMaterial.mat");
            if (playerMat != null)
            {
                capsule.GetComponent<MeshRenderer>().material = playerMat;
            }
            
            // Add NetworkIdentity
            player.AddComponent<QuantumMechanic.Networking.NetworkIdentity>();
            
            // Add CharacterController for movement
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.up;
            
            // Add simple player controller script
            player.AddComponent<PlayerController>();
            
            // Save as prefab
            string prefabPath = $"{ROOT_FOLDER}/Prefabs/Player.prefab";
            PrefabUtility.SaveAsPrefabAsset(player, prefabPath);
            DestroyImmediate(player);
            
            Debug.Log("  Created Player prefab");
        }
        
        /// <summary>
        /// Creates a ground plane prefab.
        /// </summary>
        private static void CreateGroundPrefab()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10, 1, 10);
            
            // Apply material
            Material groundMat = AssetDatabase.LoadAssetAtPath<Material>($"{ROOT_FOLDER}/Materials/GroundMaterial.mat");
            if (groundMat != null)
            {
                ground.GetComponent<MeshRenderer>().material = groundMat;
            }
            
            // Save as prefab
            string prefabPath = $"{ROOT_FOLDER}/Prefabs/Ground.prefab";
            PrefabUtility.SaveAsPrefabAsset(ground, prefabPath);
            DestroyImmediate(ground);
            
            Debug.Log("  Created Ground prefab");
        }
        
        /// <summary>
        /// Creates the main game scene with all necessary components.
        /// </summary>
        private static void CreateMainScene()
        {
            Debug.Log("[Bootstrapper] Creating Main scene...");
            
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            
            // Configure main camera
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.transform.position = new Vector3(0, 10, -15);
                mainCam.transform.rotation = Quaternion.Euler(30, 0, 0);
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
            }
            
            // Add directional light if not present
            Light dirLight = Object.FindObjectOfType<Light>();
            if (dirLight != null)
            {
                dirLight.transform.rotation = Quaternion.Euler(50, -30, 0);
                dirLight.intensity = 1.5f;
            }
            
            // Instantiate ground
            GameObject groundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ROOT_FOLDER}/Prefabs/Ground.prefab");
            if (groundPrefab != null)
            {
                PrefabUtility.InstantiatePrefab(groundPrefab);
            }
            
            // Create NetworkManager GameObject
            GameObject networkManager = new GameObject("NetworkManager");
            networkManager.AddComponent<QuantumMechanic.Networking.ServerHost>();
            networkManager.AddComponent<QuantumMechanic.Networking.ClientManager>();
            networkManager.AddComponent<GameNetworkManager>();
            
            // Create SystemManager GameObject
            GameObject systemManager = new GameObject("SystemManager");
            systemManager.AddComponent<QuantumMechanic.Persistence.SaveSystem>();
            systemManager.AddComponent<QuantumMechanic.Economy.EconomyManager>();
            
            // Save scene
            string scenePath = $"{ROOT_FOLDER}/Scenes/Main.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            
            Debug.Log($"  Main scene created: {scenePath}");
        }
        
        /// <summary>
        /// Creates the GameNetworkManager integration script.
        /// </summary>
        private static void SetupNetworkManager()
        {
            Debug.Log("[Bootstrapper] Setting up network integration...");
            
            string scriptPath = $"{ROOT_FOLDER}/Scripts/Core/GameNetworkManager.cs";
            
            if (File.Exists(scriptPath))
            {
                Debug.Log("  GameNetworkManager already exists");
                return;
            }
            
            string scriptContent = @"using UnityEngine;
using QuantumMechanic.Networking;
using QuantumMechanic.Persistence;
using QuantumMechanic.Economy;
using System.Collections.Generic;

/// <summary>
/// Central game network manager that integrates all systems.
/// Handles player spawning, movement synchronization, and game state.
/// This is the ""glue"" that connects networking, economy, and persistence.
/// </summary>
public class GameNetworkManager : MonoBehaviour
{
    [Header(""Configuration"")]
    [SerializeField] private bool _isServer = true;
    [SerializeField] private GameObject _playerPrefab;
    
    private ServerHost _server;
    private ClientManager _client;
    private SaveSystem _saveSystem;
    private EconomyManager _economy;
    
    private Dictionary<uint, GameObject> _spawnedPlayers = new Dictionary<uint, GameObject>();
    private GameObject _localPlayer;
    
    private void Start()
    {
        // Get references
        _server = GetComponent<ServerHost>();
        _client = GetComponent<ClientManager>();
        _saveSystem = SaveSystem.Instance;
        _economy = EconomyManager.Instance;
        
        // Load player prefab
        _playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            ""Assets/_QuantumMechanic/Prefabs/Player.prefab""
        );
        
        // Hook up events
        if (_server != null)
        {
            _server.OnClientConnected += HandleClientConnected;
            _server.OnClientDisconnected += HandleClientDisconnected;
            _server.OnPacketReceived += HandleServerPacketReceived;
        }
        
        if (_client != null)
        {
            _client.OnConnected += HandleClientConnectedToServer;
            _client.OnPacketReceived += HandleClientPacketReceived;
        }
        
        // Auto-start based on configuration
        if (_isServer)
        {
            Debug.Log(""[GameNetworkManager] Starting as server"");
            // Server auto-starts in ServerHost component
        }
        else
        {
            Debug.Log(""[GameNetworkManager] Starting as client"");
            _client.Connect();
        }
    }
    
    private void HandleClientConnected(uint clientId)
    {
        Debug.Log($""[GameNetworkManager] Client {clientId} connected"");
        
        // Spawn player for this client
        SpawnPlayerForClient(clientId);
    }
    
    private void HandleClientDisconnected(uint clientId)
    {
        Debug.Log($""[GameNetworkManager] Client {clientId} disconnected"");
        
        // Destroy player object
        if (_spawnedPlayers.TryGetValue(clientId, out GameObject player))
        {
            Destroy(player);
            _spawnedPlayers.Remove(clientId);
            
            // Broadcast despawn to all clients
            NetworkPacket despawnPacket = new NetworkPacket(PacketType.Despawn, clientId, """");
            _server.BroadcastToAll(despawnPacket);
        }
    }
    
    private void SpawnPlayerForClient(uint clientId)
    {
        if (_playerPrefab == null) return;
        
        Vector3 spawnPos = new Vector3(Random.Range(-5f, 5f), 2f, Random.Range(-5f, 5f));
        GameObject player = Instantiate(_playerPrefab, spawnPos, Quaternion.identity);
        player.name = $""Player_{clientId}"";
        
        NetworkIdentity netId = player.GetComponent<NetworkIdentity>();
        if (netId != null)
        {
            netId.SetNetworkId(clientId);
        }
        
        _spawnedPlayers[clientId] = player;
        
        // Send spawn packet to all clients
        NetworkPacket spawnPacket = PacketProcessor.CreateSpawnPacket(
            clientId, ""Player"", spawnPos, false
        );
        _server.BroadcastToAll(spawnPacket);
    }
    
    private void HandleServerPacketReceived(uint clientId, NetworkPacket packet)
    {
        PacketType type = (PacketType)packet.packetType;
        
        switch (type)
        {
            case PacketType.Transform:
                // Broadcast transform to all other clients
                _server.BroadcastToAllExcept(clientId, packet);
                
                // Update server-side player position
                if (_spawnedPlayers.TryGetValue(clientId, out GameObject player))
                {
                    TransformData data = PacketProcessor.ParseTransformData(packet.payload);
                    if (data != null)
                    {
                        player.transform.position = data.GetPosition();
                        player.transform.rotation = data.GetRotation();
                    }
                }
                break;
                
            case PacketType.Chat:
                // Broadcast chat to all clients
                _server.BroadcastToAll(packet);
                ChatData chatData = PacketProcessor.ParseChatData(packet.payload);
                if (chatData != null)
                {
                    Debug.Log($""[Chat] {chatData.username}: {chatData.message}"");
                }
                break;
        }
    }
    
    private void HandleClientConnectedToServer()
    {
        Debug.Log(""[GameNetworkManager] Connected to server as client"");
    }
    
    private void HandleClientPacketReceived(NetworkPacket packet)
    {
        PacketType type = (PacketType)packet.packetType;
        
        switch (type)
        {
            case PacketType.Spawn:
                SpawnData spawnData = PacketProcessor.ParseSpawnData(packet.payload);
                if (spawnData != null && _playerPrefab != null)
                {
                    GameObject player = Instantiate(_playerPrefab, spawnData.GetPosition(), Quaternion.identity);
                    player.name = $""RemotePlayer_{spawnData.networkId}"";
                    
                    NetworkIdentity netId = player.GetComponent<NetworkIdentity>();
                    if (netId != null)
                    {
                        netId.SetNetworkId(spawnData.networkId);
                        if (spawnData.isLocalPlayer)
                        {
                            netId.SetAsLocalPlayer();
                            _localPlayer = player;
                        }
                    }
                    
                    _spawnedPlayers[spawnData.networkId] = player;
                }
                break;
                
            case PacketType.Transform:
                TransformData transformData = PacketProcessor.ParseTransformData(packet.payload);
                if (transformData != null && _spawnedPlayers.TryGetValue(packet.senderId, out GameObject remotePlayer))
                {
                    remotePlayer.transform.position = transformData.GetPosition();
                    remotePlayer.transform.rotation = transformData.GetRotation();
                }
                break;
                
            case PacketType.Chat:
                ChatData chatData = PacketProcessor.ParseChatData(packet.payload);
                if (chatData != null)
                {
                    Debug.Log($""[Chat] {chatData.username}: {chatData.message}"");
                }
                break;
        }
    }
}";
            
            File.WriteAllText(scriptPath, scriptContent);
            AssetDatabase.Refresh();
            
            Debug.Log("  GameNetworkManager script created");
        }
        
        /// <summary>
        /// Creates the PlayerController script for local player movement.
        /// </summary>
        [MenuItem("Project/Generate Player Controller")]
        public static void CreatePlayerController()
        {
            string scriptPath = $"{ROOT_FOLDER}/Scripts/Player/PlayerController.cs";
            
            if (File.Exists(scriptPath))
            {
                Debug.Log("PlayerController already exists");
                return;
            }
            
            string scriptContent = @"using UnityEngine;
using QuantumMechanic.Networking;

/// <summary>
/// Simple WASD player movement controller with network synchronization.
/// Sends transform updates to the server for multiplayer synchronization.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header(""Movement"")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 10f;
    [SerializeField] private float _gravity = -9.81f;
    
    private CharacterController _controller;
    private NetworkIdentity _netIdentity;
    private ClientManager _client;
    private Vector3 _velocity;
    private float _lastSyncTime;
    
    private void Start()
    {
        _controller = GetComponent<CharacterController>();
        _netIdentity = GetComponent<NetworkIdentity>();
        _client = FindObjectOfType<ClientManager>();
    }
    
    private void Update()
    {
        // Only allow local player to move
        if (_netIdentity != null && !_netIdentity.IsLocalPlayer)
            return;
        
        HandleMovement();
        ApplyGravity();
        SynchronizeTransform();
    }
    
    private void HandleMovement()
    {
        float h = Input.GetAxis(""Horizontal"");
        float v = Input.GetAxis(""Vertical"");
        
        Vector3 move = new Vector3(h, 0, v).normalized;
        
        if (move.magnitude > 0.1f)
        {
            // Move
            _controller.Move(move * _moveSpeed * Time.deltaTime);
            
            // Rotate
            Quaternion targetRotation = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }
    
    private void ApplyGravity()
    {
        if (_controller.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }
        
        _velocity.y += _gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }
    
    private void SynchronizeTransform()
    {
        if (_client == null || !_client.IsConnected)
            return;
        
        // Send transform at 20Hz
        if (Time.time - _lastSyncTime > 0.05f)
        {
            _client.SendTransform(transform.position, transform.rotation, _velocity);
            _lastSyncTime = Time.time;
        }
    }
}";
            
            File.WriteAllText(scriptPath, scriptContent);
            AssetDatabase.Refresh();
            
            Debug.Log("PlayerController script created");
        }
    }
}
#endif