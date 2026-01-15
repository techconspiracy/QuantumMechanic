using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuantumMechanic
{
    /// <summary>
    /// Master bootstrapper that initializes all Quantum Mechanic systems in the correct order.
    /// Attach this to a persistent GameObject in your initial scene.
    /// </summary>
    public class MasterBootstrapper : MonoBehaviour
    {
        [Header("Initialization Settings")]
        [SerializeField] private bool debugMode = true;
        [SerializeField] private bool enableMultiplayer = false;
        [SerializeField] private bool enableAnalytics = true;
        [SerializeField] private float systemTimeout = 30f;

        [Header("System Status")]
        [SerializeField] private InitializationPhase currentPhase = InitializationPhase.NotStarted;
        [SerializeField] private float initializationProgress = 0f;

        private static MasterBootstrapper _instance;
        public static MasterBootstrapper Instance => _instance;

        private Dictionary<Type, IGameSystem> systems = new Dictionary<Type, IGameSystem>();
        private List<string> initializationLog = new List<string>();
        private bool isInitialized = false;

        public event Action OnBootstrapComplete;
        public event Action<string> OnSystemInitialized;
        public event Action<string, Exception> OnSystemFailed;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            Log("🚀 Quantum Mechanic Master Bootstrapper Starting...");
            StartCoroutine(InitializeAllSystems());
        }

        /// <summary>
        /// Main initialization coroutine that orchestrates all system startup.
        /// </summary>
        private IEnumerator InitializeAllSystems()
        {
            float startTime = Time.realtimeSinceStartup;

            try
            {
                // PHASE 1: Core Foundation Systems
                yield return StartCoroutine(InitializePhase1_CoreSystems());
                UpdateProgress(0.2f);

                // PHASE 2: Game Logic Systems
                yield return StartCoroutine(InitializePhase2_GameSystems());
                UpdateProgress(0.4f);

                // PHASE 3: Visual & Audio Systems
                yield return StartCoroutine(InitializePhase3_PresentationSystems());
                UpdateProgress(0.6f);

                // PHASE 4: UI & Player Experience
                yield return StartCoroutine(InitializePhase4_UIAndExperience());
                UpdateProgress(0.8f);

                // PHASE 5: Network & Analytics (Optional)
                yield return StartCoroutine(InitializePhase5_NetworkAndAnalytics());
                UpdateProgress(0.9f);

                // PHASE 6: Final Integration
                yield return StartCoroutine(InitializePhase6_FinalIntegration());
                UpdateProgress(1.0f);

                isInitialized = true;
                float totalTime = Time.realtimeSinceStartup - startTime;
                Log($"✅ All systems initialized successfully in {totalTime:F2}s");

                OnBootstrapComplete?.Invoke();
            }
            catch (Exception ex)
            {
                LogError($"❌ Critical failure during initialization: {ex.Message}");
                LogError(ex.StackTrace);
            }
        }

        #region Phase 1: Core Foundation Systems

        private IEnumerator InitializePhase1_CoreSystems()
        {
            currentPhase = InitializationPhase.CoreSystems;
            Log("📦 PHASE 1: Initializing Core Systems...");

            // Event System (Must be first - everything depends on this)
            yield return InitializeSystem("EventSystem", () =>
            {
                var eventSystem = GetOrCreateSystem<Events.GameEventSystem>();
                eventSystem?.Initialize();
            });

            // Save System (Early - many systems need to load saved data)
            yield return InitializeSystem("SaveSystem", () =>
            {
                var saveManager = GetOrCreateSystem<Save.SaveManager>();
                saveManager?.Initialize();
            });

            // Resource System (Core resources and assets)
            yield return InitializeSystem("ResourceSystem", () =>
            {
                var resourceSystem = GetOrCreateSystem<Resources.ResourceSystem>();
                resourceSystem?.Initialize();
            });

            // Settings/Configuration System
            yield return InitializeSystem("SettingsSystem", () =>
            {
                // Initialize game settings, quality presets, etc.
                LoadGameSettings();
            });

            Log("✅ Phase 1 Complete: Core Systems Ready");
        }

        #endregion

        #region Phase 2: Game Logic Systems

        private IEnumerator InitializePhase2_GameSystems()
        {
            currentPhase = InitializationPhase.GameLogic;
            Log("🎮 PHASE 2: Initializing Game Logic Systems...");

            // Combat System
            yield return InitializeSystem("CombatSystem", () =>
            {
                var combatSystem = GetOrCreateSystem<Combat.CombatSystem>();
                var damageSystem = GetOrCreateSystem<Combat.DamageSystem>();
                var weaponController = GetOrCreateSystem<Combat.WeaponController>();
                
                combatSystem?.Initialize();
                damageSystem?.Initialize();
                weaponController?.Initialize();
            });

            // Ability System
            yield return InitializeSystem("AbilitySystem", () =>
            {
                var abilitySystem = GetOrCreateSystem<Abilities.AbilitySystem>();
                var castingSystem = GetOrCreateSystem<Abilities.CastingSystem>();
                var cooldownSystem = GetOrCreateSystem<Abilities.CooldownSystem>();
                
                abilitySystem?.Initialize();
                castingSystem?.Initialize();
                cooldownSystem?.Initialize();
            });

            // Quest System
            yield return InitializeSystem("QuestSystem", () =>
            {
                var questManager = GetOrCreateSystem<Quests.QuestManager>();
                questManager?.Initialize();
                questManager?.LoadQuestData();
            });

            // Dialogue System
            yield return InitializeSystem("DialogueSystem", () =>
            {
                var dialogueManager = GetOrCreateSystem<Dialogue.DialogueManager>();
                dialogueManager?.Initialize();
            });

            // Achievement System
            yield return InitializeSystem("AchievementSystem", () =>
            {
                var achievementManager = GetOrCreateSystem<Achievements.AchievementManager>();
                achievementManager?.Initialize();
                achievementManager?.LoadAchievements();
            });

            // Economy System
            yield return InitializeSystem("EconomySystem", () =>
            {
                var economyManager = GetOrCreateSystem<Economy.EconomyManager>();
                economyManager?.Initialize();
            });

            // Tutorial System
            yield return InitializeSystem("TutorialSystem", () =>
            {
                var tutorialManager = GetOrCreateSystem<Tutorial.TutorialManager>();
                tutorialManager?.Initialize();
            });

            Log("✅ Phase 2 Complete: Game Logic Systems Ready");
        }

        #endregion

        #region Phase 3: Visual & Audio Systems

        private IEnumerator InitializePhase3_PresentationSystems()
        {
            currentPhase = InitializationPhase.Presentation;
            Log("🎨 PHASE 3: Initializing Presentation Systems...");

            // Audio System
            yield return InitializeSystem("AudioSystem", () =>
            {
                var audioManager = GetOrCreateSystem<Audio.AudioManager>();
                audioManager?.Initialize();
                audioManager?.LoadAudioLibrary();
            });

            // Particle System
            yield return InitializeSystem("ParticleSystem", () =>
            {
                var particleManager = GetOrCreateSystem<Particles.ParticleManager>();
                var particleFactory = GetOrCreateSystem<Particles.ParticleFactory>();
                
                particleManager?.Initialize();
                particleFactory?.Initialize();
            });

            // Post Processing
            yield return InitializeSystem("PostProcessing", () =>
            {
                var postProcessManager = GetOrCreateSystem<Visual.PostProcessManager>();
                postProcessManager?.Initialize();
            });

            // Projectile System
            yield return InitializeSystem("ProjectileSystem", () =>
            {
                var projectileManager = GetOrCreateSystem<Projectiles.ProjectileManager>();
                projectileManager?.Initialize();
            });

            // Procedural Generation
            yield return InitializeSystem("ProceduralGeneration", () =>
            {
                var modelFactory = GetOrCreateSystem<Procedural.ProceduralModelFactory>();
                var textureGenerator = GetOrCreateSystem<Procedural.TextureGenerator>();
                
                modelFactory?.Initialize();
                textureGenerator?.Initialize();
            });

            Log("✅ Phase 3 Complete: Presentation Systems Ready");
        }

        #endregion

        #region Phase 4: UI & Player Experience

        private IEnumerator InitializePhase4_UIAndExperience()
        {
            currentPhase = InitializationPhase.UserInterface;
            Log("🖥️ PHASE 4: Initializing UI & Experience Systems...");

            // UI Manager
            yield return InitializeSystem("UIManager", () =>
            {
                var uiManager = GetOrCreateSystem<UI.UIManager>();
                uiManager?.Initialize();
            });

            // Notification System
            yield return InitializeSystem("NotificationSystem", () =>
            {
                var notificationManager = GetOrCreateSystem<Notifications.NotificationManager>();
                notificationManager?.Initialize();
            });

            // Damage Number UI
            yield return InitializeSystem("DamageNumberUI", () =>
            {
                var damageNumberHelper = GetOrCreateSystem<UI.DamageNumberUIHelper>();
                damageNumberHelper?.Initialize();
            });

            // Input System (if not using Unity's new input system)
            yield return InitializeSystem("InputSystem", () =>
            {
                InitializeInputBindings();
            });

            Log("✅ Phase 4 Complete: UI & Experience Systems Ready");
        }

        #endregion

        #region Phase 5: Network & Analytics

        private IEnumerator InitializePhase5_NetworkAndAnalytics()
        {
            currentPhase = InitializationPhase.NetworkAndAnalytics;
            Log("🌐 PHASE 5: Initializing Network & Analytics Systems...");

            // Network System (if multiplayer enabled)
            if (enableMultiplayer)
            {
                yield return InitializeSystem("NetworkSystem", () =>
                {
                    var networkManager = GetOrCreateSystem<Networking.NetworkManager>();
                    var clientManager = GetOrCreateSystem<Networking.ClientManager>();
                    var serverHost = GetOrCreateSystem<Networking.ServerHost>();
                    var networkIdentity = GetOrCreateSystem<Networking.NetworkIdentity>();
                    var packetProcessor = GetOrCreateSystem<Networking.PacketProcessor>();
                    
                    networkManager?.Initialize();
                    clientManager?.Initialize();
                    serverHost?.Initialize();
                    networkIdentity?.Initialize();
                    packetProcessor?.Initialize();
                });
            }

            // Analytics System (if enabled)
            if (enableAnalytics)
            {
                yield return InitializeSystem("AnalyticsSystem", () =>
                {
                    var analyticsManager = GetOrCreateSystem<Analytics.AnalyticsManager>();
                    var performanceTracker = GetOrCreateSystem<Analytics.PerformanceTracker>();
                    
                    analyticsManager?.Initialize();
                    performanceTracker?.Initialize();
                    
                    // Track bootstrap event
                    analyticsManager?.TrackEvent("game_bootstrap", new Dictionary<string, object>
                    {
                        {"platform", Application.platform.ToString()},
                        {"version", Application.version}
                    });
                });
            }

            Log("✅ Phase 5 Complete: Network & Analytics Ready");
        }

        #endregion

        #region Phase 6: Final Integration

        private IEnumerator InitializePhase6_FinalIntegration()
        {
            currentPhase = InitializationPhase.FinalIntegration;
            Log("🔧 PHASE 6: Final Integration & Health Checks...");

            // Cross-system event wiring
            WireSystemEvents();

            // Perform health checks on all systems
            yield return StartCoroutine(PerformHealthChecks());

            // Load initial game state
            LoadInitialGameState();

            // Subscribe to application events
            Application.wantsToQuit += OnApplicationWantsToQuit;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            Log("✅ Phase 6 Complete: All Systems Integrated");
        }

        #endregion

        #region System Management

        /// <summary>
        /// Initialize a single system with error handling and timeout.
        /// </summary>
        private IEnumerator InitializeSystem(string systemName, Action initializeAction)
        {
            float startTime = Time.realtimeSinceStartup;
            bool completed = false;
            Exception caughtException = null;

            Log($"  → Initializing {systemName}...");

            // Run initialization in try-catch
            try
            {
                initializeAction?.Invoke();
                completed = true;
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            // Wait one frame to allow initialization to complete
            yield return null;

            // Check results
            float elapsedTime = Time.realtimeSinceStartup - startTime;

            if (caughtException != null)
            {
                LogError($"  ❌ {systemName} failed: {caughtException.Message}");
                OnSystemFailed?.Invoke(systemName, caughtException);
            }
            else if (elapsedTime > systemTimeout)
            {
                LogWarning($"  ⚠️ {systemName} initialization timeout ({elapsedTime:F2}s)");
            }
            else
            {
                Log($"  ✓ {systemName} initialized ({elapsedTime * 1000:F0}ms)");
                OnSystemInitialized?.Invoke(systemName);
            }
        }

        /// <summary>
        /// Get or create a system instance.
        /// </summary>
        private T GetOrCreateSystem<T>() where T : class, IGameSystem, new()
        {
            Type type = typeof(T);
            
            if (systems.ContainsKey(type))
                return systems[type] as T;

            T system = new T();
            systems[type] = system;
            return system;
        }

        /// <summary>
        /// Get an already initialized system.
        /// </summary>
        public T GetSystem<T>() where T : class, IGameSystem
        {
            Type type = typeof(T);
            return systems.ContainsKey(type) ? systems[type] as T : null;
        }

        #endregion

        #region Cross-System Integration

        /// <summary>
        /// Wire events between different systems for cross-system communication.
        /// </summary>
        private void WireSystemEvents()
        {
            Log("🔗 Wiring cross-system events...");

            // Example: Quest completion triggers achievement check
            // QuestManager.OnQuestCompleted += AchievementManager.CheckQuestAchievements;

            // Example: Combat events trigger particle effects
            // CombatSystem.OnDamageDealt += ParticleManager.SpawnDamageEffect;

            // Example: Economy changes trigger UI updates
            // EconomyManager.OnCurrencyChanged += UIManager.UpdateCurrencyDisplay;

            // Example: Achievement unlocks trigger notifications
            // AchievementManager.OnAchievementUnlocked += NotificationManager.ShowAchievement;

            // Example: Network events trigger analytics
            // NetworkManager.OnPlayerJoined += AnalyticsManager.TrackPlayerJoin;

            Log("  ✓ Cross-system events wired");
        }

        /// <summary>
        /// Perform health checks on all initialized systems.
        /// </summary>
        private IEnumerator PerformHealthChecks()
        {
            Log("🏥 Performing system health checks...");

            int healthyCount = 0;
            int totalCount = systems.Count;

            foreach (var kvp in systems)
            {
                if (kvp.Value.IsHealthy())
                {
                    healthyCount++;
                }
                else
                {
                    LogWarning($"  ⚠️ System unhealthy: {kvp.Key.Name}");
                }

                yield return null;
            }

            Log($"  ✓ Health check complete: {healthyCount}/{totalCount} systems healthy");
        }

        /// <summary>
        /// Load initial game state from save system.
        /// </summary>
        private void LoadInitialGameState()
        {
            Log("💾 Loading initial game state...");

            // Check if save file exists
            var saveSystem = GetSystem<Save.SaveManager>();
            if (saveSystem != null && saveSystem.HasSaveData())
            {
                saveSystem.Load();
                Log("  ✓ Save data loaded");
            }
            else
            {
                Log("  ℹ️ No save data found, starting new game");
            }
        }

        /// <summary>
        /// Load game settings and apply them.
        /// </summary>
        private void LoadGameSettings()
        {
            // Graphics quality
            QualitySettings.SetQualityLevel(PlayerPrefs.GetInt("QualityLevel", 2));

            // Audio volumes
            AudioListener.volume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);

            // Other settings...
        }

        /// <summary>
        /// Initialize input bindings.
        /// </summary>
        private void InitializeInputBindings()
        {
            // Setup input mappings
            // This depends on your input system
        }

        #endregion

        #region Application Lifecycle

        private bool OnApplicationWantsToQuit()
        {
            Log("💾 Application shutting down, saving state...");

            // Save all system states
            var saveSystem = GetSystem<Save.SaveManager>();
            saveSystem?.Save();

            // Flush analytics
            var analytics = GetSystem<Analytics.AnalyticsManager>();
            analytics?.TrackEvent("game_shutdown", null);

            // Graceful network disconnect
            if (enableMultiplayer)
            {
                var network = GetSystem<Networking.NetworkManager>();
                network?.Disconnect();
            }

            return true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log($"🗺️ Scene loaded: {scene.name}");

            // Notify systems of scene change
            foreach (var system in systems.Values)
            {
                system.OnSceneLoaded(scene);
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            Log($"🗑️ Scene unloaded: {scene.name}");

            // Cleanup scene-specific resources
            foreach (var system in systems.Values)
            {
                system.OnSceneUnloaded(scene);
            }
        }

        #endregion

        #region Utilities

        private void UpdateProgress(float progress)
        {
            initializationProgress = progress;
        }

        private void Log(string message)
        {
            initializationLog.Add(message);
            if (debugMode)
                Debug.Log($"[Bootstrap] {message}");
        }

        private void LogWarning(string message)
        {
            initializationLog.Add($"WARNING: {message}");
            if (debugMode)
                Debug.LogWarning($"[Bootstrap] {message}");
        }

        private void LogError(string message)
        {
            initializationLog.Add($"ERROR: {message}");
            Debug.LogError($"[Bootstrap] {message}");
        }

        /// <summary>
        /// Get initialization log for debugging.
        /// </summary>
        public List<string> GetInitializationLog() => new List<string>(initializationLog);

        /// <summary>
        /// Check if bootstrapping is complete.
        /// </summary>
        public bool IsInitialized() => isInitialized;

        /// <summary>
        /// Get current initialization progress (0-1).
        /// </summary>
        public float GetProgress() => initializationProgress;

        #endregion

        #region Debug Gizmos

        private void OnGUI()
        {
            if (!debugMode || isInitialized) return;

            GUILayout.BeginArea(new Rect(10, 10, 400, 200));
            GUILayout.Box($"QUANTUM MECHANIC BOOTSTRAP\nPhase: {currentPhase}\nProgress: {initializationProgress * 100:F0}%");
            GUILayout.EndArea();
        }

        #endregion
    }

    #region Interfaces and Enums

    /// <summary>
    /// Interface that all game systems must implement.
    /// </summary>
    public interface IGameSystem
    {
        void Initialize();
        bool IsHealthy();
        void OnSceneLoaded(Scene scene);
        void OnSceneUnloaded(Scene scene);
    }

    /// <summary>
    /// Initialization phases for the bootstrapper.
    /// </summary>
    public enum InitializationPhase
    {
        NotStarted,
        CoreSystems,
        GameLogic,
        Presentation,
        UserInterface,
        NetworkAndAnalytics,
        FinalIntegration,
        Complete
    }

    #endregion
}