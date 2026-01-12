// MODULE: Combat-03B
// FILE: CooldownSystem.cs
// DEPENDENCIES: NetworkIdentity.cs
// INTEGRATES WITH: Future AbilitySystem, UIManager, NetworkManager
// PURPOSE: Server-authoritative cooldown management for abilities with reduction modifiers and group cooldowns

using System.Collections.Generic;
using UnityEngine;

namespace QuantumMechanic.Combat
{
    /// <summary>
    /// Manages ability cooldowns with server-authoritative tracking, cooldown reduction modifiers,
    /// and support for group cooldowns (shared CD across multiple abilities).
    /// </summary>
    public class CooldownSystem : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("References")]
        [Tooltip("NetworkIdentity component for this entity")]
        public NetworkIdentity NetworkIdentity;
        
        [Header("Settings")]
        [Tooltip("Global cooldown reduction multiplier (0.2 = 20% faster cooldowns)")]
        [Range(0f, 0.9f)]
        public float GlobalCooldownReduction = 0f;
        
        [Tooltip("Enable debug logging for cooldown events")]
        public bool DebugMode = false;
        
        #endregion
        
        #region Private Fields
        
        // Local cooldown tracking
        private Dictionary<string, CooldownInstance> activeCooldowns = new Dictionary<string, CooldownInstance>();
        private Dictionary<string, CooldownGroup> cooldownGroups = new Dictionary<string, CooldownGroup>();
        
        // Server-side authoritative tracking
        private static Dictionary<NetworkIdentity, Dictionary<string, CooldownInstance>> serverCooldowns 
            = new Dictionary<NetworkIdentity, Dictionary<string, CooldownInstance>>();
        
        private static Dictionary<NetworkIdentity, Dictionary<string, CooldownGroup>> serverGroups 
            = new Dictionary<NetworkIdentity, Dictionary<string, CooldownGroup>>();
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when a cooldown is started. Parameters: (abilityId, duration)
        /// </summary>
        public event System.Action<string, float> OnCooldownStarted;
        
        /// <summary>
        /// Fired when a cooldown finishes. Parameters: (abilityId)
        /// </summary>
        public event System.Action<string> OnCooldownFinished;
        
        /// <summary>
        /// Fired when attempting to use an ability that's on cooldown. Parameters: (abilityId, remainingTime)
        /// </summary>
        public event System.Action<string, float> OnCooldownBlocked;
        
        /// <summary>
        /// Fired when a cooldown is reduced by a modifier. Parameters: (abilityId, oldRemaining, newRemaining)
        /// </summary>
        public event System.Action<string, float, float> OnCooldownReduced;
        
        /// <summary>
        /// Fired when a group cooldown is triggered. Parameters: (groupId, duration)
        /// </summary>
        public event System.Action<string, float> OnGroupCooldownTriggered;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            if (NetworkIdentity == null)
            {
                NetworkIdentity = GetComponent<NetworkIdentity>();
            }
            
            InitializeServerTracking();
        }
        
        private void Update()
        {
            UpdateCooldowns(Time.deltaTime);
        }
        
        private void OnDestroy()
        {
            // Clean up server tracking
            if (NetworkIdentity != null)
            {
                if (serverCooldowns.ContainsKey(NetworkIdentity))
                {
                    serverCooldowns.Remove(NetworkIdentity);
                }
                if (serverGroups.ContainsKey(NetworkIdentity))
                {
                    serverGroups.Remove(NetworkIdentity);
                }
            }
        }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initializes server-side cooldown tracking for this entity.
        /// </summary>
        private void InitializeServerTracking()
        {
            if (NetworkIdentity != null && NetworkIdentity.IsServer)
            {
                if (!serverCooldowns.ContainsKey(NetworkIdentity))
                {
                    serverCooldowns[NetworkIdentity] = new Dictionary<string, CooldownInstance>();
                }
                if (!serverGroups.ContainsKey(NetworkIdentity))
                {
                    serverGroups[NetworkIdentity] = new Dictionary<string, CooldownGroup>();
                }
            }
        }
        
        #endregion
        
        #region Public API - Cooldown Management
        
        /// <summary>
        /// Starts a cooldown for the specified ability.
        /// </summary>
        /// <param name="abilityId">Unique identifier for the ability</param>
        /// <param name="baseDuration">Base cooldown duration in seconds</param>
        /// <param name="groupId">Optional group ID for shared cooldowns</param>
        public void StartCooldown(string abilityId, float baseDuration, string groupId = null)
        {
            if (string.IsNullOrEmpty(abilityId) || baseDuration <= 0f)
            {
                return;
            }
            
            // Apply cooldown reduction
            float finalDuration = CalculateFinalCooldown(baseDuration);
            
            // Create or update cooldown instance
            if (!activeCooldowns.ContainsKey(abilityId))
            {
                activeCooldowns[abilityId] = new CooldownInstance(abilityId, finalDuration, groupId);
            }
            else
            {
                activeCooldowns[abilityId].RemainingTime = finalDuration;
                activeCooldowns[abilityId].TotalDuration = finalDuration;
            }
            
            // Handle group cooldown
            if (!string.IsNullOrEmpty(groupId))
            {
                StartGroupCooldown(groupId, finalDuration);
            }
            
            // Update server tracking
            if (NetworkIdentity != null && NetworkIdentity.IsServer)
            {
                UpdateServerCooldown(abilityId, finalDuration, groupId);
            }
            
            OnCooldownStarted?.Invoke(abilityId, finalDuration);
            
            if (DebugMode)
            {
                Debug.Log($"[CooldownSystem] Started cooldown for {abilityId}: {finalDuration:F1}s");
            }
        }
        
        /// <summary>
        /// Checks if an ability is currently on cooldown.
        /// </summary>
        /// <param name="abilityId">The ability identifier</param>
        /// <returns>True if the ability is on cooldown</returns>
        public bool IsOnCooldown(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId))
            {
                return false;
            }
            
            if (activeCooldowns.ContainsKey(abilityId))
            {
                return activeCooldowns[abilityId].RemainingTime > 0f;
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets the remaining cooldown time for an ability.
        /// </summary>
        /// <param name="abilityId">The ability identifier</param>
        /// <returns>Remaining cooldown time in seconds, or 0 if not on cooldown</returns>
        public float GetRemainingCooldown(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId) || !activeCooldowns.ContainsKey(abilityId))
            {
                return 0f;
            }
            
            return Mathf.Max(0f, activeCooldowns[abilityId].RemainingTime);
        }
        
        /// <summary>
        /// Gets the cooldown percentage (0-1, where 1 is ready).
        /// </summary>
        /// <param name="abilityId">The ability identifier</param>
        /// <returns>Cooldown percentage (0 = just started, 1 = ready)</returns>
        public float GetCooldownPercentage(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId) || !activeCooldowns.ContainsKey(abilityId))
            {
                return 1f;
            }
            
            CooldownInstance cd = activeCooldowns[abilityId];
            if (cd.TotalDuration <= 0f)
            {
                return 1f;
            }
            
            float elapsed = cd.TotalDuration - cd.RemainingTime;
            return Mathf.Clamp01(elapsed / cd.TotalDuration);
        }
        
        /// <summary>
        /// Attempts to use an ability, checking if it's off cooldown.
        /// </summary>
        /// <param name="abilityId">The ability identifier</param>
        /// <returns>True if the ability can be used (not on cooldown)</returns>
        public bool TryUseAbility(string abilityId)
        {
            if (IsOnCooldown(abilityId))
            {
                float remaining = GetRemainingCooldown(abilityId);
                OnCooldownBlocked?.Invoke(abilityId, remaining);
                
                if (DebugMode)
                {
                    Debug.Log($"[CooldownSystem] Ability {abilityId} blocked: {remaining:F1}s remaining");
                }
                
                return false;
            }
            
            // Check group cooldown
            if (activeCooldowns.ContainsKey(abilityId) && !string.IsNullOrEmpty(activeCooldowns[abilityId].GroupId))
            {
                string groupId = activeCooldowns[abilityId].GroupId;
                if (IsGroupOnCooldown(groupId))
                {
                    float remaining = GetGroupRemainingCooldown(groupId);
                    OnCooldownBlocked?.Invoke(abilityId, remaining);
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Resets the cooldown for a specific ability (makes it immediately available).
        /// </summary>
        /// <param name="abilityId">The ability identifier</param>
        public void ResetCooldown(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId))
            {
                return;
            }
            
            if (activeCooldowns.ContainsKey(abilityId))
            {
                activeCooldowns[abilityId].RemainingTime = 0f;
                OnCooldownFinished?.Invoke(abilityId);
                
                if (DebugMode)
                {
                    Debug.Log($"[CooldownSystem] Reset cooldown for {abilityId}");
                }
            }
            
            // Update server tracking
            if (NetworkIdentity != null && NetworkIdentity.IsServer && serverCooldowns.ContainsKey(NetworkIdentity))
            {
                if (serverCooldowns[NetworkIdentity].ContainsKey(abilityId))
                {
                    serverCooldowns[NetworkIdentity][abilityId].RemainingTime = 0f;
                }
            }
        }
        
        /// <summary>
        /// Resets all cooldowns for this entity.
        /// </summary>
        public void ResetAllCooldowns()
        {
            List<string> abilityIds = new List<string>(activeCooldowns.Keys);
            foreach (string abilityId in abilityIds)
            {
                ResetCooldown(abilityId);
            }
            
            // Reset group cooldowns
            List<string> groupIds = new List<string>(cooldownGroups.Keys);
            foreach (string groupId in groupIds)
            {
                ResetGroupCooldown(groupId);
            }
        }
        
        /// <summary>
        /// Reduces the remaining cooldown time for an ability by a fixed amount.
        /// </summary>
        /// <param name="abilityId">The ability identifier</param>
        /// <param name="reduction">Amount of time to reduce (seconds)</param>
        public void ReduceCooldown(string abilityId, float reduction)
        {
            if (string.IsNullOrEmpty(abilityId) || reduction <= 0f)
            {
                return;
            }
            
            if (!activeCooldowns.ContainsKey(abilityId))
            {
                return;
            }
            
            CooldownInstance cd = activeCooldowns[abilityId];
            float oldRemaining = cd.RemainingTime;
            cd.RemainingTime = Mathf.Max(0f, cd.RemainingTime - reduction);
            float newRemaining = cd.RemainingTime;
            
            if (Mathf.Abs(oldRemaining - newRemaining) > 0.001f)
            {
                OnCooldownReduced?.Invoke(abilityId, oldRemaining, newRemaining);
                
                if (newRemaining <= 0f)
                {
                    OnCooldownFinished?.Invoke(abilityId);
                }
                
                if (DebugMode)
                {
                    Debug.Log($"[CooldownSystem] Reduced {abilityId} cooldown: {oldRemaining:F1}s → {newRemaining:F1}s");
                }
            }
        }
        
        #endregion
        
        #region Group Cooldowns
        
        /// <summary>
        /// Starts a group cooldown that affects multiple abilities.
        /// </summary>
        /// <param name="groupId">Unique identifier for the cooldown group</param>
        /// <param name="duration">Duration in seconds</param>
        private void StartGroupCooldown(string groupId, float duration)
        {
            if (string.IsNullOrEmpty(groupId) || duration <= 0f)
            {
                return;
            }
            
            if (!cooldownGroups.ContainsKey(groupId))
            {
                cooldownGroups[groupId] = new CooldownGroup(groupId, duration);
            }
            else
            {
                cooldownGroups[groupId].RemainingTime = duration;
                cooldownGroups[groupId].TotalDuration = duration;
            }
            
            // Update server tracking
            if (NetworkIdentity != null && NetworkIdentity.IsServer)
            {
                UpdateServerGroupCooldown(groupId, duration);
            }
            
            OnGroupCooldownTriggered?.Invoke(groupId, duration);
        }
        
        /// <summary>
        /// Checks if a cooldown group is active.
        /// </summary>
        /// <param name="groupId">The group identifier</param>
        /// <returns>True if the group is on cooldown</returns>
        public bool IsGroupOnCooldown(string groupId)
        {
            if (string.IsNullOrEmpty(groupId) || !cooldownGroups.ContainsKey(groupId))
            {
                return false;
            }
            
            return cooldownGroups[groupId].RemainingTime > 0f;
        }
        
        /// <summary>
        /// Gets the remaining time for a group cooldown.
        /// </summary>
        /// <param name="groupId">The group identifier</param>
        /// <returns>Remaining time in seconds, or 0 if not on cooldown</returns>
        public float GetGroupRemainingCooldown(string groupId)
        {
            if (string.IsNullOrEmpty(groupId) || !cooldownGroups.ContainsKey(groupId))
            {
                return 0f;
            }
            
            return Mathf.Max(0f, cooldownGroups[groupId].RemainingTime);
        }
        
        /// <summary>
        /// Resets a group cooldown.
        /// </summary>
        /// <param name="groupId">The group identifier</param>
        public void ResetGroupCooldown(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                return;
            }
            
            if (cooldownGroups.ContainsKey(groupId))
            {
                cooldownGroups[groupId].RemainingTime = 0f;
            }
            
            // Update server tracking
            if (NetworkIdentity != null && NetworkIdentity.IsServer && serverGroups.ContainsKey(NetworkIdentity))
            {
                if (serverGroups[NetworkIdentity].ContainsKey(groupId))
                {
                    serverGroups[NetworkIdentity][groupId].RemainingTime = 0f;
                }
            }
        }
        
        #endregion
        
        #region Cooldown Reduction
        
        /// <summary>
        /// Calculates the final cooldown duration after applying reduction modifiers.
        /// </summary>
        /// <param name="baseDuration">The base cooldown duration</param>
        /// <returns>Final cooldown duration with reductions applied</returns>
        private float CalculateFinalCooldown(float baseDuration)
        {
            float reduction = Mathf.Clamp01(GlobalCooldownReduction);
            return baseDuration * (1f - reduction);
        }
        
        /// <summary>
        /// Sets the global cooldown reduction modifier.
        /// </summary>
        /// <param name="reduction">Reduction percentage (0-0.9, where 0.2 = 20% faster)</param>
        public void SetGlobalCooldownReduction(float reduction)
        {
            GlobalCooldownReduction = Mathf.Clamp(reduction, 0f, 0.9f);
        }
        
        /// <summary>
        /// Adds to the global cooldown reduction modifier.
        /// </summary>
        /// <param name="additionalReduction">Additional reduction to add (stacks additively)</param>
        public void AddCooldownReduction(float additionalReduction)
        {
            GlobalCooldownReduction = Mathf.Clamp(GlobalCooldownReduction + additionalReduction, 0f, 0.9f);
        }
        
        #endregion
        
        #region Update Loop
        
        /// <summary>
        /// Updates all active cooldowns, decrementing their remaining time.
        /// </summary>
        private void UpdateCooldowns(float deltaTime)
        {
            // Update ability cooldowns
            List<string> finishedCooldowns = new List<string>();
            
            foreach (var kvp in activeCooldowns)
            {
                CooldownInstance cd = kvp.Value;
                
                if (cd.RemainingTime > 0f)
                {
                    cd.RemainingTime -= deltaTime;
                    
                    if (cd.RemainingTime <= 0f)
                    {
                        cd.RemainingTime = 0f;
                        finishedCooldowns.Add(kvp.Key);
                    }
                }
            }
            
            // Fire events for finished cooldowns
            foreach (string abilityId in finishedCooldowns)
            {
                OnCooldownFinished?.Invoke(abilityId);
                
                if (DebugMode)
                {
                    Debug.Log($"[CooldownSystem] Cooldown finished: {abilityId}");
                }
            }
            
            // Update group cooldowns
            foreach (var kvp in cooldownGroups)
            {
                CooldownGroup group = kvp.Value;
                
                if (group.RemainingTime > 0f)
                {
                    group.RemainingTime -= deltaTime;
                    group.RemainingTime = Mathf.Max(0f, group.RemainingTime);
                }
            }
        }
        
        #endregion
        
        #region Server Validation
        
        /// <summary>
        /// Validates that an ability is off cooldown on the server (anti-cheat).
        /// </summary>
        /// <param name="identity">The entity attempting to use the ability</param>
        /// <param name="abilityId">The ability identifier</param>
        /// <returns>True if the ability is off cooldown server-side</returns>
        public static bool ValidateCooldown(NetworkIdentity identity, string abilityId)
        {
            if (identity == null || string.IsNullOrEmpty(abilityId))
            {
                return false;
            }
            
            if (!serverCooldowns.ContainsKey(identity))
            {
                return true; // No cooldowns tracked = ability available
            }
            
            if (!serverCooldowns[identity].ContainsKey(abilityId))
            {
                return true; // No cooldown for this ability = available
            }
            
            return serverCooldowns[identity][abilityId].RemainingTime <= 0f;
        }
        
        /// <summary>
        /// Starts a cooldown on the server (authoritative).
        /// </summary>
        /// <param name="identity">The entity using the ability</param>
        /// <param name="abilityId">The ability identifier</param>
        /// <param name="duration">The cooldown duration</param>
        /// <param name="groupId">Optional group identifier</param>
        public static void ServerStartCooldown(NetworkIdentity identity, string abilityId, float duration, string groupId = null)
        {
            if (identity == null || string.IsNullOrEmpty(abilityId) || duration <= 0f)
            {
                return;
            }
            
            if (!serverCooldowns.ContainsKey(identity))
            {
                serverCooldowns[identity] = new Dictionary<string, CooldownInstance>();
            }
            
            if (!serverCooldowns[identity].ContainsKey(abilityId))
            {
                serverCooldowns[identity][abilityId] = new CooldownInstance(abilityId, duration, groupId);
            }
            else
            {
                serverCooldowns[identity][abilityId].RemainingTime = duration;
                serverCooldowns[identity][abilityId].TotalDuration = duration;
            }
            
            // Handle group cooldown
            if (!string.IsNullOrEmpty(groupId))
            {
                if (!serverGroups.ContainsKey(identity))
                {
                    serverGroups[identity] = new Dictionary<string, CooldownGroup>();
                }
                
                if (!serverGroups[identity].ContainsKey(groupId))
                {
                    serverGroups[identity][groupId] = new CooldownGroup(groupId, duration);
                }
                else
                {
                    serverGroups[identity][groupId].RemainingTime = duration;
                }
            }
        }
        
        /// <summary>
        /// Gets the server's authoritative remaining cooldown for an ability.
        /// </summary>
        /// <param name="identity">The entity to check</param>
        /// <param name="abilityId">The ability identifier</param>
        /// <returns>Remaining cooldown time, or 0 if not on cooldown</returns>
        public static float GetServerRemainingCooldown(NetworkIdentity identity, string abilityId)
        {
            if (identity == null || string.IsNullOrEmpty(abilityId))
            {
                return 0f;
            }
            
            if (!serverCooldowns.ContainsKey(identity) || !serverCooldowns[identity].ContainsKey(abilityId))
            {
                return 0f;
            }
            
            return Mathf.Max(0f, serverCooldowns[identity][abilityId].RemainingTime);
        }
        
        /// <summary>
        /// Updates server-side cooldown tracking.
        /// </summary>
        private void UpdateServerCooldown(string abilityId, float duration, string groupId)
        {
            if (!serverCooldowns.ContainsKey(NetworkIdentity))
            {
                serverCooldowns[NetworkIdentity] = new Dictionary<string, CooldownInstance>();
            }
            
            if (!serverCooldowns[NetworkIdentity].ContainsKey(abilityId))
            {
                serverCooldowns[NetworkIdentity][abilityId] = new CooldownInstance(abilityId, duration, groupId);
            }
            else
            {
                serverCooldowns[NetworkIdentity][abilityId].RemainingTime = duration;
                serverCooldowns[NetworkIdentity][abilityId].TotalDuration = duration;
            }
        }
        
        /// <summary>
        /// Updates server-side group cooldown tracking.
        /// </summary>
        private void UpdateServerGroupCooldown(string groupId, float duration)
        {
            if (!serverGroups.ContainsKey(NetworkIdentity))
            {
                serverGroups[NetworkIdentity] = new Dictionary<string, CooldownGroup>();
            }
            
            if (!serverGroups[NetworkIdentity].ContainsKey(groupId))
            {
                serverGroups[NetworkIdentity][groupId] = new CooldownGroup(groupId, duration);
            }
            else
            {
                serverGroups[NetworkIdentity][groupId].RemainingTime = duration;
                serverGroups[NetworkIdentity][groupId].TotalDuration = duration;
            }
        }
        
        #endregion
    }
    
    #region Data Structures
    
    /// <summary>
    /// Represents an active cooldown for a specific ability.
    /// </summary>
    [System.Serializable]
    public class CooldownInstance
    {
        public string AbilityId;
        public float RemainingTime;
        public float TotalDuration;
        public string GroupId;
        
        public CooldownInstance(string abilityId, float duration, string groupId = null)
        {
            AbilityId = abilityId;
            RemainingTime = duration;
            TotalDuration = duration;
            GroupId = groupId;
        }
    }
    
    /// <summary>
    /// Represents a cooldown group that affects multiple abilities.
    /// </summary>
    [System.Serializable]
    public class CooldownGroup
    {
        public string GroupId;
        public float RemainingTime;
        public float TotalDuration;
        
        public CooldownGroup(string groupId, float duration)
        {
            GroupId = groupId;
            RemainingTime = duration;
            TotalDuration = duration;
        }
    }
    
    #endregion
}

