// MODULE: Combat-03A
// FILE: ResourceSystem.cs
// DEPENDENCIES: NetworkIdentity.cs
// INTEGRATES WITH: Future AbilitySystem, UIManager, NetworkManager
// PURPOSE: Server-authoritative resource management (health, mana, energy, stamina) with consumption, regeneration, and events

using System.Collections.Generic;
using UnityEngine;

namespace QuantumMechanic.Combat
{
    /// <summary>
    /// Manages character resources (health, mana, energy, stamina) with consumption,
    /// regeneration, and server-authoritative tracking for multiplayer games.
    /// </summary>
    public class ResourceSystem : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("References")]
        [Tooltip("NetworkIdentity component for this entity")]
        public NetworkIdentity NetworkIdentity;
        
        [Header("Resource Configuration")]
        [Tooltip("Maximum health points")]
        public float MaxHealth = 100f;
        
        [Tooltip("Maximum mana points")]
        public float MaxMana = 100f;
        
        [Tooltip("Maximum energy points")]
        public float MaxEnergy = 100f;
        
        [Tooltip("Maximum stamina points")]
        public float MaxStamina = 100f;
        
        [Header("Regeneration Rates")]
        [Tooltip("Health regeneration per second")]
        public float HealthRegenPerSecond = 1f;
        
        [Tooltip("Mana regeneration per second")]
        public float ManaRegenPerSecond = 5f;
        
        [Tooltip("Energy regeneration per second")]
        public float EnergyRegenPerSecond = 10f;
        
        [Tooltip("Stamina regeneration per second")]
        public float StaminaRegenPerSecond = 15f;
        
        [Header("Settings")]
        [Tooltip("Enable automatic resource regeneration")]
        public bool EnableAutoRegen = true;
        
        [Tooltip("Delay before regeneration starts after consumption (seconds)")]
        public float RegenDelay = 2f;
        
        #endregion
        
        #region Private Fields
        
        private Dictionary<ResourceType, ResourcePool> resources = new Dictionary<ResourceType, ResourcePool>();
        private Dictionary<ResourceType, float> lastConsumptionTime = new Dictionary<ResourceType, float>();
        
        // Server-side authoritative tracking
        private static Dictionary<NetworkIdentity, Dictionary<ResourceType, ResourcePool>> serverResources 
            = new Dictionary<NetworkIdentity, Dictionary<ResourceType, ResourcePool>>();
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when a resource value changes. Parameters: (ResourceType, currentValue, maxValue)
        /// </summary>
        public event System.Action<ResourceType, float, float> OnResourceChanged;
        
        /// <summary>
        /// Fired when a resource reaches zero. Parameters: (ResourceType)
        /// </summary>
        public event System.Action<ResourceType> OnResourceDepleted;
        
        /// <summary>
        /// Fired when a resource reaches maximum. Parameters: (ResourceType)
        /// </summary>
        public event System.Action<ResourceType> OnResourceFull;
        
        /// <summary>
        /// Fired when resource consumption fails due to insufficient amount. Parameters: (ResourceType, required, available)
        /// </summary>
        public event System.Action<ResourceType, float, float> OnInsufficientResource;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            if (NetworkIdentity == null)
            {
                NetworkIdentity = GetComponent<NetworkIdentity>();
            }
            
            InitializeResources();
        }
        
        private void Update()
        {
            if (EnableAutoRegen)
            {
                RegenerateResources(Time.deltaTime);
            }
        }
        
        private void OnDestroy()
        {
            // Clean up server tracking
            if (NetworkIdentity != null && serverResources.ContainsKey(NetworkIdentity))
            {
                serverResources.Remove(NetworkIdentity);
            }
        }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initializes all resource pools with configured max values.
        /// </summary>
        private void InitializeResources()
        {
            resources.Clear();
            lastConsumptionTime.Clear();
            
            // Create resource pools
            resources[ResourceType.Health] = new ResourcePool(ResourceType.Health, MaxHealth, MaxHealth, HealthRegenPerSecond);
            resources[ResourceType.Mana] = new ResourcePool(ResourceType.Mana, MaxMana, MaxMana, ManaRegenPerSecond);
            resources[ResourceType.Energy] = new ResourcePool(ResourceType.Energy, MaxEnergy, MaxEnergy, EnergyRegenPerSecond);
            resources[ResourceType.Stamina] = new ResourcePool(ResourceType.Stamina, MaxStamina, MaxStamina, StaminaRegenPerSecond);
            
            // Initialize last consumption times
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                lastConsumptionTime[type] = -RegenDelay;
            }
            
            // Initialize server tracking if this is server authority
            if (NetworkIdentity != null && NetworkIdentity.IsServer)
            {
                InitializeServerResources();
            }
        }
        
        /// <summary>
        /// Initializes server-side resource tracking for this entity.
        /// </summary>
        private void InitializeServerResources()
        {
            if (!serverResources.ContainsKey(NetworkIdentity))
            {
                serverResources[NetworkIdentity] = new Dictionary<ResourceType, ResourcePool>();
            }
            
            foreach (var kvp in resources)
            {
                serverResources[NetworkIdentity][kvp.Key] = new ResourcePool(
                    kvp.Value.Type,
                    kvp.Value.Current,
                    kvp.Value.Max,
                    kvp.Value.RegenPerSecond
                );
            }
        }
        
        #endregion
        
        #region Public API - Resource Access
        
        /// <summary>
        /// Gets the resource pool for the specified type.
        /// </summary>
        /// <param name="type">The resource type to retrieve</param>
        /// <returns>The resource pool, or null if not found</returns>
        public ResourcePool GetResource(ResourceType type)
        {
            return resources.ContainsKey(type) ? resources[type] : null;
        }
        
        /// <summary>
        /// Gets the current value of a resource.
        /// </summary>
        /// <param name="type">The resource type</param>
        /// <returns>Current resource value</returns>
        public float GetResourceCurrent(ResourceType type)
        {
            return resources.ContainsKey(type) ? resources[type].Current : 0f;
        }
        
        /// <summary>
        /// Gets the maximum value of a resource.
        /// </summary>
        /// <param name="type">The resource type</param>
        /// <returns>Maximum resource value</returns>
        public float GetResourceMax(ResourceType type)
        {
            return resources.ContainsKey(type) ? resources[type].Max : 0f;
        }
        
        /// <summary>
        /// Gets the percentage (0-1) of a resource.
        /// </summary>
        /// <param name="type">The resource type</param>
        /// <returns>Resource percentage (0-1)</returns>
        public float GetResourcePercentage(ResourceType type)
        {
            return resources.ContainsKey(type) ? resources[type].Percentage : 0f;
        }
        
        /// <summary>
        /// Checks if the entity has at least the specified amount of a resource.
        /// </summary>
        /// <param name="type">The resource type</param>
        /// <param name="amount">The amount to check for</param>
        /// <returns>True if sufficient resource is available</returns>
        public bool HasResource(ResourceType type, float amount)
        {
            if (!resources.ContainsKey(type))
            {
                return false;
            }
            
            return resources[type].Current >= amount;
        }
        
        #endregion
        
        #region Public API - Resource Modification
        
        /// <summary>
        /// Sets the current and maximum values for a resource.
        /// </summary>
        /// <param name="type">The resource type</param>
        /// <param name="current">The current value</param>
        /// <param name="max">The maximum value</param>
        public void SetResource(ResourceType type, float current, float max)
        {
            if (!resources.ContainsKey(type))
            {
                return;
            }
            
            ResourcePool pool = resources[type];
            pool.Max = Mathf.Max(0f, max);
            pool.Current = Mathf.Clamp(current, 0f, pool.Max);
            
            // Update server tracking
            if (NetworkIdentity != null && NetworkIdentity.IsServer)
            {
                UpdateServerResource(type, pool);
            }
            
            OnResourceChanged?.Invoke(type, pool.Current, pool.Max);
            
            CheckResourceThresholds(type, pool);
        }
        
        /// <summary>
        /// Modifies a resource by a delta amount (positive to add, negative to subtract).
        /// </summary>
        /// <param name="type">The resource type</param>
        /// <param name="amount">The amount to add (positive) or subtract (negative)</param>
        /// <returns>The actual amount modified (may be less than requested due to clamping)</returns>
        public float ModifyResource(ResourceType type, float amount)
        {
            if (!resources.ContainsKey(type))
            {
                return 0f;
            }
            
            ResourcePool pool = resources[type];
            float oldValue = pool.Current;
            float newValue = Mathf.Clamp(pool.Current + amount, 0f, pool.Max);
            pool.Current = newValue;
            
            float actualChange = newValue - oldValue;
            
            // Track consumption for regen delay
            if (actualChange < 0f)
            {
                lastConsumptionTime[type] = Time.time;
            }
            
            // Update server tracking
            if (NetworkIdentity != null && NetworkIdentity.IsServer)
            {
                UpdateServerResource(type, pool);
            }
            
            if (Mathf.Abs(actualChange) > 0.001f)
            {
                OnResourceChanged?.Invoke(type, pool.Current, pool.Max);
                CheckResourceThresholds(type, pool);
            }
            
            return actualChange;
        }
        
        /// <summary>
        /// Attempts to consume the specified amount of a resource.
        /// </summary>
        /// <param name="type">The resource type</param>
        /// <param name="amount">The amount to consume</param>
        /// <returns>True if the resource was consumed, false if insufficient</returns>
        public bool ConsumeResource(ResourceType type, float amount)
        {
            if (!resources.ContainsKey(type))
            {
                return false;
            }
            
            ResourcePool pool = resources[type];
            
            if (pool.Current < amount)
            {
                OnInsufficientResource?.Invoke(type, amount, pool.Current);
                return false;
            }
            
            float actualChange = ModifyResource(type, -amount);
            return Mathf.Abs(actualChange) >= amount - 0.001f;
        }
        
        /// <summary>
        /// Restores the specified amount of a resource.
        /// </summary>
        /// <param name="type">The resource type</param>
        /// <param name="amount">The amount to restore</param>
        /// <returns>The actual amount restored</returns>
        public float RestoreResource(ResourceType type, float amount)
        {
            return ModifyResource(type, amount);
        }
        
        /// <summary>
        /// Sets a resource to its maximum value.
        /// </summary>
        /// <param name="type">The resource type</param>
        public void FillResource(ResourceType type)
        {
            if (resources.ContainsKey(type))
            {
                ResourcePool pool = resources[type];
                SetResource(type, pool.Max, pool.Max);
            }
        }
        
        /// <summary>
        /// Sets all resources to their maximum values.
        /// </summary>
        public void FillAllResources()
        {
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                FillResource(type);
            }
        }
        
        #endregion
        
        #region Regeneration
        
        /// <summary>
        /// Regenerates all resources over time based on their regen rates.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last regeneration</param>
        public void RegenerateResources(float deltaTime)
        {
            foreach (var kvp in resources)
            {
                ResourceType type = kvp.Key;
                ResourcePool pool = kvp.Value;
                
                // Check if regen delay has passed
                if (Time.time - lastConsumptionTime[type] < RegenDelay)
                {
                    continue;
                }
                
                // Skip if already full
                if (pool.Current >= pool.Max - 0.001f)
                {
                    continue;
                }
                
                // Calculate regen amount
                float regenAmount = pool.RegenPerSecond * deltaTime;
                float oldValue = pool.Current;
                pool.Current = Mathf.Min(pool.Current + regenAmount, pool.Max);
                
                float actualRegen = pool.Current - oldValue;
                
                // Update server tracking
                if (NetworkIdentity != null && NetworkIdentity.IsServer)
                {
                    UpdateServerResource(type, pool);
                }
                
                if (actualRegen > 0.001f)
                {
                    OnResourceChanged?.Invoke(type, pool.Current, pool.Max);
                    CheckResourceThresholds(type, pool);
                }
            }
        }
        
        /// <summary>
        /// Sets the regeneration rate for a specific resource type.
        /// </summary>
        /// <param name="type">The resource type</param>
        /// <param name="regenPerSecond">The new regeneration rate per second</param>
        public void SetRegenRate(ResourceType type, float regenPerSecond)
        {
            if (resources.ContainsKey(type))
            {
                resources[type].RegenPerSecond = Mathf.Max(0f, regenPerSecond);
                
                // Update server tracking
                if (NetworkIdentity != null && NetworkIdentity.IsServer && serverResources.ContainsKey(NetworkIdentity))
                {
                    serverResources[NetworkIdentity][type].RegenPerSecond = resources[type].RegenPerSecond;
                }
            }
        }
        
        #endregion
        
        #region Server Validation
        
        /// <summary>
        /// Validates a resource consumption request on the server.
        /// </summary>
        /// <param name="identity">The entity attempting to consume</param>
        /// <param name="type">The resource type</param>
        /// <param name="amount">The amount to consume</param>
        /// <returns>True if the request is valid</returns>
        public static bool ValidateResourceConsumption(NetworkIdentity identity, ResourceType type, float amount)
        {
            if (identity == null || !serverResources.ContainsKey(identity))
            {
                return false;
            }
            
            if (!serverResources[identity].ContainsKey(type))
            {
                return false;
            }
            
            return serverResources[identity][type].Current >= amount;
        }
        
        /// <summary>
        /// Applies resource consumption on the server (authoritative).
        /// </summary>
        /// <param name="identity">The entity consuming resources</param>
        /// <param name="type">The resource type</param>
        /// <param name="amount">The amount to consume</param>
        /// <returns>True if consumption was successful</returns>
        public static bool ServerConsumeResource(NetworkIdentity identity, ResourceType type, float amount)
        {
            if (!ValidateResourceConsumption(identity, type, amount))
            {
                return false;
            }
            
            ResourcePool pool = serverResources[identity][type];
            pool.Current -= amount;
            pool.Current = Mathf.Max(0f, pool.Current);
            
            return true;
        }
        
        /// <summary>
        /// Gets the server's authoritative resource value for an entity.
        /// </summary>
        /// <param name="identity">The entity to check</param>
        /// <param name="type">The resource type</param>
        /// <returns>The server's tracked resource value, or -1 if not found</returns>
        public static float GetServerResourceValue(NetworkIdentity identity, ResourceType type)
        {
            if (identity == null || !serverResources.ContainsKey(identity))
            {
                return -1f;
            }
            
            if (!serverResources[identity].ContainsKey(type))
            {
                return -1f;
            }
            
            return serverResources[identity][type].Current;
        }
        
        /// <summary>
        /// Updates server-side tracking for a resource.
        /// </summary>
        private void UpdateServerResource(ResourceType type, ResourcePool pool)
        {
            if (serverResources.ContainsKey(NetworkIdentity))
            {
                if (!serverResources[NetworkIdentity].ContainsKey(type))
                {
                    serverResources[NetworkIdentity][type] = new ResourcePool(pool.Type, pool.Current, pool.Max, pool.RegenPerSecond);
                }
                else
                {
                    serverResources[NetworkIdentity][type].Current = pool.Current;
                    serverResources[NetworkIdentity][type].Max = pool.Max;
                }
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Checks if a resource has crossed threshold boundaries (empty/full).
        /// </summary>
        private void CheckResourceThresholds(ResourceType type, ResourcePool pool)
        {
            if (pool.Current <= 0.001f)
            {
                OnResourceDepleted?.Invoke(type);
            }
            else if (pool.Current >= pool.Max - 0.001f)
            {
                OnResourceFull?.Invoke(type);
            }
        }
        
        #endregion
    }
    
    #region Data Structures
    
    /// <summary>
    /// Enumeration of available resource types.
    /// </summary>
    public enum ResourceType
    {
        Health,
        Mana,
        Energy,
        Stamina
    }
    
    /// <summary>
    /// Represents a pool of a specific resource with current/max values and regeneration.
    /// </summary>
    [System.Serializable]
    public class ResourcePool
    {
        public ResourceType Type;
        public float Current;
        public float Max;
        public float RegenPerSecond;
        
        /// <summary>
        /// Gets the resource as a percentage (0-1).
        /// </summary>
        public float Percentage => Max > 0f ? Mathf.Clamp01(Current / Max) : 0f;
        
        /// <summary>
        /// Gets whether the resource is depleted.
        /// </summary>
        public bool IsDepleted => Current <= 0.001f;
        
        /// <summary>
        /// Gets whether the resource is full.
        /// </summary>
        public bool IsFull => Current >= Max - 0.001f;
        
        public ResourcePool(ResourceType type, float current, float max, float regenPerSecond)
        {
            Type = type;
            Current = current;
            Max = max;
            RegenPerSecond = regenPerSecond;
        }
    }
    
    #endregion
}

