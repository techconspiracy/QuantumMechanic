// MODULE: Combat-01
// FILE: DamageSystem.cs
// DEPENDENCIES: NetworkIdentity.cs
// INTEGRATES WITH: Future WeaponController, AbilitySystem, HealthBar, CombatManager
// PURPOSE: Server-authoritative damage calculation with stat modifiers and damage types

using UnityEngine;
using System.Collections.Generic;

namespace QuantumMechanic.Combat
{
    /// <summary>
    /// Defines the types of damage that can be dealt in combat.
    /// Each type has unique interactions with armor and resistances.
    /// </summary>
    public enum DamageType
    {
        Physical,   // Reduced by armor
        Energy,     // Reduced by shields
        Poison,     // Damage over time, ignores armor
        Fire,       // Ignores armor, causes burning
        Electric,   // Stuns + damage, reduced by resistances
        Explosive,  // Area damage, partial armor penetration
        True        // Bypasses all defenses
    }

    /// <summary>
    /// Stores all combat-related statistics for an entity.
    /// Modified through DamageSystem methods only.
    /// </summary>
    public class CombatStats
    {
        public float MaxHealth = 100f;
        public float CurrentHealth = 100f;
        public float Armor = 0f;
        public float Shield = 0f;
        public float CritChance = 0.1f;
        public float CritMultiplier = 2f;
        public float DamageMultiplier = 1f;
        public Dictionary<DamageType, float> Resistances = new Dictionary<DamageType, float>();
        public Dictionary<DamageType, float> Vulnerabilities = new Dictionary<DamageType, float>();

        /// <summary>
        /// Initializes a new CombatStats instance with default resistance and vulnerability values.
        /// </summary>
        public CombatStats()
        {
            foreach (DamageType type in System.Enum.GetValues(typeof(DamageType)))
            {
                Resistances[type] = 0f;
                Vulnerabilities[type] = 1f;
            }
        }

        /// <summary>
        /// Creates a copy of this CombatStats instance.
        /// </summary>
        public CombatStats Clone()
        {
            CombatStats clone = new CombatStats
            {
                MaxHealth = this.MaxHealth,
                CurrentHealth = this.CurrentHealth,
                Armor = this.Armor,
                Shield = this.Shield,
                CritChance = this.CritChance,
                CritMultiplier = this.CritMultiplier,
                DamageMultiplier = this.DamageMultiplier
            };

            foreach (var kvp in Resistances)
            {
                clone.Resistances[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in Vulnerabilities)
            {
                clone.Vulnerabilities[kvp.Key] = kvp.Value;
            }

            return clone;
        }
    }

    /// <summary>
    /// Represents a request to deal damage to a target entity.
    /// Created by clients and validated/processed by server.
    /// </summary>
    public class DamageRequest
    {
        public NetworkIdentity Attacker;
        public NetworkIdentity Target;
        public float BaseDamage;
        public DamageType Type;
        public bool CanCrit = true;
        public float ArmorPenetration = 0f;
        public Vector3 HitPosition;
        public Vector3 HitNormal;
    }

    /// <summary>
    /// Contains the result of a damage calculation.
    /// Broadcast by server to all clients for visual feedback.
    /// </summary>
    public class DamageResult
    {
        public NetworkIdentity Target;
        public float FinalDamage;
        public float HealthRemaining;
        public bool WasCritical;
        public bool WasKilled;
        public DamageType Type;
        public Vector3 HitPosition;
        public Vector3 HitNormal;
    }

    /// <summary>
    /// Server-authoritative damage calculation system.
    /// Handles all combat math, stat modifiers, and entity health management.
    /// </summary>
    public static class DamageSystem
    {
        private static Dictionary<NetworkIdentity, CombatStats> entityStats = new Dictionary<NetworkIdentity, CombatStats>();

        /// <summary>
        /// Invoked when damage is successfully dealt to an entity.
        /// Subscribe to this for visual effects, UI updates, and gameplay reactions.
        /// </summary>
        public static event System.Action<DamageResult> OnDamageDealt;

