using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuantumMechanic
{
    /// <summary>
    /// The brain of the Quantum Mechanic framework. This class:
    /// 1. Automatically starts when Unity launches (no manual setup required)
    /// 2. Finds all systems in your project using reflection
    /// 3. Validates dependencies between systems
    /// 4. Initializes everything in the correct order
    /// 5. Provides access to systems via GetSystem<T>()
    /// 6. Dispatches Update/FixedUpdate calls to systems that need them
    /// 
    /// This is a singleton - only one exists and it persists between scenes.
    /// </summary>
    public class QuantumBootstrapper : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════════════════════
        // SINGLETON PATTERN
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>The one and only instance of the bootstrapper</summary>
        private static QuantumBootstrapper instance;

        /// <summary>Public accessor for the singleton instance</summary>
        public static QuantumBootstrapper Instance => instance;

        // ═══════════════════════════════════════════════════════════════════════════════
        // STATE TRACKING
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>List of all discovered and initialized systems</summary>
        private List<IGameSystem> allSystems = new List<IGameSystem>();

        /// <summary>Systems that need OnUpdate() called every frame</summary>
        private List<IUpdateableSystem> updateableSystems = new List<IUpdateableSystem>();

        /// <summary>Systems that need OnFixedUpdate() called at fixed intervals</summary>
        private List<IFixedUpdateableSystem> fixedUpdateableSystems = new List<IFixedUpdateableSystem>();

        /// <summary>Tracks whether bootstrap has completed</summary>
        private bool isBootstrapComplete = false;

        /// <summary>Progress percentage (0-1) for loading screens</summary>
        private float bootstrapProgress = 0f;

        /// <summary>Current status message shown on loading screen</summary>
        private string currentStatusMessage = "Initializing...";

        /// <summary>Total number of systems to initialize</summary>
        private int totalSystemCount = 0;

        /// <summary>Number of systems successfully initialized</summary>
        private int initializedSystemCount = 0;

        // ═══════════════════════════════════════════════════════════════════════════════
        // AUTOMATIC STARTUP - This method runs BEFORE any scene loads
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Unity calls this automatically when the game starts (before any scene loads).
        /// Creates the bootstrapper GameObject and starts the initialization process.
        /// This is why you don't need to manually add the bootstrapper to scenes!
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            // Only create if one doesn't exist already
            if (instance == null)
            {
                GameObject bootstrapObj = new GameObject("[QuantumBootstrapper]");
                instance = bootstrapObj.AddComponent<QuantumBootstrapper>();
                DontDestroyOnLoad(bootstrapObj); // Survives scene changes
                
                UnityEngine.Debug.Log("[QuantumBootstrapper] Auto-initialized via RuntimeInitializeOnLoadMethod");
                
                // Start the bootstrap process asynchronously
                instance.StartBootstrapAsync();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // BOOTSTRAP PROCESS
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Main bootstrap sequence. Finds, validates, and initializes all systems.
        /// </summary>
        private async void StartBootstrapAsync()
        {
            Stopwatch totalTimer = Stopwatch.StartNew();
            
            try
            {
                QuantumEvents.InvokeBootstrapStarted();
                currentStatusMessage = "Discovering systems...";

                // STEP 1: Find all classes marked with [QuantumSystem]
                List<Type> systemTypes = DiscoverSystems();
                totalSystemCount = systemTypes.Count;
                UnityEngine.Debug.Log($"[QuantumBootstrapper] Discovered {totalSystemCount} systems");

                // STEP 2: Check that all dependencies are present
                ValidateDependencies(systemTypes);
                currentStatusMessage = "Validating dependencies...";

                // STEP 3: Sort by Phase and Priority
                List<Type> sortedTypes = SortSystemsByPhaseAndPriority(systemTypes);

                // STEP 4: Create instances of all systems
                List<IGameSystem> systems = CreateSystemInstances(sortedTypes);
                
                // STEP 5: Initialize systems one by one
                await InitializeSystems(systems);

                // STEP 6: Register for Unity lifecycle events
                RegisterSceneCallbacks();

                // Done!
                isBootstrapComplete = true;
                bootstrapProgress = 1f;
                totalTimer.Stop();
                
                QuantumEvents.InvokeBootstrapCompleted(
                    (float)totalTimer.Elapsed.TotalSeconds,
                    initializedSystemCount,
                    totalSystemCount
                );

                UnityEngine.Debug.Log($"[QuantumBootstrapper] Bootstrap complete! {initializedSystemCount}/{totalSystemCount} systems initialized in {totalTimer.Elapsed.TotalSeconds:F2}s");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[QuantumBootstrapper] FATAL: Bootstrap failed: {ex.Message}\n{ex.StackTrace}");
                currentStatusMessage = $"Bootstrap failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Scans all loaded assemblies for classes with [QuantumSystem] attribute.
        /// This is how the framework auto-discovers your systems without hard-coding them!
        /// </summary>
        private List<Type> DiscoverSystems()
        {
            List<Type> found = new List<Type>();

            // Get all assemblies currently loaded in the game
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (Assembly asm in assemblies)
            {
                try
                {
                    // Get all types (classes) in this assembly
                    Type[] types = asm.GetTypes();

                    foreach (Type t in types)
                    {
                        // Check if this class has the [QuantumSystem] attribute
                        if (t.GetCustomAttribute<QuantumSystemAttribute>() != null)
                        {
                            // Make sure it actually implements IGameSystem
                            if (typeof(IGameSystem).IsAssignableFrom(t) && !t.IsAbstract)
                            {
                                found.Add(t);
                            }
                            else
                            {
                                UnityEngine.Debug.LogWarning($"[QuantumBootstrapper] {t.Name} has [QuantumSystem] but doesn't implement IGameSystem");
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Some assemblies can't be fully loaded - skip them
                    UnityEngine.Debug.LogWarning($"[QuantumBootstrapper] Could not load types from {asm.GetName().Name}: {ex.Message}");
                }
            }

            return found;
        }

        /// <summary>
        /// Checks that all systems with [RequiresSystem] have their dependencies present.
        /// Throws an exception if a required system is missing.
        /// </summary>
        private void ValidateDependencies(List<Type> systemTypes)
        {
            foreach (Type t in systemTypes)
            {
                // Get all [RequiresSystem] attributes on this class
                var requireAttrs = t.GetCustomAttributes<RequiresSystemAttribute>();

                foreach (var req in requireAttrs)
                {
                    // Check if the required system exists in our discovered list
                    if (!systemTypes.Contains(req.RequiredSystemType))
                    {
                        throw new Exception($"System {t.Name} requires {req.RequiredSystemType.Name}, but it was not found!");
                    }
                }
            }
        }

        /// <summary>
        /// Sorts systems by InitializationPhase first, then by Priority within each phase.
        /// This ensures Core systems start before GameLogic, etc.
        /// </summary>
        private List<Type> SortSystemsByPhaseAndPriority(List<Type> systemTypes)
        {
            return systemTypes.OrderBy(t =>
            {
                var attr = t.GetCustomAttribute<QuantumSystemAttribute>();
                return ((int)attr.Phase * 10000) + attr.Priority; // Phase is primary, priority is secondary
            }).ToList();
        }

        /// <summary>
        /// Creates actual instances of all system classes.
        /// Uses Activator.CreateInstance to instantiate them.
        /// </summary>
        private List<IGameSystem> CreateSystemInstances(List<Type> sortedTypes)
        {
            List<IGameSystem> systems = new List<IGameSystem>();

            foreach (Type t in sortedTypes)
            {
                try
                {
                    // Create an instance of this system
                    IGameSystem sys = (IGameSystem)Activator.CreateInstance(t);
                    systems.Add(sys);

                    // Also track if it needs Update or FixedUpdate
                    if (sys is IUpdateableSystem updateable)
                    {
                        updateableSystems.Add(updateable);
                    }

                    if (sys is IFixedUpdateableSystem fixedUpdateable)
                    {
                        fixedUpdateableSystems.Add(fixedUpdateable);
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[QuantumBootstrapper] Failed to create instance of {t.Name}: {ex.Message}");
                }
            }

            return systems;
        }

        /// <summary>
        /// Initializes all systems one by one, tracking progress and handling errors.
        /// </summary>
        private async Awaitable InitializeSystems(List<IGameSystem> systems)
        {
            InitializationPhase currentPhase = InitializationPhase.Core;

            for (int i = 0; i < systems.Count; i++)
            {
                IGameSystem sys = systems[i];
                Type sysType = sys.GetType();
                var attr = sysType.GetCustomAttribute<QuantumSystemAttribute>();

                // Check if we've moved to a new phase
                if (attr.Phase != currentPhase)
                {
                    QuantumEvents.InvokePhaseCompleted(currentPhase);
                    currentPhase = attr.Phase;
                    UnityEngine.Debug.Log($"[QuantumBootstrapper] Entering phase: {currentPhase}");
                }

                // Update progress
                bootstrapProgress = (float)i / systems.Count;
                currentStatusMessage = $"Initializing {sys.GetSystemName()}...";

                // Initialize this system
                Stopwatch timer = Stopwatch.StartNew();
                
                try
                {
                    await sys.Initialize();
                    timer.Stop();

                    allSystems.Add(sys);
                    initializedSystemCount++;

                    float timeMs = (float)timer.Elapsed.TotalMilliseconds;
                    QuantumEvents.InvokeSystemInitialized(sys.GetSystemName(), timeMs);
                    
                    UnityEngine.Debug.Log($"[QuantumBootstrapper] ✓ {sys.GetSystemName()} initialized ({timeMs:F2}ms)");
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    QuantumEvents.InvokeSystemFailed(sys.GetSystemName(), ex);

                    // If system is optional, continue; otherwise, fail bootstrap
                    if (attr.Optional)
                    {
                        UnityEngine.Debug.LogWarning($"[QuantumBootstrapper] Optional system {sys.GetSystemName()} failed: {ex.Message}");
                    }
                    else
                    {
                        throw new Exception($"Critical system {sys.GetSystemName()} failed to initialize", ex);
                    }
                }

                // Small delay to prevent frame hitches
                await Awaitable.NextFrameAsync();
            }

            // Complete final phase
            QuantumEvents.InvokePhaseCompleted(currentPhase);
        }

        /// <summary>
        /// Registers for Unity's scene loading/unloading events so we can notify systems.
        /// </summary>
        private void RegisterSceneCallbacks()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        /// <summary>
        /// Called when a scene finishes loading. Notifies all systems.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var sys in allSystems)
            {
                try
                {
                    sys.OnSceneLoaded(scene);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[QuantumBootstrapper] {sys.GetSystemName()} failed in OnSceneLoaded: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Called before a scene unloads. Notifies all systems.
        /// </summary>
        private void OnSceneUnloaded(Scene scene)
        {
            foreach (var sys in allSystems)
            {
                try
                {
                    sys.OnSceneUnloaded(scene);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[QuantumBootstrapper] {sys.GetSystemName()} failed in OnSceneUnloaded: {ex.Message}");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // UNITY LIFECYCLE - Dispatch updates to systems
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called every frame by Unity. Dispatches to systems that implement IUpdateableSystem.
        /// </summary>
        private void Update()
        {
            if (!isBootstrapComplete) return;

            float dt = Time.deltaTime;

            foreach (var sys in updateableSystems)
            {
                try
                {
                    sys.OnUpdate(dt);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[QuantumBootstrapper] Error in {sys.GetType().Name}.OnUpdate: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Called at fixed intervals by Unity. Dispatches to systems that implement IFixedUpdateableSystem.
        /// </summary>
        private void FixedUpdate()
        {
            if (!isBootstrapComplete) return;

            float fdt = Time.fixedDeltaTime;

            foreach (var sys in fixedUpdateableSystems)
            {
                try
                {
                    sys.OnFixedUpdate(fdt);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[QuantumBootstrapper] Error in {sys.GetType().Name}.OnFixedUpdate: {ex.Message}");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // PUBLIC API - How other code interacts with the bootstrapper
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets a system by its type. Returns null if not found or not initialized yet.
        /// 
        /// Example:
        /// EventSystem events = QuantumBootstrapper.Instance.GetSystem<EventSystem>();
        /// if (events != null) { events.Publish("GameStarted", null); }
        /// </summary>
        public T GetSystem<T>() where T : class, IGameSystem
        {
            foreach (var sys in allSystems)
            {
                if (sys is T typedSys)
                {
                    return typedSys;
                }
            }
            return null;
        }

        /// <summary>
        /// Checks if bootstrap is complete.
        /// Useful for waiting before accessing systems.
        /// </summary>
        public bool IsBootstrapComplete() => isBootstrapComplete;

        /// <summary>
        /// Gets current bootstrap progress (0-1).
        /// Useful for loading screens.
        /// </summary>
        public float GetProgress() => bootstrapProgress;

        /// <summary>
        /// Gets a copy of all initialized systems.
        /// Useful for debugging or editor tools.
        /// </summary>
        public List<IGameSystem> GetAllSystems() => new List<IGameSystem>(allSystems);

        // ═══════════════════════════════════════════════════════════════════════════════
        // LOADING SCREEN - Simple OnGUI display while bootstrapping
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Draws a simple loading screen while bootstrap is running.
        /// MacOS-style progress bar with percentage and status message.
        /// </summary>
        private void OnGUI()
        {
            if (isBootstrapComplete) return;

            // Semi-transparent black background
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Calculate centered positions
            float centerX = Screen.width / 2f;
            float centerY = Screen.height / 2f;

            // Title
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(centerX - 200, centerY - 100, 400, 50), "Quantum Mechanic", titleStyle);

            // Progress bar background
            Rect barBg = new Rect(centerX - 250, centerY - 20, 500, 30);
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            GUI.DrawTexture(barBg, Texture2D.whiteTexture);

            // Progress bar fill
            Rect barFill = new Rect(centerX - 248, centerY - 18, 496 * bootstrapProgress, 26);
            GUI.color = new Color(0.3f, 0.7f, 1f, 1f);
            GUI.DrawTexture(barFill, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Percentage text
            GUIStyle percentStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            string percentText = $"{(bootstrapProgress * 100f):F0}%";
            GUI.Label(new Rect(centerX - 200, centerY - 20, 400, 30), percentText, percentStyle);

            // Status message
            GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 1f) }
            };
            GUI.Label(new Rect(centerX - 200, centerY + 30, 400, 30), currentStatusMessage, statusStyle);

            // System count
            string countText = $"{initializedSystemCount} / {totalSystemCount} systems";
            GUI.Label(new Rect(centerX - 200, centerY + 60, 400, 30), countText, statusStyle);
        }
    }
}