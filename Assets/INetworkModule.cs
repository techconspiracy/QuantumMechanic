// File: Assets/Scripts/RPG/Contracts/INetworkModule.cs
using UnityEngine;
using Unity.Netcode;

namespace RPG.Contracts
{
    /// <summary>
    /// Base contract for all network-synchronized RPG modules.
    /// Ensures consistent lifecycle and ownership validation.
    /// </summary>
    public interface INetworkModule
    {
        /// <summary>
        /// Unique identifier for this module instance.
        /// </summary>
        string ModuleId { get; }
        
        /// <summary>
        /// Called after NetworkObject spawns. Safe to initialize NetworkVariables here.
        /// </summary>
        void OnModuleInitialized();
        
        /// <summary>
        /// Called before NetworkObject despawns. Cleanup logic goes here.
        /// </summary>
        void OnModuleShutdown();
        
        /// <summary>
        /// Validates if this module can execute owner-only logic.
        /// </summary>
        bool ValidateOwnership();
    }

    /// <summary>
    /// Contract for modules that can receive damage.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float amount, ulong attackerId);
        bool IsDead { get; }
    }

    /// <summary>
    /// Contract for modules that manage resource pools (Health, Mana, Stamina).
    /// </summary>
    public interface IResourcePool
    {
        float CurrentValue { get; }
        float MaxValue { get; }
        void ModifyResource(float delta);
        void SetMaxValue(float newMax);
    }

    /// <summary>
    /// Contract for modules requiring data-driven configuration.
    /// </summary>
    public interface IConfigurable<T> where T : ScriptableObject
    {
        T Configuration { get; set; }
        void ApplyConfiguration();
    }
}