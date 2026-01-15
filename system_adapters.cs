using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuantumMechanic
{
    /// <summary>
    /// Example adapter implementations showing how to make your existing systems
    /// compatible with the MasterBootstrapper's IGameSystem interface.
    /// 
    /// For each of your existing singleton managers, create a similar adapter
    /// or modify the original class to implement IGameSystem.
    /// </summary>

    #region Event System Adapter

    namespace Events
    {
        public class GameEventSystem : IGameSystem
        {
            private static GameEventSystem _instance;
            public static GameEventSystem Instance => _instance ?? (_instance = new GameEventSystem());

            private bool isInitialized = false;

            public void Initialize()
            {
                if (isInitialized) return;
                
                // Initialize your event system here
                Debug.Log("[EventSystem] Initializing...");
                
                isInitialized = true;
            }

            public bool IsHealthy()
            {
                return isInitialized;
            }

            public void OnSceneLoaded(Scene scene)
            {
                // Clear scene-specific event subscriptions if needed
            }

            public void OnSceneUnloaded(Scene scene)
            {
                // Cleanup scene-specific events
            }

            // Your existing event system methods...
            public void Publish(string eventName, object data = null) { }
            public void Subscribe(string eventName, System.Action<object> callback) { }
            public void Unsubscribe(string eventName, System.Action<object> callback) { }
        }
    }

    #endregion

    #region Save System Adapter

    namespace Save
    {
        public class SaveManager : IGameSystem
        {
            private static SaveManager _instance;
            public static SaveManager Instance => _instance ?? (_instance = new SaveManager());

            private bool isInitialized = false;

            public void Initialize()
            {
                if (isInitialized) return;
                
                Debug.Log("[SaveManager] Initializing...");
                // Setup save directory, load encryption keys, etc.
                
                isInitialized = true;
            }

            public bool IsHealthy()
            {
                return isInitialized;
            }

            public void OnSceneLoaded(Scene scene)
            {
                // Auto-save on scene transitions if enabled
            }

            public void OnSceneUnloaded(Scene scene)
            {
                // Cleanup scene-specific save data
            }

            // Your existing save system methods...
            public void Save() { }
            public void Load() { }
            public bool HasSaveData() { return false; }
        }
    }

    #endregion

    #region Resource System Adapter

    namespace Resources
    {
        public class ResourceSystem : IGameSystem
        {
            private static ResourceSystem _instance;
            public static ResourceSystem Instance => _instance ?? (_instance = new ResourceSystem());

            private bool isInitialized = false;

            public void Initialize()
            {
                if (isInitialized) return;
                
                Debug.Log("[ResourceSystem] Initializing...");
                // Load resource manifests, setup asset bundles, etc.
                
                isInitialized = true;
            }

            public bool IsHealthy()
            {
                return isInitialized;
            }

            public void OnSceneLoaded(Scene scene)
            {
                // Load scene-specific resources
            }

            public void OnSceneUnloaded(Scene scene)
            {
                // Unload scene resources
            }
        }
    }

    #endregion

    #region Combat System Adapter

    namespace Combat
    {
        public class CombatSystem : IGameSystem
        {
            private static CombatSystem _instance;
            public static CombatSystem Instance => _instance ?? (_instance = new CombatSystem());

            private bool isInitialized = false;

            public void Initialize()
            {
                if (isInitialized) return;
                
                Debug.Log("[CombatSystem] Initializing...");
                
                isInitialized = true;
            }

            public bool IsHealthy()
            {
                return isInitialized;
            }

            public void OnSceneLoaded(Scene scene)
            {
                // Setup combat zones for new scene
            }

            public void OnSceneUnloaded(Scene scene)
            {
                // Clear combat state
            }
        }

        public class DamageSystem : IGameSystem
        {
            private static DamageSystem _instance;
            public static DamageSystem Instance => _instance ?? (_instance = new DamageSystem());

            public void Initialize() { }
            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }

        public class WeaponController : IGameSystem
        {
            private static WeaponController _instance;
            public static WeaponController Instance => _instance ?? (_instance = new WeaponController());

            public void Initialize() { }
            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }
    }

    #endregion

    #region Ability System Adapter

    namespace Abilities
    {
        public class AbilitySystem : IGameSystem
        {
            private static AbilitySystem _instance;
            public static AbilitySystem Instance => _instance ?? (_instance = new AbilitySystem());

            public void Initialize()
            {
                Debug.Log("[AbilitySystem] Initializing...");
            }

            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }

        public class CastingSystem : IGameSystem
        {
            private static CastingSystem _instance;
            public static CastingSystem Instance => _instance ?? (_instance = new CastingSystem());

            public void Initialize() { }
            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }

        public class CooldownSystem : IGameSystem
        {
            private static CooldownSystem _instance;
            public static CooldownSystem Instance => _instance ?? (_instance = new CooldownSystem());

            public void Initialize() { }
            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }
    }

    #endregion

    #region Quest System Adapter

    namespace Quests
    {
        public class QuestManager : IGameSystem
        {
            private static QuestManager _instance;
            public static QuestManager Instance => _instance ?? (_instance = new QuestManager());

            private bool isInitialized = false;

            public void Initialize()
            {
                if (isInitialized) return;
                Debug.Log("[QuestManager] Initializing...");
                isInitialized = true;
            }

            public void LoadQuestData()
            {
                // Load quest definitions from JSON/ScriptableObjects
            }

            public bool IsHealthy() { return isInitialized; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }
    }

    #endregion

    #region Dialogue System Adapter

    namespace Dialogue
    {
        public class DialogueManager : IGameSystem
        {
            private static DialogueManager _instance;
            public static DialogueManager Instance => _instance ?? (_instance = new DialogueManager());

            public void Initialize()
            {
                Debug.Log("[DialogueManager] Initializing...");
            }

            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }
    }

    #endregion

    #region Achievement System Adapter

    namespace Achievements
    {
        public class AchievementManager : IGameSystem
        {
            private static AchievementManager _instance;
            public static AchievementManager Instance => _instance ?? (_instance = new AchievementManager());

            public void Initialize()
            {
                Debug.Log("[AchievementManager] Initializing...");
            }

            public void LoadAchievements()
            {
                // Load achievement definitions
            }

            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }
    }

    #endregion

    #region Economy System Adapter

    namespace Economy
    {
        public class EconomyManager : IGameSystem
        {
            private static EconomyManager _instance;
            public static EconomyManager Instance => _instance ?? (_instance = new EconomyManager());

            public void Initialize()
            {
                Debug.Log("[EconomyManager] Initializing...");
            }

            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }
    }

    #endregion

    #region Tutorial System Adapter

    namespace Tutorial
    {
        public class TutorialManager : IGameSystem
        {
            private static TutorialManager _instance;
            public static TutorialManager Instance => _instance ?? (_instance = new TutorialManager());

            public void Initialize()
            {
                Debug.Log("[TutorialManager] Initializing...");
            }

            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }
    }

    #endregion

    #region Audio System Adapter

    namespace Audio
    {
        public class AudioManager : IGameSystem
        {
            private static AudioManager _instance;
            public static AudioManager Instance => _instance ?? (_instance = new AudioManager());

            public void Initialize()
            {
                Debug.Log("[AudioManager] Initializing...");
            }

            public void LoadAudioLibrary()
            {
                // Load all audio clips
            }

            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }
    }

    #endregion

    #region Particle System Adapter

    namespace Particles
    {
        public class ParticleManager : IGameSystem
        {
            private static ParticleManager _instance;
            public static ParticleManager Instance => _instance ?? (_instance = new ParticleManager());

            public void Initialize()
            {
                Debug.Log("[ParticleManager] Initializing...");
            }

            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }

        public class ParticleFactory : IGameSystem
        {
            private static ParticleFactory _instance;
            public static ParticleFactory Instance => _instance ?? (_instance = new ParticleFactory());

            public void Initialize() { }
            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }
    }

    #endregion

    #region Visual Systems Adapter

    namespace Visual
    {
        public class PostProcessManager : IGameSystem
        {
            private static PostProcessManager _instance;
            public static PostProcessManager Instance => _instance ?? (_instance = new PostProcessManager());

            public void Initialize()
            {
                Debug.Log("[PostProcessManager] Initializing...");
            }

            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }
    }

    namespace Projectiles
    {
        public class ProjectileManager : IGameSystem
        {
            private static ProjectileManager _instance;
            public static ProjectileManager Instance => _instance ?? (_instance = new ProjectileManager());

            public void Initialize() { }
            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }
    }

    namespace Procedural
    {
        public class ProceduralModelFactory : IGameSystem
        {
            private static ProceduralModelFactory _instance;
            public static ProceduralModelFactory Instance => _instance ?? (_instance = new ProceduralModelFactory());

            public void Initialize() { }
            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }

        public class TextureGenerator : IGameSystem
        {
            private static TextureGenerator _instance;
            public static TextureGenerator Instance => _instance ?? (_instance = new TextureGenerator());

            public void Initialize() { }
            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }
    }

    #endregion

    #region UI Systems Adapter

    namespace UI
    {
        public class UIManager : IGameSystem
        {
            private static UIManager _instance;
            public static UIManager Instance => _instance ?? (_instance = new UIManager());

            public void Initialize()
            {
                Debug.Log("[UIManager] Initializing...");
            }

            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }

        public class DamageNumberUIHelper : IGameSystem
        {
            private static DamageNumberUIHelper _instance;
            public static DamageNumberUIHelper Instance => _instance ?? (_instance = new DamageNumberUIHelper());

            public void Initialize() { }
            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }
    }

    namespace Notifications
    {
        public class NotificationManager : IGameSystem
        {
            private static NotificationManager _instance;
            public static NotificationManager Instance => _instance ?? (_instance = new NotificationManager());

            public void Initialize()
            {
                Debug.Log("[NotificationManager] Initializing...");
            }

            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }
    }

    #endregion

    #region Networking Adapters

    namespace Networking
    {
        public class NetworkManager : IGameSystem
        {
            private static NetworkManager _instance;
            public static NetworkManager Instance => _instance ?? (_instance = new NetworkManager());

            public void Initialize()
            {
                Debug.Log("[NetworkManager] Initializing...");
            }

            public void Disconnect() { }
            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }

        public class ClientManager : IGameSystem
        {
            public void Initialize() { }
            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }

        public class ServerHost : IGameSystem
        {
            public void Initialize() { }
            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }

        public class NetworkIdentity : IGameSystem
        {
            public void Initialize() { }
            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }

        public class PacketProcessor : IGameSystem
        {
            public void Initialize() { }
            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }
    }

    #endregion

    #region Analytics Adapters

    namespace Analytics
    {
        public class AnalyticsManager : IGameSystem
        {
            private static AnalyticsManager _instance;
            public static AnalyticsManager Instance => _instance ?? (_instance = new AnalyticsManager());

            public void Initialize()
            {
                Debug.Log("[AnalyticsManager] Initializing...");
            }

            public void TrackEvent(string eventName, System.Collections.Generic.Dictionary<string, object> data) { }
            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }

        public class PerformanceTracker : IGameSystem
        {
            private static PerformanceTracker _instance;
            public static PerformanceTracker Instance => _instance ?? (_instance = new PerformanceTracker());

            public void Initialize() { }
            public bool IsHealthy() { return true; }
            public void OnSceneLoaded(Scene scene) { }
            public void OnSceneUnloaded(Scene scene) { }
        }
    }

    #endregion
}