        /// <summary>
        /// Invoked when an entity's health reaches zero.
        /// Subscribe to this for death animations, loot drops, and respawn logic.
        /// </summary>
        public static event System.Action<NetworkIdentity> OnEntityKilled;

        /// <summary>
        /// Invoked whenever an entity's health changes (damage or healing).
        /// Subscribe to this for health bar updates and status effects.
        /// </summary>
        public static event System.Action<NetworkIdentity, float> OnHealthChanged;

        /// <summary>
        /// Registers an entity with the damage system.
        /// Must be called before entity can deal or receive damage.
        /// </summary>
        /// <param name="entity">Network identity of the entity to register</param>
        /// <param name="stats">Initial combat statistics for the entity</param>
        public static void RegisterEntity(NetworkIdentity entity, CombatStats stats)
        {
            if (entity == null || stats == null)
            {
                Debug.LogWarning("[DamageSystem] Cannot register entity with null identity or stats");
                return;
            }

            if (entityStats.ContainsKey(entity))
            {
                entityStats[entity] = stats;
            }
            else
            {
                entityStats.Add(entity, stats);
            }
        }

        /// <summary>
        /// Removes an entity from the damage system.
        /// Call this when entity despawns or is destroyed to prevent memory leaks.
        /// </summary>
        /// <param name="entity">Network identity of the entity to unregister</param>
        public static void UnregisterEntity(NetworkIdentity entity)
        {
            if (entity == null) return;

            if (entityStats.ContainsKey(entity))
            {
                entityStats.Remove(entity);
            }
        }

        /// <summary>
        /// Retrieves the combat statistics for a registered entity.
        /// Returns null if entity is not registered.
        /// </summary>
        /// <param name="entity">Network identity of the entity</param>
        /// <returns>Combat stats or null if not found</returns>
        public static CombatStats GetStats(NetworkIdentity entity)
        {
            if (entity == null || !entityStats.ContainsKey(entity)) return null;
            return entityStats[entity];
        }

        /// <summary>
        /// Calculates final damage without applying it to the target.
        /// Useful for damage prediction and UI previews.
        /// </summary>
        /// <param name="request">Damage request containing all attack parameters</param>
        /// <returns>Damage result with calculated values, or null if invalid</returns>
        public static DamageResult CalculateDamage(DamageRequest request)
        {
            if (request == null || request.Target == null)
            {
                Debug.LogWarning("[DamageSystem] Invalid damage request");
                return null;
            }

            CombatStats attackerStats = GetStats(request.Attacker);
            CombatStats targetStats = GetStats(request.Target);

            if (targetStats == null)
            {
                Debug.LogWarning($"[DamageSystem] Target {request.Target.NetworkID} not registered");
                return null;
            }

            float damage = request.BaseDamage;

            // Apply attacker damage multiplier
            if (attackerStats != null)
            {
                damage *= attackerStats.DamageMultiplier;
            }

            // Critical hit check
            bool isCrit = false;
            if (request.CanCrit && CanDamageTypeCrit(request.Type))
            {
                if (attackerStats != null && Random.value < attackerStats.CritChance)
                {
                    damage *= attackerStats.CritMultiplier;
                    isCrit = true;
                }
            }

            // Apply damage type specific modifiers
            damage = ApplyDamageTypeModifiers(damage, request.Type, targetStats, request.ArmorPenetration);

            // Apply target resistances
            if (targetStats.Resistances.ContainsKey(request.Type))
            {
                float resistance = Mathf.Clamp01(targetStats.Resistances[request.Type]);
                damage *= (1f - resistance);
            }

            // Apply target vulnerabilities
            if (targetStats.Vulnerabilities.ContainsKey(request.Type))
            {
                damage *= Mathf.Max(0f, targetStats.Vulnerabilities[request.Type]);
            }

            // Ensure non-negative damage
            damage = Mathf.Max(0f, damage);

            // Build result
            DamageResult result = new DamageResult
            {
                Target = request.Target,
                FinalDamage = damage,
                WasCritical = isCrit,
                Type = request.Type,
                HitPosition = request.HitPosition,
                HitNormal = request.HitNormal,
                HealthRemaining = targetStats.CurrentHealth - damage,
                WasKilled = (targetStats.CurrentHealth - damage) <= 0f
            };

            return result;
        }

