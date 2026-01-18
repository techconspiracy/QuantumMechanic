using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuantumMechanic
{
    // ═══════════════════════════════════════════════════════════════════════════════════
    // CORE INTERFACES - These define the contract that all game systems must follow
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Base interface that every game system must implement.
    /// This ensures systems can initialize themselves, report their health status,
    /// and respond to scene changes (like when a new level loads).
    /// </summary>
    public interface IGameSystem
    {
        /// <summary>
        /// Called once when the system first starts up.
        /// Use this to set up resources, connect to other systems, load data, etc.
        /// This method is async, so you can use "await" to wait for things to finish.
        /// </summary>
        /// <returns>An awaitable task that completes when initialization is done</returns>
        Awaitable Initialize();

        /// <summary>
        /// Checks if the system is working correctly.
        /// Returns true if everything is good, false if something is broken.
        /// The bootstrapper uses this to detect failing systems.
        /// </summary>
        /// <returns>True if healthy, false if broken</returns>
        bool IsHealthy();

        /// <summary>
        /// Called automatically whenever a new scene finishes loading in Unity.
        /// Use this to set up scene-specific things like finding objects in the scene.
        /// </summary>
        /// <param name="scene">The scene that just loaded</param>
        void OnSceneLoaded(Scene scene);

        /// <summary>
        /// Called automatically before a scene is destroyed/unloaded.
        /// Use this to clean up anything you set up in OnSceneLoaded.
        /// </summary>
        /// <param name="scene">The scene that's about to unload</param>
        void OnSceneUnloaded(Scene scene);

        /// <summary>
        /// Returns a human-readable name for this system (like "EventSystem" or "QuestSystem").
        /// Used for logging and debugging.
        /// </summary>
        /// <returns>The system's display name</returns>
        string GetSystemName();
    }

    /// <summary>
    /// Optional interface for systems that need to run code every frame.
    /// If your system implements this, it will automatically get OnUpdate() called.
    /// Example: A system that moves objects smoothly would use this.
    /// </summary>
    public interface IUpdateableSystem
    {
        /// <summary>
        /// Called every frame (typically 60 times per second).
        /// Use this for smooth visual updates, input checking, animations, etc.
        /// </summary>
        /// <param name="deltaTime">Time since last frame in seconds (usually ~0.016s)</param>
        void OnUpdate(float deltaTime);
    }

    /// <summary>
    /// Optional interface for systems that need physics-accurate timing.
    /// If your system implements this, it will automatically get OnFixedUpdate() called.
    /// Example: A physics-based combat system would use this for consistent results.
    /// </summary>
    public interface IFixedUpdateableSystem
    {
        /// <summary>
        /// Called at a fixed rate (default 50 times per second).
        /// Use this for physics calculations, network updates, or anything that needs
        /// consistent timing regardless of frame rate.
        /// </summary>
        /// <param name="fixedDeltaTime">Fixed time step in seconds (usually 0.02s)</param>
        void OnFixedUpdate(float fixedDeltaTime);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // INITIALIZATION PHASES - Controls the order systems start up in
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Defines the order that systems initialize in.
    /// Lower numbers start first (Core = 0 starts before UI = 3).
    /// This prevents systems from trying to use other systems before they're ready.
    /// 
    /// Example: The EventSystem (Core) must start before QuestSystem (GameLogic)
    /// because QuestSystem needs to subscribe to events.
    /// </summary>
    public enum InitializationPhase
    {
        /// <summary>Phase 0: Foundational systems that everything else depends on</summary>
        /// <remarks>Examples: Events, SaveData, ResourceLoading</remarks>
        Core = 0,

        /// <summary>Phase 1: Gameplay mechanics and logic systems</summary>
        /// <remarks>Examples: Combat, Quests, Abilities, Inventory</remarks>
        GameLogic = 1,

        /// <summary>Phase 2: Visual and audio feedback systems</summary>
        /// <remarks>Examples: ParticleEffects, AudioManager, VisualEffects</remarks>
        Presentation = 2,

        /// <summary>Phase 3: User interface and menu systems</summary>
        /// <remarks>Examples: HUD, MainMenu, InventoryUI, DialogueUI</remarks>
        UserInterface = 3,

        /// <summary>Phase 4: External communication systems</summary>
        /// <remarks>Examples: Multiplayer, Analytics, CloudSave, Leaderboards</remarks>
        NetworkAndAnalytics = 4,

        /// <summary>Phase 5: Final integration and cross-system wiring</summary>
        /// <remarks>Examples: Systems that connect multiple other systems together</remarks>
        FinalIntegration = 5
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // SYSTEM ATTRIBUTES - Decorators that provide metadata about your systems
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Marks a class as a Quantum Mechanic system so the bootstrapper can find it.
    /// Also specifies WHEN and in what ORDER the system should initialize.
    /// 
    /// Usage:
    /// [QuantumSystem(InitializationPhase.Core, priority: 100)]
    /// public class MySystem : BaseGameSystem { ... }
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class QuantumSystemAttribute : Attribute
    {
        /// <summary>Which phase this system initializes in (Core, GameLogic, etc.)</summary>
        public InitializationPhase Phase { get; }

        /// <summary>
        /// Priority within the phase (higher numbers = later).
        /// Within the same phase, priority 50 starts before priority 100.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// If true, the bootstrapper won't fail if this system fails to initialize.
        /// Use this for non-critical systems like analytics or optional features.
        /// </summary>
        public bool Optional { get; set; }

        /// <summary>
        /// Human-readable name shown in editor tools and logs.
        /// If not set, uses the class name.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Creates a new QuantumSystem attribute.
        /// </summary>
        /// <param name="phase">When to initialize (Core is earliest, FinalIntegration is latest)</param>
        /// <param name="priority">Order within phase (lower = earlier, default 100)</param>
        public QuantumSystemAttribute(InitializationPhase phase, int priority = 100)
        {
            Phase = phase;
            Priority = priority;
        }
    }

    /// <summary>
    /// Marks a system as safe to reload while the editor is running.
    /// Without this, the system will be skipped during hot-reloads to prevent crashes.
    /// Only add this if your system can safely reinitialize without breaking stuff.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class HotReloadableAttribute : Attribute { }

    /// <summary>
    /// Declares that this system needs another system to work.
    /// The bootstrapper will verify dependencies and initialize in the right order.
    /// 
    /// Usage:
    /// [RequiresSystem(typeof(EventSystem))]
    /// public class QuestSystem : BaseGameSystem { ... }
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RequiresSystemAttribute : Attribute
    {
        /// <summary>The Type of the system this one depends on</summary>
        public Type RequiredSystemType { get; }

        /// <summary>
        /// Creates a dependency declaration.
        /// </summary>
        /// <param name="requiredSystemType">The system Type that must exist (e.g., typeof(EventSystem))</param>
        public RequiresSystemAttribute(Type requiredSystemType)
        {
            RequiredSystemType = requiredSystemType;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // BASE GAME SYSTEM - Provides common functionality for all systems
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Base class that all game systems should inherit from.
    /// Provides automatic error handling, logging, health checking, and lifecycle management.
    /// 
    /// Instead of inheriting from MonoBehaviour, inherit from this:
    /// public class MySystem : BaseGameSystem { ... }
    /// </summary>
    public abstract class BaseGameSystem : IGameSystem
    {
        /// <summary>Tracks whether Initialize() has completed successfully</summary>
        protected bool isInitialized = false;

        /// <summary>Tracks whether the system is working correctly (can be set to false if errors occur)</summary>
        protected bool isHealthy = true;

        /// <summary>The display name of this system (set automatically from class name or DisplayName attribute)</summary>
        protected string systemName;

        /// <summary>
        /// Public initialization method called by the bootstrapper.
        /// This wraps your OnInitialize() method with error handling and logging.
        /// You should NOT override this - override OnInitialize() instead.
        /// </summary>
        public virtual async Awaitable Initialize()
        {
            try
            {
                // Set the system name from the class name if not already set
                if (string.IsNullOrEmpty(systemName))
                {
                    systemName = GetType().Name;
                }

                Log($"Initializing {systemName}...");

                // Call the derived class's initialization logic
                await OnInitialize();

                isInitialized = true;
                Log($"{systemName} initialized successfully");
            }
            catch (Exception ex)
            {
                // If initialization fails, mark system as unhealthy and log the error
                isHealthy = false;
                isInitialized = false;
                LogError($"{systemName} failed to initialize: {ex.Message}\n{ex.StackTrace}");
                throw; // Re-throw so bootstrapper knows initialization failed
            }
        }

        /// <summary>
        /// Override this method in your system to implement initialization logic.
        /// This is where you set up resources, connect to other systems, etc.
        /// 
        /// Example:
        /// protected override async Awaitable OnInitialize()
        /// {
        ///     myData = new Dictionary<string, object>();
        ///     await Awaitable.NextFrameAsync(); // Wait one frame if needed
        /// }
        /// </summary>
        protected abstract Awaitable OnInitialize();

        /// <summary>
        /// Checks if the system is initialized and healthy.
        /// You can override this to add custom health checks.
        /// 
        /// Example:
        /// public override bool IsHealthy()
        /// {
        ///     return base.IsHealthy() && myDatabase != null && myDatabase.IsConnected;
        /// }
        /// </summary>
        public virtual bool IsHealthy() => isHealthy && isInitialized;

        /// <summary>
        /// Called when a scene loads. Override to handle scene-specific setup.
        /// Example: Finding objects in the scene, spawning UI, etc.
        /// </summary>
        public virtual void OnSceneLoaded(Scene scene) { }

        /// <summary>
        /// Called before a scene unloads. Override to clean up scene-specific things.
        /// Example: Destroying spawned objects, clearing cached references, etc.
        /// </summary>
        public virtual void OnSceneUnloaded(Scene scene) { }

        /// <summary>
        /// Returns the system's display name.
        /// </summary>
        public virtual string GetSystemName() => systemName;

        // ═══════════════════════════════════════════════════════════════════════════════
        // LOGGING HELPERS - Makes logging consistent and easy
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Logs an info message to the Unity console.
        /// Automatically prefixes with [SystemName] for easy filtering.
        /// </summary>
        protected void Log(string message)
        {
            Debug.Log($"[{systemName}] {message}");
        }

        /// <summary>
        /// Logs a warning message to the Unity console.
        /// Use for non-critical issues that don't break functionality.
        /// </summary>
        protected void LogWarning(string message)
        {
            Debug.LogWarning($"[{systemName}] {message}");
        }

        /// <summary>
        /// Logs an error message to the Unity console.
        /// Use for serious problems that affect functionality.
        /// </summary>
        protected void LogError(string message)
        {
            Debug.LogError($"[{systemName}] {message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // QUANTUM EVENTS - Global event system for bootstrap lifecycle
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Static event hub that fires during the bootstrapping process.
    /// Other systems can subscribe to these to know when initialization happens.
    /// 
    /// Example:
    /// QuantumEvents.OnSystemInitialized += (name, time) => Debug.Log($"{name} took {time}ms");
    /// </summary>
    public static class QuantumEvents
    {
        /// <summary>
        /// Fired when the bootstrap process starts (before any systems initialize).
        /// </summary>
        public static event Action OnBootstrapStarted;

        /// <summary>
        /// Fired after each individual system finishes initializing.
        /// Args: (systemName, initializationTimeMs)
        /// </summary>
        public static event Action<string, float> OnSystemInitialized;

        /// <summary>
        /// Fired when a system fails to initialize.
        /// Args: (systemName, exception)
        /// </summary>
        public static event Action<string, Exception> OnSystemFailed;

        /// <summary>
        /// Fired when an entire initialization phase completes.
        /// Args: (phase) - which phase just finished (Core, GameLogic, etc.)
        /// </summary>
        public static event Action<InitializationPhase> OnPhaseCompleted;

        /// <summary>
        /// Fired when the entire bootstrap process completes.
        /// Args: (totalTimeSeconds, successCount, totalCount)
        /// </summary>
        public static event Action<float, int, int> OnBootstrapCompleted;

        // Internal invoke methods (only the bootstrapper should call these)
        internal static void InvokeBootstrapStarted() => OnBootstrapStarted?.Invoke();
        internal static void InvokeSystemInitialized(string name, float time) => OnSystemInitialized?.Invoke(name, time);
        internal static void InvokeSystemFailed(string name, Exception ex) => OnSystemFailed?.Invoke(name, ex);
        internal static void InvokePhaseCompleted(InitializationPhase phase) => OnPhaseCompleted?.Invoke(phase);
        internal static void InvokeBootstrapCompleted(float time, int success, int total) => OnBootstrapCompleted?.Invoke(time, success, total);
    }
}