/*
USAGE EXAMPLE:

// 1. Attach ResourceSystem to player GameObject
GameObject player = new GameObject("Player");
player.AddComponent<NetworkIdentity>();
ResourceSystem resourceSystem = player.AddComponent<ResourceSystem>();

// 2. Configure resources in Inspector or code
resourceSystem.MaxHealth = 100f;
resourceSystem.MaxMana = 150f;
resourceSystem.HealthRegenPerSecond = 2f;
resourceSystem.ManaRegenPerSecond = 10f;

// 3. Subscribe to events
resourceSystem.OnResourceChanged += (type, current, max) =>
{
    Debug.Log($"{type}: {current}/{max}");
};

resourceSystem.OnResourceDepleted += (type) =>
{
    if (type == ResourceType.Health)
    {
        Debug.Log("Player died!");
    }
};

resourceSystem.OnInsufficientResource += (type, required, available) =>
{
    Debug.Log($"Not enough {type}! Need {required}, have {available}");
};

// 4. Consume resources (e.g., casting ability)
if (resourceSystem.HasResource(ResourceType.Mana, 25f))
{
    bool success = resourceSystem.ConsumeResource(ResourceType.Mana, 25f);
    if (success)
    {
        Debug.Log("Ability cast!");
    }
}

// 5. Restore resources (e.g., healing potion)
float restored = resourceSystem.RestoreResource(ResourceType.Health, 50f);
Debug.Log($"Restored {restored} health");

// 6. Server validation (for multiplayer)
bool valid = ResourceSystem.ValidateResourceConsumption(playerIdentity, ResourceType.Stamina, 20f);
if (valid)
{
    ResourceSystem.ServerConsumeResource(playerIdentity, ResourceType.Stamina, 20f);
}

// 7. Check resource status
float healthPercent = resourceSystem.GetResourcePercentage(ResourceType.Health);
if (healthPercent < 0.2f)
{
    Debug.Log("Low health warning!");
}

INTEGRATION NOTES:
- Attach to any GameObject with NetworkIdentity
- Automatic regeneration runs in Update()
- Server-authoritative tracking prevents cheating
- Events allow UI integration (health bars, mana bars)
- Future AbilitySystem will call ConsumeResource() for ability costs
- Future DamageSystem will call ModifyResource(Health, -damage)
*/