        /// <summary>
        /// Calculates and applies damage to the target entity.
        /// This is the primary method for dealing damage in combat.
        /// Server-side only.
        /// </summary>
        /// <param name="request">Damage request containing all attack parameters</param>
        public static void ApplyDamage(DamageRequest request)
        {
            DamageResult result = CalculateDamage(request);
            if (result == null) return;

            CombatStats targetStats = GetStats(request.Target);
            if (targetStats == null) return;

            // Apply damage to health
            targetStats.CurrentHealth -= result.FinalDamage;
            targetStats.CurrentHealth = Mathf.Max(0f, targetStats.CurrentHealth);

            result.HealthRemaining = targetStats.CurrentHealth;
            result.WasKilled = targetStats.CurrentHealth <= 0f;

            // Trigger events
            OnDamageDealt?.Invoke(result);
            OnHealthChanged?.Invoke(result.Target, targetStats.CurrentHealth);

            if (result.WasKilled)
            {
                OnEntityKilled?.Invoke(result.Target);
            }
        }

        /// <summary>
        /// Modifies an entity's health by the specified amount.
        /// Positive values heal, negative values damage.
        /// </summary>
        /// <param name="entity">Network identity of the entity</param>
        /// <param name="amount">Amount to modify health by (can be negative)</param>
        /// <param name="allowOverheal">If true, health can exceed MaxHealth</param>
        public static void ModifyHealth(NetworkIdentity entity, float amount, bool allowOverheal = false)
        {
            CombatStats stats = GetStats(entity);
            if (stats == null)
            {
                Debug.LogWarning($"[DamageSystem] Cannot modify health of unregistered entity {entity?.NetworkID}");
                return;
            }

            stats.CurrentHealth += amount;

            if (!allowOverheal)
            {
                stats.CurrentHealth = Mathf.Min(stats.CurrentHealth, stats.MaxHealth);
            }

            stats.CurrentHealth = Mathf.Max(0f, stats.CurrentHealth);

            OnHealthChanged?.Invoke(entity, stats.CurrentHealth);

            if (stats.CurrentHealth <= 0f)
            {
                OnEntityKilled?.Invoke(entity);
            }
        }

        /// <summary>
        /// Modifies a specific combat stat for an entity.
        /// Used for buffs, debuffs, and equipment changes.
        /// </summary>
        /// <param name="entity">Network identity of the entity</param>
        /// <param name="statName">Name of the stat to modify (case-insensitive)</param>
        /// <param name="amount">Amount to add to the stat (can be negative)</param>
        public static void ModifyStat(NetworkIdentity entity, string statName, float amount)
        {
            CombatStats stats = GetStats(entity);
            if (stats == null)
            {
                Debug.LogWarning($"[DamageSystem] Cannot modify stat of unregistered entity {entity?.NetworkID}");
                return;
            }

            switch (statName.ToLower())
            {
                case "maxhealth":
                    stats.MaxHealth = Mathf.Max(1f, stats.MaxHealth + amount);
                    stats.CurrentHealth = Mathf.Min(stats.CurrentHealth, stats.MaxHealth);
                    OnHealthChanged?.Invoke(entity, stats.CurrentHealth);
                    break;

                case "armor":
                    stats.Armor = Mathf.Max(0f, stats.Armor + amount);
                    break;

                case "shield":
                    stats.Shield = Mathf.Max(0f, stats.Shield + amount);
                    break;

                case "critmultiplier":
                    stats.CritMultiplier = Mathf.Max(1f, stats.CritMultiplier + amount);
                    break;

                case "critchance":
                    stats.CritChance = Mathf.Clamp01(stats.CritChance + amount);
                    break;

                case "damagemultiplier":
                    stats.DamageMultiplier = Mathf.Max(0f, stats.DamageMultiplier + amount);
                    break;

                default:
                    Debug.LogWarning($"[DamageSystem] Unknown stat name: {statName}");
                    break;
            }
        }

