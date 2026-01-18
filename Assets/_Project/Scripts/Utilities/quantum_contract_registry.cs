using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace QuantumMechanic
{
    // ═══════════════════════════════════════════════════════════════════════════════════
    // CONTRACT ATTRIBUTES - Define what systems provide and require
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Declares that a system provides a specific capability (feature).
    /// Other systems can require this capability to ensure compatibility.
    /// 
    /// Example:
    /// [ProvidesCapability("EventDispatch", Version = 1, Description = "Basic event pub/sub")]
    /// public class EventSystem : BaseGameSystem { ... }
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ProvidesCapabilityAttribute : Attribute
    {
        /// <summary>Unique name of the capability (e.g., "EventDispatch", "SaveData")</summary>
        public string CapabilityName { get; }

        /// <summary>Version number (increment when making breaking changes)</summary>
        public int Version { get; set; } = 1;

        /// <summary>Human-readable description of what this capability does</summary>
        public string Description { get; set; }

        public ProvidesCapabilityAttribute(string capabilityName)
        {
            CapabilityName = capabilityName;
        }
    }

    /// <summary>
    /// Declares that a system needs a specific capability to work.
    /// The contract registry validates that all requirements are met.
    /// 
    /// Example:
    /// [RequiresCapability("EventDispatch", MinVersion = 1)]
    /// public class QuestSystem : BaseGameSystem { ... }
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RequiresCapabilityAttribute : Attribute
    {
        /// <summary>Name of the required capability</summary>
        public string CapabilityName { get; }

        /// <summary>Minimum version needed (allows older systems to work with newer providers)</summary>
        public int MinVersion { get; set; } = 1;

        /// <summary>If true, system can still work without this capability</summary>
        public bool Optional { get; set; } = false;

        public RequiresCapabilityAttribute(string capabilityName)
        {
            CapabilityName = capabilityName;
        }
    }

    /// <summary>
    /// Declares that a system provides a hook (event callback) that others can subscribe to.
    /// Hooks let systems communicate without tight coupling.
    /// 
    /// Example:
    /// [ProvidesHook("OnEventPublished", typeof(Action<string, object>), Description = "Fires when event published")]
    /// public class EventSystem : BaseGameSystem { ... }
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ProvidesHookAttribute : Attribute
    {
        /// <summary>Name of the hook (e.g., "OnEventPublished", "OnQuestCompleted")</summary>
        public string HookName { get; }

        /// <summary>The delegate type for this hook (e.g., typeof(Action<string>))</summary>
        public Type DelegateType { get; }

        /// <summary>Description of when this hook fires</summary>
        public string Description { get; set; }

        public ProvidesHookAttribute(string hookName, Type delegateType)
        {
            HookName = hookName;
            DelegateType = delegateType;
        }
    }

    /// <summary>
    /// Declares that a system wants to subscribe to a hook.
    /// The registry can validate hook compatibility.
    /// 
    /// Example:
    /// [SubscribesToHook("OnEventPublished", Required = true)]
    /// public class AnalyticsSystem : BaseGameSystem { ... }
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class SubscribesToHookAttribute : Attribute
    {
        /// <summary>Name of the hook to subscribe to</summary>
        public string HookName { get; }

        /// <summary>If true, system fails to initialize if hook doesn't exist</summary>
        public bool Required { get; set; } = false;

        public SubscribesToHookAttribute(string hookName)
        {
            HookName = hookName;
        }
    }

    /// <summary>
    /// Marks a method as the implementation of a capability.
    /// Used for documentation and automatic API discovery.
    /// 
    /// Example:
    /// [CapabilityImplementation("EventDispatch")]
    /// public void Publish(string eventName, object data) { ... }
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CapabilityImplementationAttribute : Attribute
    {
        /// <summary>Name of the capability this method implements</summary>
        public string CapabilityName { get; }

        public CapabilityImplementationAttribute(string capabilityName)
        {
            CapabilityName = capabilityName;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // HOOK PROVIDER BASE CLASS - Systems that provide hooks inherit from this
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Helper class for systems that provide hooks.
    /// Since C# doesn't support multiple inheritance, systems must compose this
    /// rather than inherit from it.
    /// 
    /// Example:
    /// public class EventSystem : BaseGameSystem
    /// {
    ///     private HookManager hookManager = new HookManager();
    ///     
    ///     protected override async Awaitable OnInitialize()
    ///     {
    ///         hookManager.RegisterHook<Action<string, object>>("OnEventPublished");
    ///     }
    /// }
    /// </summary>
    public class HookManager
    {
        /// <summary>Dictionary of hook name → delegate instance</summary>
        private Dictionary<string, Delegate> hooks = new Dictionary<string, Delegate>();

        /// <summary>
        /// Registers a new hook that others can subscribe to.
        /// Call this in your OnInitialize() method.
        /// 
        /// Example:
        /// hookManager.RegisterHook<Action<string>>("OnPlayerJoined");
        /// </summary>
        public void RegisterHook<T>(string hookName) where T : Delegate
        {
            if (hooks.ContainsKey(hookName))
            {
                Debug.LogWarning($"[HookManager] Hook '{hookName}' already registered");
                return;
            }

            hooks[hookName] = null; // Will be set when subscribers attach
            Debug.Log($"[HookManager] Registered hook: {hookName}");
        }

        /// <summary>
        /// Allows external systems to subscribe to a hook.
        /// 
        /// Example:
        /// eventSystem.SubscribeToHook<Action<string>>("OnPlayerJoined", (name) => Debug.Log(name));
        /// </summary>
        public void SubscribeToHook<T>(string hookName, T handler) where T : Delegate
        {
            if (!hooks.ContainsKey(hookName))
            {
                Debug.LogWarning($"[HookManager] Hook '{hookName}' not found");
                return;
            }

            hooks[hookName] = Delegate.Combine(hooks[hookName], handler);
        }

        /// <summary>
        /// Unsubscribes from a hook.
        /// 
        /// Example:
        /// eventSystem.UnsubscribeFromHook<Action<string>>("OnPlayerJoined", myHandler);
        /// </summary>
        public void UnsubscribeFromHook<T>(string hookName, T handler) where T : Delegate
        {
            if (!hooks.ContainsKey(hookName))
            {
                Debug.LogWarning($"[HookManager] Hook '{hookName}' not found");
                return;
            }

            hooks[hookName] = Delegate.Remove(hooks[hookName], handler);
        }

        /// <summary>
        /// Invokes a hook, calling all subscribed handlers.
        /// 
        /// Example:
        /// hookManager.InvokeHook("OnPlayerJoined", playerName);
        /// </summary>
        public void InvokeHook(string hookName, params object[] args)
        {
            if (!hooks.ContainsKey(hookName))
            {
                Debug.LogWarning($"[HookManager] Hook '{hookName}' not found");
                return;
            }

            Delegate hook = hooks[hookName];
            if (hook != null)
            {
                try
                {
                    hook.DynamicInvoke(args);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[HookManager] Error invoking hook '{hookName}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets the current delegate for a hook (for advanced use cases).
        /// </summary>
        public Delegate GetHook(string hookName)
        {
            return hooks.ContainsKey(hookName) ? hooks[hookName] : null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // DATA STRUCTURES - How contract information is stored
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Represents a capability that a system provides.
    /// </summary>
    [Serializable]
    public class SystemCapability
    {
        public string CapabilityName;
        public int Version;
        public string Description;
        public Type ProviderType;
        public List<MethodInfo> ImplementationMethods = new List<MethodInfo>();

        public override string ToString()
        {
            return $"{CapabilityName} v{Version} (from {ProviderType.Name})";
        }
    }

    /// <summary>
    /// Represents a hook that a system provides.
    /// </summary>
    [Serializable]
    public class SystemHook
    {
        public string HookName;
        public Type DelegateType;
        public string Description;
        public Type ProviderType;

        public override string ToString()
        {
            return $"{HookName} ({DelegateType.Name}) from {ProviderType.Name}";
        }
    }

    /// <summary>
    /// Represents a requirement that a system has.
    /// </summary>
    [Serializable]
    public class SystemRequirement
    {
        public string RequirementName;
        public int MinVersion;
        public bool Optional;
        public Type RequiringType;

        public override string ToString()
        {
            return $"{RequiringType.Name} requires {RequirementName} v{MinVersion}+";
        }
    }

    /// <summary>
    /// Results of contract validation.
    /// </summary>
    [Serializable]
    public class ContractValidationResult
    {
        public bool IsValid;
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();

        public override string ToString()
        {
            if (IsValid)
            {
                return "All contracts valid ✓";
            }
            return $"Validation failed: {Errors.Count} errors, {Warnings.Count} warnings";
        }
    }

    /// <summary>
    /// Represents a breaking change detected between versions.
    /// </summary>
    [Serializable]
    public class BreakingChange
    {
        public string ChangeType; // "CapabilityRemoved", "VersionDowngrade", "HookRemoved", etc.
        public string Description;
        public string AffectedContract;

        public override string ToString()
        {
            return $"[{ChangeType}] {Description}";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // QUANTUM CONTRACT REGISTRY - Main registry class
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Central registry that tracks all capabilities, hooks, and requirements.
    /// Validates contracts and detects breaking changes.
    /// This prevents systems from breaking when you update them!
    /// </summary>
    public class QuantumContractRegistry : MonoBehaviour
    {
        // Singleton pattern
        private static QuantumContractRegistry instance;
        public static QuantumContractRegistry Instance => instance;

        // Contract storage
        private Dictionary<string, SystemCapability> capabilities = new Dictionary<string, SystemCapability>();
        private Dictionary<string, SystemHook> hooks = new Dictionary<string, SystemHook>();
        private List<SystemRequirement> requirements = new List<SystemRequirement>();

        // ═══════════════════════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════════════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void AutoInitialize()
        {
            if (instance == null)
            {
                GameObject registryObj = new GameObject("[QuantumContractRegistry]");
                instance = registryObj.AddComponent<QuantumContractRegistry>();
                DontDestroyOnLoad(registryObj);
                
                instance.BuildContractRegistry();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // REGISTRY BUILDING
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Scans all assemblies and builds the complete contract registry.
        /// This discovers all capabilities, hooks, and requirements in your project.
        /// </summary>
        public void BuildContractRegistry()
        {
            capabilities.Clear();
            hooks.Clear();
            requirements.Clear();

            Debug.Log("[ContractRegistry] Building contract registry...");

            // Scan all assemblies
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (Assembly asm in assemblies)
            {
                try
                {
                    Type[] types = asm.GetTypes();

                    foreach (Type t in types)
                    {
                        // Skip abstract classes
                        if (t.IsAbstract) continue;

                        // Process ProvidesCapability attributes
                        var capAttrs = t.GetCustomAttributes<ProvidesCapabilityAttribute>();
                        foreach (var attr in capAttrs)
                        {
                            RegisterCapability(t, attr);
                        }

                        // Process ProvidesHook attributes
                        var hookAttrs = t.GetCustomAttributes<ProvidesHookAttribute>();
                        foreach (var attr in hookAttrs)
                        {
                            RegisterHook(t, attr);
                        }

                        // Process RequiresCapability attributes
                        var reqAttrs = t.GetCustomAttributes<RequiresCapabilityAttribute>();
                        foreach (var attr in reqAttrs)
                        {
                            RegisterRequirement(t, attr);
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Debug.LogWarning($"[ContractRegistry] Could not load types from {asm.GetName().Name}: {ex.Message}");
                }
            }

            Debug.Log($"[ContractRegistry] Registry built: {capabilities.Count} capabilities, {hooks.Count} hooks, {requirements.Count} requirements");
        }

        private void RegisterCapability(Type providerType, ProvidesCapabilityAttribute attr)
        {
            if (capabilities.ContainsKey(attr.CapabilityName))
            {
                Debug.LogWarning($"[ContractRegistry] Capability '{attr.CapabilityName}' already registered by {capabilities[attr.CapabilityName].ProviderType.Name}. Overwriting with {providerType.Name}");
            }

            var cap = new SystemCapability
            {
                CapabilityName = attr.CapabilityName,
                Version = attr.Version,
                Description = attr.Description,
                ProviderType = providerType
            };

            // Find methods marked with [CapabilityImplementation]
            var methods = providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methods)
            {
                var implAttr = method.GetCustomAttribute<CapabilityImplementationAttribute>();
                if (implAttr != null && implAttr.CapabilityName == attr.CapabilityName)
                {
                    cap.ImplementationMethods.Add(method);
                }
            }

            capabilities[attr.CapabilityName] = cap;
            Debug.Log($"[ContractRegistry] Registered capability: {cap}");
        }

        private void RegisterHook(Type providerType, ProvidesHookAttribute attr)
        {
            if (hooks.ContainsKey(attr.HookName))
            {
                Debug.LogWarning($"[ContractRegistry] Hook '{attr.HookName}' already registered by {hooks[attr.HookName].ProviderType.Name}. Overwriting with {providerType.Name}");
            }

            var hook = new SystemHook
            {
                HookName = attr.HookName,
                DelegateType = attr.DelegateType,
                Description = attr.Description,
                ProviderType = providerType
            };

            hooks[attr.HookName] = hook;
            Debug.Log($"[ContractRegistry] Registered hook: {hook}");
        }

        private void RegisterRequirement(Type requiringType, RequiresCapabilityAttribute attr)
        {
            var req = new SystemRequirement
            {
                RequirementName = attr.CapabilityName,
                MinVersion = attr.MinVersion,
                Optional = attr.Optional,
                RequiringType = requiringType
            };

            requirements.Add(req);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // CONTRACT VALIDATION
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Validates that all requirements are satisfied by available capabilities.
        /// Returns a result object with any errors or warnings.
        /// </summary>
        public ContractValidationResult ValidateAllContracts()
        {
            var result = new ContractValidationResult { IsValid = true };

            foreach (var req in requirements)
            {
                // Check if capability exists
                if (!capabilities.ContainsKey(req.RequirementName))
                {
                    string msg = $"{req.RequiringType.Name} requires capability '{req.RequirementName}' which is not provided by any system";
                    
                    if (req.Optional)
                    {
                        result.Warnings.Add(msg);
                    }
                    else
                    {
                        result.Errors.Add(msg);
                        result.IsValid = false;
                    }
                    continue;
                }

                // Check version compatibility
                var cap = capabilities[req.RequirementName];
                if (cap.Version < req.MinVersion)
                {
                    string msg = $"{req.RequiringType.Name} requires {req.RequirementName} v{req.MinVersion}+, but only v{cap.Version} is available";
                    
                    if (req.Optional)
                    {
                        result.Warnings.Add(msg);
                    }
                    else
                    {
                        result.Errors.Add(msg);
                        result.IsValid = false;
                    }
                }
            }

            return result;
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // BREAKING CHANGE DETECTION
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Compares two versions of a system type and detects breaking changes.
        /// Useful for upgrade validation and migration planning.
        /// </summary>
        public List<BreakingChange> DetectBreakingChanges(Type oldType, Type newType)
        {
            var changes = new List<BreakingChange>();

            // Get old capabilities
            var oldCaps = oldType.GetCustomAttributes<ProvidesCapabilityAttribute>().ToList();
            var newCaps = newType.GetCustomAttributes<ProvidesCapabilityAttribute>().ToList();

            // Check for removed capabilities
            foreach (var oldCap in oldCaps)
            {
                var match = newCaps.FirstOrDefault(c => c.CapabilityName == oldCap.CapabilityName);
                if (match == null)
                {
                    changes.Add(new BreakingChange
                    {
                        ChangeType = "CapabilityRemoved",
                        Description = $"Capability '{oldCap.CapabilityName}' was removed",
                        AffectedContract = oldCap.CapabilityName
                    });
                }
                else if (match.Version < oldCap.Version)
                {
                    changes.Add(new BreakingChange
                    {
                        ChangeType = "VersionDowngrade",
                        Description = $"Capability '{oldCap.CapabilityName}' version downgraded from {oldCap.Version} to {match.Version}",
                        AffectedContract = oldCap.CapabilityName
                    });
                }
            }

            // Get old hooks
            var oldHooks = oldType.GetCustomAttributes<ProvidesHookAttribute>().ToList();
            var newHooks = newType.GetCustomAttributes<ProvidesHookAttribute>().ToList();

            // Check for removed hooks
            foreach (var oldHook in oldHooks)
            {
                var match = newHooks.FirstOrDefault(h => h.HookName == oldHook.HookName);
                if (match == null)
                {
                    changes.Add(new BreakingChange
                    {
                        ChangeType = "HookRemoved",
                        Description = $"Hook '{oldHook.HookName}' was removed",
                        AffectedContract = oldHook.HookName
                    });
                }
                else if (match.DelegateType != oldHook.DelegateType)
                {
                    changes.Add(new BreakingChange
                    {
                        ChangeType = "HookSignatureChanged",
                        Description = $"Hook '{oldHook.HookName}' signature changed from {oldHook.DelegateType.Name} to {match.DelegateType.Name}",
                        AffectedContract = oldHook.HookName
                    });
                }
            }

            return changes;
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // QUERY API
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Checks if a capability is available.
        /// </summary>
        public bool HasCapability(string capabilityName)
        {
            return capabilities.ContainsKey(capabilityName);
        }

        /// <summary>
        /// Gets information about a capability.
        /// </summary>
        public SystemCapability GetCapability(string capabilityName)
        {
            return capabilities.ContainsKey(capabilityName) ? capabilities[capabilityName] : null;
        }

        /// <summary>
        /// Gets information about a hook.
        /// </summary>
        public SystemHook GetHook(string hookName)
        {
            return hooks.ContainsKey(hookName) ? hooks[hookName] : null;
        }

        /// <summary>
        /// Gets all registered capabilities.
        /// </summary>
        public List<SystemCapability> GetAllCapabilities()
        {
            return new List<SystemCapability>(capabilities.Values);
        }

        /// <summary>
        /// Gets all registered hooks.
        /// </summary>
        public List<SystemHook> GetAllHooks()
        {
            return new List<SystemHook>(hooks.Values);
        }

        /// <summary>
        /// Gets all registered requirements.
        /// </summary>
        public List<SystemRequirement> GetAllRequirements()
        {
            return new List<SystemRequirement>(requirements);
        }
    }
}