/*
USAGE EXAMPLE:

// 1. Attach CooldownSystem to player GameObject
GameObject player = new GameObject("Player");
player.AddComponent<NetworkIdentity>();
CooldownSystem cooldownSystem = player.AddComponent<CooldownSystem>();

// 2. Configure cooldown reduction
cooldownSystem.GlobalCooldownReduction = 0.2f; // 20% faster cooldowns

// 3. Subscribe to events
cooldownSystem.OnCooldownStarted += (abilityId, duration) =>
{
    Debug.Log($"Cooldown started: {abilityId} for {duration:F1}s");
};

cooldownSystem.OnCooldownFinished += (abilityId) =>
{
    Debug.Log($"Cooldown finished: {abilityId}");
};

cooldownSystem.OnCooldownBlocked += (abilityId, remaining) =>
{
    Debug.Log($"Ability {abilityId} on cooldown: {remaining:F1}s remaining");
};

// 4. Start a cooldown when ability is used
if (cooldownSystem.TryUseAbility("Fireball"))
{
    // Ability can be used
    CastFireball();
    cooldownSystem.StartCooldown("Fireball", 5f);
}

// 5. Use group cooldowns (e.g., all healing spells share 1s CD)
cooldownSystem.StartCooldown("HealingTouch", 8f, "HealingGroup");
cooldownSystem.StartCooldown("Rejuvenate", 10f, "HealingGroup");
// Both abilities will share the "HealingGroup" cooldown

// 6. Check cooldown status
float remaining = cooldownSystem.GetRemainingCooldown("Fireball");
Debug.Log($"Fireball cooldown: {remaining:F1}s");

float percentage = cooldownSystem.GetCooldownPercentage("Fireball");
Debug.Log($"Fireball ready: {percentage * 100f:F0}%");

// 7. Reduce cooldowns (e.g., from a buff/talent)
cooldownSystem.ReduceCooldown("Fireball", 2f); // Reduce by 2 seconds
cooldownSystem.AddCooldownReduction(0.1f); // Add 10% CDR permanently

// 8. Server validation (for multiplayer)
bool valid = CooldownSystem.ValidateCooldown(playerIdentity, "Fireball");
if (valid)
{
    CooldownSystem.ServerStartCooldown(playerIdentity, "Fireball", 5f);
}

// 9. Reset cooldowns (e.g., on respawn or special ability)
cooldownSystem.ResetCooldown("Fireball");
cooldownSystem.ResetAllCooldowns();

INTEGRATION NOTES:
- Attach to any GameObject with NetworkIdentity
- Cooldowns update automatically in Update()
- Server-authoritative validation prevents cooldown exploits
- Events allow UI integration (cooldown wheels, numeric displays)
- Group cooldowns enable "global cooldown" mechanics common in MMOs
- Future AbilitySystem will call StartCooldown() after successful cast
- Future UIManager will subscribe to events for cooldown visualizations
*/