        /// <summary>
        /// Modifies resistance to a specific damage type.
        /// </summary>
        /// <param name="entity">Network identity of the entity</param>
        /// <param name="type">Damage type to modify resistance for</param>
        /// <param name="amount">Amount to add to resistance (0-1 scale)</param>
        public static void ModifyResistance(NetworkIdentity entity, DamageType type, float amount)
        {
            CombatStats stats = GetStats(entity);
            if (stats == null) return;

            if (stats.Resistances.ContainsKey(type))
            {
                stats.Resistances[type] = Mathf.Clamp01(stats.Resistances[type] + amount);
            }
        }

        /// <summary>
        /// Modifies vulnerability to a specific damage type.
        /// </summary>
        /// <param name="entity">Network identity of the entity</param>
        /// <param name="type">Damage type to modify vulnerability for</param>
        /// <param name="amount">Amount to add to vulnerability multiplier</param>
        public static void ModifyVulnerability(NetworkIdentity entity, DamageType type, float amount)
        {
            CombatStats stats = GetStats(entity);
            if (stats == null) return;

            if (stats.Vulnerabilities.ContainsKey(type))
            {
                stats.Vulnerabilities[type] = Mathf.Max(0f, stats.Vulnerabilities[type] + amount);
            }
        }

        /// <summary>
        /// Checks if an entity is currently alive (health > 0).
        /// </summary>
        /// <param name="entity">Network identity of the entity</param>
        /// <returns>True if entity is alive, false otherwise</returns>
        public static bool IsEntityAlive(NetworkIdentity entity)
        {
            CombatStats stats = GetStats(entity);
            return stats != null && stats.CurrentHealth > 0f;
        }

        /// <summary>
        /// Gets the current health percentage of an entity (0-1).
        /// </summary>
        /// <param name="entity">Network identity of the entity</param>
        /// <returns>Health percentage (0-1), or 0 if entity not found</returns>
        public static float GetHealthPercentage(NetworkIdentity entity)
        {
            CombatStats stats = GetStats(entity);
            if (stats == null || stats.MaxHealth <= 0f) return 0f;
            return Mathf.Clamp01(stats.CurrentHealth / stats.MaxHealth);
        }

        /// <summary>
        /// Resets an entity's health to maximum.
        /// </summary>
        /// <param name="entity">Network identity of the entity</param>
        public static void ResetHealth(NetworkIdentity entity)
        {
            CombatStats stats = GetStats(entity);
            if (stats == null) return;

            stats.CurrentHealth = stats.MaxHealth;
            OnHealthChanged?.Invoke(entity, stats.CurrentHealth);
        }

        /// <summary>
        /// Gets the number of currently registered entities.
        /// Useful for debugging and server monitoring.
        /// </summary>
        /// <returns>Count of registered entities</returns>
        public static int GetRegisteredEntityCount()
        {
            return entityStats.Count;
        }

        /// <summary>
        /// Clears all registered entities.
        /// Use this when resetting a level or returning to menu.
        /// </summary>
        public static void ClearAllEntities()
        {
            entityStats.Clear();
        }

        /// <summary>
        /// Applies damage type-specific modifiers to the damage value.
        /// Handles armor, shields, and special damage behaviors.
        /// </summary>
        private static float ApplyDamageTypeModifiers(float damage, DamageType type, CombatStats targetStats, float armorPen)
        {
            switch (type)
            {
                case DamageType.Physical:
                    // Armor reduces damage with diminishing returns
                    float effectiveArmor = targetStats.Armor * (1f - armorPen);
                    float reduction = effectiveArmor / (effectiveArmor + 100f);
                    damage *= (1f - reduction);
                    break;

                case DamageType.Energy:
                    // Depletes shield first, then damages health
                    if (targetStats.Shield > 0f)
                    {
                        float shieldDamage = Mathf.Min(damage * 0.5f, targetStats.Shield);
                        targetStats.Shield -= shieldDamage;
                        damage -= shieldDamage;
                    }
                    break;

                case DamageType.Fire:
                case DamageType.Poison:
                    // Ignores armor completely
                    break;

                case DamageType.Electric:
                    // Standard damage calculation
                    // Stun chance handled by caller using result data
                    break;

                case DamageType.Explosive:
                    // Built-in 50% armor penetration
                    armorPen = Mathf.Max(armorPen, 0.5f);
                    effectiveArmor = targetStats.Armor * (1f - armorPen);
                    reduction = effectiveArmor / (effectiveArmor + 100f);
                    damage *= (1f - reduction);
                    break;

                case DamageType.True:
                    // Bypasses all defenses - no modifiers
                    break;
            }

            return damage;
        }

        /// <summary>
        /// Determines if a damage type can score critical hits.
        /// </summary>
        private static bool CanDamageTypeCrit(DamageType type)
        {
            return type != DamageType.True && type != DamageType.Poison;
        }
    }
}

/* EXAMPLE USAGE:

// === SERVER-SIDE INITIALIZATION ===
void SpawnPlayer(NetworkIdentity playerIdentity)
{
    CombatStats stats = new CombatStats
    {
        MaxHealth = 500f,
        CurrentHealth = 500f,
        Armor = 50f,
        CritChance = 0.15f,
        CritMultiplier = 2.5f
    };
    
    DamageSystem.RegisterEntity(playerIdentity, stats);
}

// === WEAPON CONTROLLER (Future Module) ===
void OnWeaponHit(NetworkIdentity shooter, NetworkIdentity target, Vector3 hitPos, Vector3 hitNormal)
{
    DamageRequest request = new DamageRequest
    {
        Attacker = shooter,
        Target = target,
        BaseDamage = 100f,
        Type = DamageType.Physical,
        CanCrit = true,
        ArmorPenetration = 0.2f,
        HitPosition = hitPos,
        HitNormal = hitNormal
    };
    
    DamageSystem.ApplyDamage(request);
}

// === ABILITY SYSTEM (Future Module) ===
void CastFireball(NetworkIdentity caster, NetworkIdentity target)
{
    DamageRequest request = new DamageRequest
    {
        Attacker = caster,
        Target = target,
        BaseDamage = 250f,
        Type = DamageType.Fire,
        CanCrit = true,
        ArmorPenetration = 0f,
        HitPosition = target.transform.position,
        HitNormal = Vector3.up
    };
    
    DamageSystem.ApplyDamage(request);
}

// === UI HEALTH BAR (Future Module) ===
void OnEnable()
{
    DamageSystem.OnHealthChanged += UpdateHealthBar;
}

void UpdateHealthBar(NetworkIdentity entity, float currentHealth)
{
    if (entity == localPlayerIdentity)
    {
        healthBarFill.fillAmount = DamageSystem.GetHealthPercentage(entity);
    }
}

// === COMBAT MANAGER (Future Module) ===
void OnEnable()
{
    DamageSystem.OnDamageDealt += OnDamageDealtHandler;
    DamageSystem.OnEntityKilled += OnEntityKilledHandler;
}

void OnDamageDealtHandler(DamageResult result)
{
    // Spawn hit particles
    ParticleSystemFactory.CreateImpactEffect(result.HitPosition, result.Type);
    
    // Show damage number
    ShowDamageNumber(result.FinalDamage, result.HitPosition, result.WasCritical);
}

void OnEntityKilledHandler(NetworkIdentity entity)
{
    // Play death animation
    // Drop loot
    // Award XP
    DamageSystem.UnregisterEntity(entity);
}

// === BUFF/DEBUFF SYSTEM (Future Module) ===
void ApplyStrengthBuff(NetworkIdentity entity, float duration)
{
    DamageSystem.ModifyStat(entity, "damagemultiplier", 0.5f); // +50% damage
    StartCoroutine(RemoveBuffAfterDuration(entity, duration));
}

void ApplyArmorDebuff(NetworkIdentity entity, float amount)
{
    DamageSystem.ModifyStat(entity, "armor", -amount);
}

// === HEALING SYSTEM (Future Module) ===
void UseHealthPotion(NetworkIdentity entity)
{
    DamageSystem.ModifyHealth(entity, 100f, false); // Heal 100, no overheal
}

void UseShieldRecharge(NetworkIdentity entity)
{
    DamageSystem.ModifyStat(entity, "shield", 50f);
}

*/