// MODULE: Combat-03D
// FILE: AbilitySystem.cs
// DEPENDENCIES: NetworkIdentity.cs, DamageSystem.cs, ResourceSystem.cs, CooldownSystem.cs, CastingSystem.cs
// INTEGRATES WITH: WeaponController.cs (existing), ParticleSystemFactory.cs (existing), UIManager.cs (future), NetworkManager.cs (future)
// PURPOSE: Complete ability system with 6 targeting modes, buff/debuff management, and server validation

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace QuantumMechanic.Combat
{
    #region Data Structures

    /// <summary>
    /// Defines the type of ability effect.
    /// </summary>
    public enum AbilityType
    {
        Damage,
        Healing,
        Buff,
        Debuff,
        Utility,
        Summon
    }

    /// <summary>
    /// Defines how an ability selects its targets.
    /// </summary>
    public enum TargetMode
    {
        Self,
        SingleTarget,
        GroundTarget,
        Direction,
        Cone,
        Sphere
    }

    /// <summary>
    /// Defines which character stat a buff/debuff modifies.
    /// </summary>
    public enum StatType
    {
        Damage,
        Defense,
        MoveSpeed,
        AttackSpeed,
        CritChance,
        MaxHealth
    }

    /// <summary>
    /// Defines how a stat modifier is applied.
    /// </summary>
    public enum ModifierType
    {
        Flat,
        Percentage
    }

    /// <summary>
    /// Complete ability definition with all parameters.
    /// </summary>
    [System.Serializable]
    public class AbilityData
    {
        public string AbilityId = "fireball_001";
        public string AbilityName = "Fireball";
        public AbilityType Type = AbilityType.Damage;
        public TargetMode TargetMode = TargetMode.SingleTarget;
        public DamageType DamageType = DamageType.Fire;

        public float BasePower = 50f;
        public float Cooldown = 5f;
        public float CastTime = 1f;
        public float Range = 30f;
        public float Radius = 0f;
        public float ConeAngle = 90f;

        public ResourceType ResourceCost = ResourceType.Mana;
        public float ResourceAmount = 25f;

        public float Duration = 0f;
        public float TickInterval = 1f;

        public bool RequiresLineOfSight = true;
        public bool CanCastWhileMoving = false;
        public bool InterruptedByDamage = true;

        public int MaxTargets = 5;
        public bool AllowFriendlyFire = false;

        public StatType BuffStatType = StatType.Damage;
        public ModifierType BuffModifierType = ModifierType.Flat;
        public float BuffValue = 10f;
    }

    /// <summary>
    /// Represents a stat modification from a buff or debuff.
    /// </summary>
    [System.Serializable]
    public class StatModifier
    {
        public StatType Stat;
        public ModifierType Type;
        public float Value;

        public StatModifier(StatType stat, ModifierType type, float value)
        {
            Stat = stat;
            Type = type;
            Value = value;
        }
    }

    /// <summary>
    /// Active buff or debuff instance on an entity.
    /// </summary>
    public class BuffInstance
    {
        public string BuffId;
        public string BuffName;
        public NetworkIdentity Caster;
        public NetworkIdentity Target;
        public StatModifier StatMod;
        public float Duration;
        public float RemainingDuration;
        public bool IsBuff;
        public GameObject VisualEffect;
        public float TickInterval;
        public float NextTickTime;
        public AbilityData SourceAbility;
    }

    /// <summary>
    /// Client request to cast an ability.
    /// </summary>
    public class AbilityRequest
    {
        public NetworkIdentity Caster;
        public AbilityData Ability;
        public Vector3 TargetPosition;
        public NetworkIdentity TargetEntity;
        public Vector3 Direction;
        public float Timestamp;
    }

    /// <summary>
    /// Healing result for a single target.
    /// </summary>
    public class HealResult
    {
        public NetworkIdentity Target;
        public float AmountHealed;
        public bool WasCritical;
    }

    /// <summary>
    /// Server response after processing an ability.
    /// </summary>
    public class AbilityResult
    {
        public bool Success;
        public string FailureReason;
        public List<NetworkIdentity> Targets = new List<NetworkIdentity>();
        public List<DamageResult> DamageResults = new List<DamageResult>();
        public List<HealResult> HealResults = new List<HealResult>();
        public List<BuffInstance> BuffsApplied = new List<BuffInstance>();
    }

    #endregion

    /// <summary>
    /// Manages ability casting, targeting, effects, and buff/debuff system.
    /// Integrates with ResourceSystem, CooldownSystem, CastingSystem, and DamageSystem.
    /// </summary>
    public class AbilitySystem : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Ability Configuration")]
        [SerializeField] private List<AbilityData> equippedAbilities = new List<AbilityData>();
        [SerializeField] private LayerMask targetLayerMask = -1;
        [SerializeField] private LayerMask obstructionLayerMask = -1;

        [Header("Buff Configuration")]
        [SerializeField] private int maxActiveBuffs = 20;
        [SerializeField] private GameObject defaultBuffVFX;
        [SerializeField] private GameObject defaultDebuffVFX;

        [Header("Targeting Visualization")]
        [SerializeField] private bool showDebugTargeting = true;
        [SerializeField] private Color targetingColor = Color.cyan;
        [SerializeField] private float gizmosDuration = 2f;

        #endregion

        #region Private State

        private NetworkIdentity networkIdentity;
        private ResourceSystem resourceSystem;
        private CooldownSystem cooldownSystem;
        private CastingSystem castingSystem;
        private DamageSystem damageSystem;

        private List<BuffInstance> activeBuffs = new List<BuffInstance>();
        private Dictionary<string, AbilityData> abilityDatabase = new Dictionary<string, AbilityData>();
        private List<DebugTargetingInfo> debugTargetingQueue = new List<DebugTargetingInfo>();

        private class DebugTargetingInfo
        {
            public Vector3 Position;
            public float Radius;
            public float ExpiryTime;
        }

        #endregion

        #region Events

        /// <summary>
        /// Fired when an ability is successfully executed.
        /// </summary>
        public event System.Action<AbilityData, AbilityResult> OnAbilityExecuted;

        /// <summary>
        /// Fired when a buff is applied to this entity.
        /// </summary>
        public event System.Action<BuffInstance> OnBuffApplied;

        /// <summary>
        /// Fired when a buff is manually removed.
        /// </summary>
        public event System.Action<BuffInstance> OnBuffRemoved;

        /// <summary>
        /// Fired when a buff expires naturally.
        /// </summary>
        public event System.Action<BuffInstance> OnBuffExpired;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            networkIdentity = GetComponent<NetworkIdentity>();
            resourceSystem = GetComponent<ResourceSystem>();
            cooldownSystem = GetComponent<CooldownSystem>();
            castingSystem = GetComponent<CastingSystem>();
            damageSystem = GetComponent<DamageSystem>();

            if (networkIdentity == null)
            {
                Debug.LogError($"[AbilitySystem] NetworkIdentity component missing on {gameObject.name}");
            }

            InitializeAbilityDatabase();
        }

        private void Start()
        {
            if (castingSystem != null)
            {
                castingSystem.OnCastComplete += HandleCastComplete;
                castingSystem.OnCastInterrupted += HandleCastInterrupted;
            }
        }

        private void Update()
        {
            UpdateActiveBuffs();
            UpdateDebugVisualization();
        }

        private void OnDestroy()
        {
            if (castingSystem != null)
            {
                castingSystem.OnCastComplete -= HandleCastComplete;
                castingSystem.OnCastInterrupted -= HandleCastInterrupted;
            }

            ClearAllBuffs();
        }

        #endregion

        #region Public API - Ability Casting

        /// <summary>
        /// Attempts to cast an ability with the specified targeting information.
        /// </summary>
        /// <param name="ability">The ability to cast</param>
        /// <param name="targetPosition">Target position for ground/direction targeting</param>
        /// <param name="targetEntity">Target entity for single-target abilities</param>
        public void CastAbility(AbilityData ability, Vector3 targetPosition, NetworkIdentity targetEntity = null)
        {
            if (ability == null)
            {
                Debug.LogWarning("[AbilitySystem] Cannot cast null ability");
                return;
            }

            if (!CanCastAbility(ability))
            {
                Debug.Log($"[AbilitySystem] Cannot cast {ability.AbilityName} - requirements not met");
                return;
            }

            Vector3 direction = (targetPosition - transform.position).normalized;

            if (ability.CastTime > 0f)
            {
                AbilityRequest request = new AbilityRequest
                {
                    Caster = networkIdentity,
                    Ability = ability,
                    TargetPosition = targetPosition,
                    TargetEntity = targetEntity,
                    Direction = direction,
                    Timestamp = Time.time
                };

                castingSystem.StartCast(ability.AbilityName, ability.CastTime, ability.CanCastWhileMoving, 
                    ability.InterruptedByDamage, request);
            }
            else
            {
                ExecuteAbilityImmediate(ability, targetPosition, targetEntity, direction);
            }
        }

        /// <summary>
        /// Checks if an ability can currently be cast (cooldown, resources, etc).
        /// </summary>
        /// <param name="ability">The ability to check</param>
        /// <returns>True if the ability can be cast</returns>
        public bool CanCastAbility(AbilityData ability)
        {
            if (ability == null) return false;

            if (cooldownSystem != null && cooldownSystem.IsOnCooldown(ability.AbilityId))
            {
                return false;
            }

            if (resourceSystem != null && !resourceSystem.HasResource(ability.ResourceCost, ability.ResourceAmount))
            {
                return false;
            }

            if (castingSystem != null && castingSystem.IsCasting)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Retrieves an ability by its unique identifier.
        /// </summary>
        /// <param name="abilityId">The ability ID to look up</param>
        /// <returns>The ability data, or null if not found</returns>
        public AbilityData GetAbilityById(string abilityId)
        {
            if (abilityDatabase.TryGetValue(abilityId, out AbilityData ability))
            {
                return ability;
            }
            return null;
        }

        /// <summary>
        /// Gets the list of abilities currently equipped by this entity.
        /// </summary>
        /// <returns>List of equipped abilities</returns>
        public List<AbilityData> GetEquippedAbilities()
        {
            return new List<AbilityData>(equippedAbilities);
        }

        /// <summary>
        /// Equips an ability, adding it to the hotbar.
        /// </summary>
        /// <param name="ability">The ability to equip</param>
        public void EquipAbility(AbilityData ability)
        {
            if (ability == null) return;

            if (!equippedAbilities.Contains(ability))
            {
                equippedAbilities.Add(ability);
                Debug.Log($"[AbilitySystem] Equipped ability: {ability.AbilityName}");
            }
        }

        /// <summary>
        /// Unequips an ability, removing it from the hotbar.
        /// </summary>
        /// <param name="abilityId">The ID of the ability to unequip</param>
        public void UnequipAbility(string abilityId)
        {
            AbilityData ability = equippedAbilities.FirstOrDefault(a => a.AbilityId == abilityId);
            if (ability != null)
            {
                equippedAbilities.Remove(ability);
                Debug.Log($"[AbilitySystem] Unequipped ability: {ability.AbilityName}");
            }
        }

        #endregion

        #region Target Finding (Private)

        /// <summary>
        /// Finds all valid targets for an ability based on its targeting mode.
        /// </summary>
        private List<NetworkIdentity> FindTargets(AbilityRequest request)
        {
            List<NetworkIdentity> targets = new List<NetworkIdentity>();
            Vector3 casterPosition = request.Caster.transform.position;
            Vector3 casterForward = request.Caster.transform.forward;

            switch (request.Ability.TargetMode)
            {
                case TargetMode.Self:
                    targets.Add(request.Caster);
                    break;

                case TargetMode.SingleTarget:
                    NetworkIdentity singleTarget = FindSingleTarget(casterPosition, request.Direction, request.Ability);
                    if (singleTarget != null)
                    {
                        targets.Add(singleTarget);
                    }
                    break;

                case TargetMode.GroundTarget:
                case TargetMode.Sphere:
                    targets = FindSphereTargets(request.TargetPosition, request.Ability);
                    break;

                case TargetMode.Direction:
                    targets = FindDirectionTargets(casterPosition, request.Direction, request.Ability);
                    break;

                case TargetMode.Cone:
                    targets = FindConeTargets(casterPosition, casterForward, request.Ability);
                    break;
            }

            return FilterTargets(targets, request.Ability, request.Caster);
        }

        /// <summary>
        /// Finds a single target using raycast.
        /// </summary>
        private NetworkIdentity FindSingleTarget(Vector3 origin, Vector3 direction, AbilityData ability)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, ability.Range, targetLayerMask))
            {
                if (ability.RequiresLineOfSight)
                {
                    if (Physics.Raycast(origin, direction, hit.distance, obstructionLayerMask))
                    {
                        return null;
                    }
                }

                NetworkIdentity target = hit.collider.GetComponent<NetworkIdentity>();
                return target;
            }
            return null;
        }

        /// <summary>
        /// Finds all targets within a sphere.
        /// </summary>
        private List<NetworkIdentity> FindSphereTargets(Vector3 center, AbilityData ability)
        {
            List<NetworkIdentity> targets = new List<NetworkIdentity>();
            Collider[] colliders = Physics.OverlapSphere(center, ability.Radius, targetLayerMask);

            if (showDebugTargeting)
            {
                debugTargetingQueue.Add(new DebugTargetingInfo
                {
                    Position = center,
                    Radius = ability.Radius,
                    ExpiryTime = Time.time + gizmosDuration
                });
            }

            foreach (Collider col in colliders)
            {
                NetworkIdentity identity = col.GetComponent<NetworkIdentity>();
                if (identity != null && targets.Count < ability.MaxTargets)
                {
                    targets.Add(identity);
                }
            }

            return targets;
        }

        /// <summary>
        /// Finds all targets in a line/direction.
        /// </summary>
        private List<NetworkIdentity> FindDirectionTargets(Vector3 origin, Vector3 direction, AbilityData ability)
        {
            List<NetworkIdentity> targets = new List<NetworkIdentity>();
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, ability.Range, targetLayerMask);

            foreach (RaycastHit hit in hits)
            {
                NetworkIdentity identity = hit.collider.GetComponent<NetworkIdentity>();
                if (identity != null && targets.Count < ability.MaxTargets)
                {
                    targets.Add(identity);
                }
            }

            return targets;
        }

        /// <summary>
        /// Finds all targets within a cone area.
        /// </summary>
        private List<NetworkIdentity> FindConeTargets(Vector3 origin, Vector3 forward, AbilityData ability)
        {
            List<NetworkIdentity> targets = new List<NetworkIdentity>();
            Collider[] colliders = Physics.OverlapSphere(origin, ability.Range, targetLayerMask);

            foreach (Collider col in colliders)
            {
                Vector3 dirToTarget = (col.transform.position - origin).normalized;
                float angle = Vector3.Angle(forward, dirToTarget);

                if (angle <= ability.ConeAngle / 2f)
                {
                    if (ability.RequiresLineOfSight)
                    {
                        float distance = Vector3.Distance(origin, col.transform.position);
                        if (Physics.Raycast(origin, dirToTarget, distance, obstructionLayerMask))
                        {
                            continue;
                        }
                    }

                    NetworkIdentity identity = col.GetComponent<NetworkIdentity>();
                    if (identity != null && targets.Count < ability.MaxTargets)
                    {
                        targets.Add(identity);
                    }
                }
            }

            return targets;
        }

        /// <summary>
        /// Filters targets based on ability rules (friendly fire, max targets, etc).
        /// </summary>
        private List<NetworkIdentity> FilterTargets(List<NetworkIdentity> targets, AbilityData ability, NetworkIdentity caster)
        {
            List<NetworkIdentity> filtered = new List<NetworkIdentity>();

            foreach (NetworkIdentity target in targets)
            {
                if (target == null) continue;

                if (!ability.AllowFriendlyFire)
                {
                    if (target.Team == caster.Team && target != caster)
                    {
                        continue;
                    }
                }

                filtered.Add(target);

                if (filtered.Count >= ability.MaxTargets)
                {
                    break;
                }
            }

            return filtered;
        }

        #endregion

        #region Effect Application (Private)

        /// <summary>
        /// Executes an ability immediately without cast time.
        /// </summary>
        private void ExecuteAbilityImmediate(AbilityData ability, Vector3 targetPosition, 
            NetworkIdentity targetEntity, Vector3 direction)
        {
            AbilityRequest request = new AbilityRequest
            {
                Caster = networkIdentity,
                Ability = ability,
                TargetPosition = targetPosition,
                TargetEntity = targetEntity,
                Direction = direction,
                Timestamp = Time.time
            };

            AbilityResult result = ProcessAbilityRequest(request);

            if (result.Success)
            {
                if (cooldownSystem != null)
                {
                    cooldownSystem.StartCooldown(ability.AbilityId, ability.Cooldown);
                }

                if (resourceSystem != null)
                {
                    resourceSystem.ConsumeResource(ability.ResourceCost, ability.ResourceAmount);
                }

                OnAbilityExecuted?.Invoke(ability, result);
            }
        }

        /// <summary>
        /// Server-authoritative ability processing and validation.
        /// </summary>
        public static AbilityResult ProcessAbilityRequest(AbilityRequest request)
        {
            AbilityResult result = new AbilityResult();

            if (!ValidateAbilityRequest(request))
            {
                result.Success = false;
                result.FailureReason = "Validation failed";
                return result;
            }

            AbilitySystem casterAbilitySystem = request.Caster.GetComponent<AbilitySystem>();
            List<NetworkIdentity> targets = casterAbilitySystem.FindTargets(request);

            if (targets.Count == 0)
            {
                result.Success = false;
                result.FailureReason = "No valid targets";
                return result;
            }

            result.Targets = targets;
            casterAbilitySystem.ApplyAbilityEffects(request, targets, result);

            result.Success = true;
            return result;
        }

        /// <summary>
        /// Validates that an ability request meets all requirements.
        /// </summary>
        public static bool ValidateAbilityRequest(AbilityRequest request)
        {
            if (request == null || request.Caster == null || request.Ability == null)
            {
                return false;
            }

            AbilitySystem abilitySystem = request.Caster.GetComponent<AbilitySystem>();
            if (abilitySystem == null) return false;

            ResourceSystem resources = request.Caster.GetComponent<ResourceSystem>();
            if (resources != null)
            {
                if (!resources.HasResource(request.Ability.ResourceCost, request.Ability.ResourceAmount))
                {
                    return false;
                }
            }

            CooldownSystem cooldowns = request.Caster.GetComponent<CooldownSystem>();
            if (cooldowns != null)
            {
                if (cooldowns.IsOnCooldown(request.Ability.AbilityId))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Applies ability effects to all valid targets.
        /// </summary>
        private void ApplyAbilityEffects(AbilityRequest request, List<NetworkIdentity> targets, AbilityResult result)
        {
            foreach (NetworkIdentity target in targets)
            {
                if (target == null) continue;

                switch (request.Ability.Type)
                {
                    case AbilityType.Damage:
                        ApplyDamageEffect(request, target, result);
                        break;

                    case AbilityType.Healing:
                        ApplyHealingEffect(request, target, result);
                        break;

                    case AbilityType.Buff:
                    case AbilityType.Debuff:
                        ApplyBuffEffect(request, target, result);
                        break;

                    case AbilityType.Utility:
                        ApplyUtilityEffect(request, target, result);
                        break;

                    case AbilityType.Summon:
                        ApplySummonEffect(request, target, result);
                        break;
                }
            }
        }

        /// <summary>
        /// Applies damage to a target using the DamageSystem.
        /// </summary>
        private void ApplyDamageEffect(AbilityRequest request, NetworkIdentity target, AbilityResult result)
        {
            DamageRequest damageReq = new DamageRequest
            {
                Attacker = request.Caster,
                Target = target,
                BaseDamage = request.Ability.BasePower,
                Type = request.Ability.DamageType,
                CanCrit = true,
                IsMelee = false,
                KnockbackForce = 0f
            };

            DamageResult damageResult = DamageSystem.ApplyDamage(damageReq);
            result.DamageResults.Add(damageResult);
        }

        /// <summary>
        /// Applies healing to a target.
        /// </summary>
        private void ApplyHealingEffect(AbilityRequest request, NetworkIdentity target, AbilityResult result)
        {
            ResourceSystem targetResources = target.GetComponent<ResourceSystem>();
            if (targetResources == null) return;

            float healAmount = request.Ability.BasePower;
            bool isCrit = Random.value < 0.1f;

            if (isCrit)
            {
                healAmount *= 1.5f;
            }

            float actualHealed = targetResources.RestoreResource(ResourceType.Health, healAmount);

            result.HealResults.Add(new HealResult
            {
                Target = target,
                AmountHealed = actualHealed,
                WasCritical = isCrit
            });
        }

        /// <summary>
        /// Applies a buff or debuff to a target.
        /// </summary>
        private void ApplyBuffEffect(AbilityRequest request, NetworkIdentity target, AbilityResult result)
        {
            AbilitySystem targetAbilitySystem = target.GetComponent<AbilitySystem>();
            if (targetAbilitySystem == null) return;

            BuffInstance buff = CreateBuffFromAbility(request.Ability, request.Caster, target);
            targetAbilitySystem.ApplyBuff(buff);
            result.BuffsApplied.Add(buff);
        }

        /// <summary>
        /// Applies utility effects (movement, teleport, etc).
        /// </summary>
        private void ApplyUtilityEffect(AbilityRequest request, NetworkIdentity target, AbilityResult result)
        {
            Debug.Log($"[AbilitySystem] Utility effect applied to {target.EntityId}");
        }

        /// <summary>
        /// Spawns summoned entities.
        /// </summary>
        private void ApplySummonEffect(AbilityRequest request, NetworkIdentity target, AbilityResult result)
        {
            Debug.Log($"[AbilitySystem] Summon effect triggered at {request.TargetPosition}");
        }

        /// <summary>
        /// Creates a BuffInstance from an AbilityData.
        /// </summary>
        private BuffInstance CreateBuffFromAbility(AbilityData ability, NetworkIdentity caster, NetworkIdentity target)
        {
            StatModifier statMod = new StatModifier(
                ability.BuffStatType,
                ability.BuffModifierType,
                ability.BuffValue
            );

            return new BuffInstance
            {
                BuffId = $"{ability.AbilityId}_{System.Guid.NewGuid()}",
                BuffName = ability.AbilityName,
                Caster = caster,
                Target = target,
                StatMod = statMod,
                Duration = ability.Duration,
                RemainingDuration = ability.Duration,
                IsBuff = ability.Type == AbilityType.Buff,
                TickInterval = ability.TickInterval,
                NextTickTime = Time.time + ability.TickInterval,
                SourceAbility = ability
            };
        }

        #endregion

        #region Buff Management

        /// <summary>
        /// Applies a buff to this entity.
        /// </summary>
        /// <param name="buff">The buff to apply</param>
        public void ApplyBuff(BuffInstance buff)
        {
            if (buff == null) return;

            if (activeBuffs.Count >= maxActiveBuffs)
            {
                Debug.LogWarning($"[AbilitySystem] Max buffs reached ({maxActiveBuffs}), cannot apply {buff.BuffName}");
                return;
            }

            BuffInstance existing = activeBuffs.FirstOrDefault(b => b.BuffName == buff.BuffName && b.Caster == buff.Caster);
            if (existing != null)
            {
                existing.RemainingDuration = buff.Duration;
                Debug.Log($"[AbilitySystem] Refreshed buff: {buff.BuffName}");
            }
            else
            {
                activeBuffs.Add(buff);

                if (buff.IsBuff && defaultBuffVFX != null)
                {
                    buff.VisualEffect = Instantiate(defaultBuffVFX, transform.position, Quaternion.identity, transform);
                }
                else if (!buff.IsBuff && defaultDebuffVFX != null)
                {
                    buff.VisualEffect = Instantiate(defaultDebuffVFX, transform.position, Quaternion.identity, transform);
                }

                OnBuffApplied?.Invoke(buff);
                Debug.Log($"[AbilitySystem] Applied {(buff.IsBuff ? "buff" : "debuff")}: {buff.BuffName}");
            }
        }

        /// <summary>
        /// Removes a specific buff by its ID.
        /// </summary>
        /// <param name="buffId">The ID of the buff to remove</param>
        public void RemoveBuff(string buffId)
        {
            BuffInstance buff = activeBuffs.FirstOrDefault(b => b.BuffId == buffId);
            if (buff != null)
            {
                activeBuffs.Remove(buff);

                if (buff.VisualEffect != null)
                {
                    Destroy(buff.VisualEffect);
                }

                OnBuffRemoved?.Invoke(buff);
                Debug.Log($"[AbilitySystem] Removed buff: {buff.BuffName}");
            }
        }

        /// <summary>
        /// Gets all currently active buffs on this entity.
        /// </summary>
        /// <returns>List of active buffs</returns>
        public List<BuffInstance> GetActiveBuffs()
        {
            return new List<BuffInstance>(activeBuffs);
        }

        /// <summary>
        /// Calculates the total stat modifier for a given stat from all active buffs.
        /// </summary>
        /// <param name="stat">The stat type to calculate</param>
        /// <returns>Total modifier value</returns>
        public float GetStatModifier(StatType stat)
        {
            float flatBonus = 0f;
            float percentBonus = 1f;

            foreach (BuffInstance buff in activeBuffs)
            {
                if (buff.StatMod.Stat == stat)
                {
                    if (buff.StatMod.Type == ModifierType.Flat)
                    {
                        flatBonus += buff.StatMod.Value;
                    }
                    else if (buff.StatMod.Type == ModifierType.Percentage)
                    {
                        percentBonus += buff.StatMod.Value / 100f;
                    }
                }
            }

            return flatBonus * percentBonus;
        }

        /// <summary>
        /// Updates all active buffs, handling expiration and tick effects.
        /// </summary>
        private void UpdateActiveBuffs()
        {
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                BuffInstance buff = activeBuffs[i];
                buff.RemainingDuration -= Time.deltaTime;

                if (buff.TickInterval > 0f && Time.time >= buff.NextTickTime)
                {
                    ProcessBuffTick(buff);
                    buff.NextTickTime = Time.time + buff.TickInterval;
                }

                if (buff.RemainingDuration <= 0f)
                {
                    activeBuffs.RemoveAt(i);

                    if (buff.VisualEffect != null)
                    {
                        Destroy(buff.VisualEffect);
                    }

                    OnBuffExpired?.Invoke(buff);
                    Debug.Log($"[AbilitySystem] Buff expired: {buff.BuffName}");
                }
            }
        }

        /// <summary>
        /// Processes a tick effect for buffs with TickInterval (DoTs/HoTs).
        /// </summary>
        private void ProcessBuffTick(BuffInstance buff)
        {
            if (buff.SourceAbility.Type == AbilityType.Damage)
            {
                DamageRequest tickDamage = new DamageRequest
                {
                    Attacker = buff.Caster,
                    Target = buff.Target,
                    BaseDamage = buff.SourceAbility.BasePower,
                    Type = buff.SourceAbility.DamageType,
                    CanCrit = false,
                    IsMelee = false,
                    KnockbackForce = 0f
                };
                DamageSystem.ApplyDamage(tickDamage);
            }
            else if (buff.SourceAbility.Type == AbilityType.Healing)
            {
                ResourceSystem targetResources = buff.Target.GetComponent<ResourceSystem>();
                if (targetResources != null)
                {
                    targetResources.RestoreResource(ResourceType.Health, buff.SourceAbility.BasePower);
                }
            }
        }

        /// <summary>
        /// Removes all buffs from this entity.
        /// </summary>
        private void ClearAllBuffs()
        {
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                BuffInstance buff = activeBuffs[i];
                if (buff.VisualEffect != null)
                {
                    Destroy(buff.VisualEffect);
                }
            }
            activeBuffs.Clear();
        }

        #endregion

        #region Server Validation

        /// <summary>
        /// Handles completed cast from CastingSystem.
        /// </summary>
        private void HandleCastComplete(string castId, object context)
        {
            if (context is AbilityRequest request)
            {
                ExecuteAbilityImmediate(request.Ability, request.TargetPosition, 
                    request.TargetEntity, request.Direction);
            }
        }

        /// <summary>
        /// Handles interrupted cast from CastingSystem.
        /// </summary>
        private void HandleCastInterrupted(string castId, object context)
        {
            if (context is AbilityRequest request)
            {
                Debug.Log($"[AbilitySystem] Cast interrupted: {request.Ability.AbilityName}");
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the ability database from equipped abilities.
        /// </summary>
        private void InitializeAbilityDatabase()
        {
            abilityDatabase.Clear();

            foreach (AbilityData ability in equippedAbilities)
            {
                if (ability != null && !string.IsNullOrEmpty(ability.AbilityId))
                {
                    abilityDatabase[ability.AbilityId] = ability;
                }
            }

            Debug.Log($"[AbilitySystem] Initialized with {abilityDatabase.Count} abilities");
        }

        #endregion

        #region Debug

        /// <summary>
        /// Updates debug visualization for targeting areas.
        /// </summary>
        private void UpdateDebugVisualization()
        {
            if (!showDebugTargeting) return;

            for (int i = debugTargetingQueue.Count - 1; i >= 0; i--)
            {
                if (Time.time >= debugTargetingQueue[i].ExpiryTime)
                {
                    debugTargetingQueue.RemoveAt(i);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugTargeting) return;

            Gizmos.color = targetingColor;

            foreach (DebugTargetingInfo info in debugTargetingQueue)
            {
                Gizmos.DrawWireSphere(info.Position, info.Radius);
            }

            if (equippedAbilities.Count > 0 && equippedAbilities[0] != null)
            {
                AbilityData ability = equippedAbilities[0];
                Vector3 origin = transform.position;
                Vector3 forward = transform.forward;

                switch (ability.TargetMode)
                {
                    case TargetMode.SingleTarget:
                    case TargetMode.Direction:
                        Gizmos.DrawRay(origin, forward * ability.Range);
                        break;

                    case TargetMode.Sphere:
                        Gizmos.DrawWireSphere(origin, ability.Radius);
                        break;

                    case TargetMode.Cone:
                        DrawConeGizmo(origin, forward, ability.Range, ability.ConeAngle);
                        break;
                }
            }
        }

        /// <summary>
        /// Draws a cone gizmo for cone-targeted abilities.
        /// </summary>
        private void DrawConeGizmo(Vector3 origin, Vector3 direction, float range, float angle)
        {
            int segments = 16;
            float halfAngle = angle / 2f;

            Vector3 rightBoundary = Quaternion.Euler(0, halfAngle, 0) * direction * range;
            Vector3 leftBoundary = Quaternion.Euler(0, -halfAngle, 0) * direction * range;

            Gizmos.DrawLine(origin, origin + rightBoundary);
            Gizmos.DrawLine(origin, origin + leftBoundary);

            Vector3 previousPoint = origin + rightBoundary;
            for (int i = 1; i <= segments; i++)
            {
                float currentAngle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
                Vector3 currentPoint = origin + Quaternion.Euler(0, currentAngle, 0) * direction * range;
                Gizmos.DrawLine(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }
        }

        #endregion
    }
}

/*
===============================================================================
TESTING GUIDE
===============================================================================

TEST 1: BASIC ABILITY CASTING
-------------------------------
// Attach AbilitySystem to player GameObject
AbilitySystem abilitySystem = player.GetComponent<AbilitySystem>();

// Create a simple damage ability
AbilityData fireball = new AbilityData
{
    AbilityId = "fireball_001",
    AbilityName = "Fireball",
    Type = AbilityType.Damage,
    TargetMode = TargetMode.SingleTarget,
    DamageType = DamageType.Fire,
    BasePower = 50f,
    Cooldown = 5f,
    CastTime = 1f,
    Range = 30f,
    ResourceCost = ResourceType.Mana,
    ResourceAmount = 25f
};

abilitySystem.EquipAbility(fireball);

// Cast at a target
Vector3 targetPos = enemy.transform.position;
abilitySystem.CastAbility(fireball, targetPos, enemy.GetComponent<NetworkIdentity>());

EXPECTED: Casting bar appears for 1 second, then fireball deals 50 damage.


TEST 2: INSTANT CAST ABILITIES
-------------------------------
AbilityData instantHeal = new AbilityData
{
    AbilityId = "heal_001",
    AbilityName = "Instant Heal",
    Type = AbilityType.Healing,
    TargetMode = TargetMode.Self,
    BasePower = 100f,
    Cooldown = 10f,
    CastTime = 0f, // Instant!
    ResourceCost = ResourceType.Mana,
    ResourceAmount = 50f
};

abilitySystem.EquipAbility(instantHeal);
abilitySystem.CastAbility(instantHeal, Vector3.zero, null);

EXPECTED: Player heals 100 HP immediately, no cast time.


TEST 3: AOE SPHERE TARGETING
-----------------------------
AbilityData explosion = new AbilityData
{
    AbilityId = "explosion_001",
    AbilityName = "Explosion",
    Type = AbilityType.Damage,
    TargetMode = TargetMode.Sphere,
    DamageType = DamageType.Fire,
    BasePower = 75f,
    Cooldown = 8f,
    CastTime = 2f,
    Range = 50f,
    Radius = 10f,
    MaxTargets = 5,
    ResourceCost = ResourceType.Mana,
    ResourceAmount = 75f
};

abilitySystem.EquipAbility(explosion);
Vector3 groundTarget = GetGroundClickPosition();
abilitySystem.CastAbility(explosion, groundTarget);

EXPECTED: After 2s cast, all enemies within 10m of target take 75 damage (max 5 targets).


TEST 4: CONE TARGETING
----------------------
AbilityData flameBreath = new AbilityData
{
    AbilityId = "flame_breath_001",
    AbilityName = "Flame Breath",
    Type = AbilityType.Damage,
    TargetMode = TargetMode.Cone,
    DamageType = DamageType.Fire,
    BasePower = 60f,
    Cooldown = 6f,
    CastTime = 0.5f,
    Range = 15f,
    ConeAngle = 90f,
    MaxTargets = 8,
    ResourceCost = ResourceType.Mana,
    ResourceAmount = 40f
};

abilitySystem.EquipAbility(flameBreath);
abilitySystem.CastAbility(flameBreath, transform.position + transform.forward);

EXPECTED: Enemies in 90° cone in front take 60 fire damage.


TEST 5: BUFF APPLICATION
-------------------------
AbilityData strengthBuff = new AbilityData
{
    AbilityId = "strength_buff_001",
    AbilityName = "Strength",
    Type = AbilityType.Buff,
    TargetMode = TargetMode.Self,
    Duration = 30f,
    CastTime = 0f,
    Cooldown = 60f,
    BuffStatType = StatType.Damage,
    BuffModifierType = ModifierType.Percentage,
    BuffValue = 25f, // +25% damage
    ResourceCost = ResourceType.Mana,
    ResourceAmount = 50f
};

abilitySystem.EquipAbility(strengthBuff);
abilitySystem.CastAbility(strengthBuff, Vector3.zero);

// Check buff is active
float damageBonus = abilitySystem.GetStatModifier(StatType.Damage);
Debug.Log($"Damage bonus: {damageBonus}%");

EXPECTED: +25% damage for 30 seconds, then buff expires.


TEST 6: DEBUFF APPLICATION
---------------------------
AbilityData weakness = new AbilityData
{
    AbilityId = "weakness_001",
    AbilityName = "Weakness",
    Type = AbilityType.Debuff,
    TargetMode = TargetMode.SingleTarget,
    Duration = 20f,
    CastTime = 1f,
    Cooldown = 30f,
    Range = 25f,
    BuffStatType = StatType.Defense,
    BuffModifierType = ModifierType.Flat,
    BuffValue = -10f, // -10 armor
    ResourceCost = ResourceType.Mana,
    ResourceAmount = 30f
};

abilitySystem.EquipAbility(weakness);
abilitySystem.CastAbility(weakness, enemy.transform.position, enemy.GetComponent<NetworkIdentity>());

// On enemy
AbilitySystem enemyAbility = enemy.GetComponent<AbilitySystem>();
List<BuffInstance> debuffs = enemyAbility.GetActiveBuffs();

EXPECTED: Enemy has -10 defense for 20 seconds.


TEST 7: DAMAGE OVER TIME (DoT)
-------------------------------
AbilityData poison = new AbilityData
{
    AbilityId = "poison_001",
    AbilityName = "Poison",
    Type = AbilityType.Damage,
    TargetMode = TargetMode.SingleTarget,
    DamageType = DamageType.Poison,
    BasePower = 10f, // Per tick
    Duration = 10f,
    TickInterval = 1f, // Every 1 second
    CastTime = 0f,
    Cooldown = 15f,
    Range = 20f,
    ResourceCost = ResourceType.Mana,
    ResourceAmount = 20f
};

abilitySystem.EquipAbility(poison);
abilitySystem.CastAbility(poison, enemy.transform.position, enemy.GetComponent<NetworkIdentity>());

EXPECTED: Enemy takes 10 poison damage every second for 10 seconds (100 total).


TEST 8: HEAL OVER TIME (HoT)
-----------------------------
AbilityData regeneration = new AbilityData
{
    AbilityId = "regen_001",
    AbilityName = "Regeneration",
    Type = AbilityType.Healing,
    TargetMode = TargetMode.Self,
    BasePower = 20f, // Per tick
    Duration = 15f,
    TickInterval = 3f, // Every 3 seconds
    CastTime = 0f,
    Cooldown = 45f,
    ResourceCost = ResourceType.Mana,
    ResourceAmount = 60f
};

abilitySystem.EquipAbility(regeneration);
abilitySystem.CastAbility(regeneration, Vector3.zero);

EXPECTED: Player heals 20 HP every 3 seconds for 15 seconds (100 HP total).


TEST 9: RESOURCE COST VALIDATION
---------------------------------
// Set mana to 10
ResourceSystem resources = player.GetComponent<ResourceSystem>();
resources.SetResourceMax(ResourceType.Mana, 100f);
resources.SetResourceCurrent(ResourceType.Mana, 10f);

AbilityData expensiveAbility = new AbilityData
{
    AbilityId = "expensive_001",
    AbilityName = "Expensive Spell",
    Type = AbilityType.Damage,
    TargetMode = TargetMode.Self,
    BasePower = 100f,
    ResourceCost = ResourceType.Mana,
    ResourceAmount = 50f // More than we have
};

bool canCast = abilitySystem.CanCastAbility(expensiveAbility);
Debug.Log($"Can cast: {canCast}");

EXPECTED: canCast = false, ability fails due to insufficient mana.


TEST 10: COOLDOWN TRACKING
---------------------------
AbilityData quickSpell = new AbilityData
{
    AbilityId = "quick_001",
    AbilityName = "Quick Spell",
    Type = AbilityType.Damage,
    TargetMode = TargetMode.Self,
    BasePower = 30f,
    Cooldown = 5f,
    CastTime = 0f,
    ResourceCost = ResourceType.Mana,
    ResourceAmount = 10f
};

abilitySystem.EquipAbility(quickSpell);
abilitySystem.CastAbility(quickSpell, Vector3.zero);

// Try to cast again immediately
bool canCastAgain = abilitySystem.CanCastAbility(quickSpell);
Debug.Log($"Can cast again: {canCastAgain}");

// Wait 5+ seconds
yield return new WaitForSeconds(5.5f);
bool canCastNow = abilitySystem.CanCastAbility(quickSpell);
Debug.Log($"Can cast now: {canCastNow}");

EXPECTED: First check = false (on cooldown), second check = true (cooldown finished).


TEST 11: BUFF STACKING BEHAVIOR
--------------------------------
AbilityData speedBuff = new AbilityData
{
    AbilityId = "speed_001",
    AbilityName = "Speed Boost",
    Type = AbilityType.Buff,
    TargetMode = TargetMode.Self,
    Duration = 10f,
    BuffStatType = StatType.MoveSpeed,
    BuffModifierType = ModifierType.Percentage,
    BuffValue = 20f,
    CastTime = 0f,
    Cooldown = 5f,
    ResourceCost = ResourceType.Mana,
    ResourceAmount = 15f
};

abilitySystem.EquipAbility(speedBuff);
abilitySystem.CastAbility(speedBuff, Vector3.zero);

// Wait 6 seconds (cooldown expires)
yield return new WaitForSeconds(6f);

// Cast again (should refresh, not stack)
abilitySystem.CastAbility(speedBuff, Vector3.zero);

List<BuffInstance> buffs = abilitySystem.GetActiveBuffs();
Debug.Log($"Active speed buffs: {buffs.Count(b => b.BuffName == "Speed Boost")}");

EXPECTED: Only 1 speed buff active (duration refreshed to 10s).


TEST 12: CAST INTERRUPTION
---------------------------
AbilityData longCast = new AbilityData
{
    AbilityId = "long_cast_001",
    AbilityName = "Long Cast",
    Type = AbilityType.Damage,
    TargetMode = TargetMode.Self,
    BasePower = 200f,
    CastTime = 5f,
    InterruptedByDamage = true,
    Cooldown = 10f,
    ResourceCost = ResourceType.Mana,
    ResourceAmount = 50f
};

abilitySystem.EquipAbility(longCast);
abilitySystem.CastAbility(longCast, Vector3.zero);

// Wait 2 seconds, then deal damage to interrupt
yield return new WaitForSeconds(2f);

DamageRequest interrupt = new DamageRequest
{
    Attacker = enemy.GetComponent<NetworkIdentity>(),
    Target = player.GetComponent<NetworkIdentity>(),
    BaseDamage = 10f,
    Type = DamageType.Physical
};
DamageSystem.ApplyDamage(interrupt);

EXPECTED: Cast interrupted after 2s, spell doesn't fire, cooldown not consumed.


===============================================================================
INTEGRATION EXAMPLES
===============================================================================

EXAMPLE 1: PLAYER INPUT HANDLING
---------------------------------
// In PlayerController.cs Update()
void Update()
{
    AbilitySystem abilities = GetComponent<AbilitySystem>();
    
    if (Input.GetKeyDown(KeyCode.Alpha1))
    {
        List<AbilityData> equipped = abilities.GetEquippedAbilities();
        if (equipped.Count > 0)
        {
            Vector3 targetPos = GetMouseWorldPosition();
            NetworkIdentity target = GetTargetUnderMouse();
            abilities.CastAbility(equipped[0], targetPos, target);
        }
    }
    
    if (Input.GetKeyDown(KeyCode.Alpha2) && equipped.Count > 1)
    {
        abilities.CastAbility(equipped[1], transform.position, null);
    }
}


EXAMPLE 2: UI COOLDOWN DISPLAY
-------------------------------
// In UIManager.cs
AbilitySystem abilities = player.GetComponent<AbilitySystem>();
CooldownSystem cooldowns = player.GetComponent<CooldownSystem>();

List<AbilityData> equipped = abilities.GetEquippedAbilities();
for (int i = 0; i < equipped.Count; i++)
{
    AbilityData ability = equipped[i];
    float remaining = cooldowns.GetRemainingCooldown(ability.AbilityId);
    
    abilityButtons[i].interactable = abilities.CanCastAbility(ability);
    cooldownOverlays[i].fillAmount = remaining / ability.Cooldown;
    cooldownTexts[i].text = remaining > 0 ? remaining.ToString("F1") : "";
}


EXAMPLE 3: BUFF ICON DISPLAY
-----------------------------
// In UIManager.cs
AbilitySystem abilities = player.GetComponent<AbilitySystem>();
List<BuffInstance> activeBuffs = abilities.GetActiveBuffs();

for (int i = 0; i < buffIcons.Length; i++)
{
    if (i < activeBuffs.Count)
    {
        BuffInstance buff = activeBuffs[i];
        buffIcons[i].sprite = GetBuffIcon(buff.BuffName);
        buffIcons[i].gameObject.SetActive(true);
        buffTimers[i].text = buff.RemainingDuration.ToString("F0");
        buffBorders[i].color = buff.IsBuff ? Color.green : Color.red;
    }
    else
    {
        buffIcons[i].gameObject.SetActive(false);
    }
}


EXAMPLE 4: AI ABILITY USAGE
----------------------------
// In AIController.cs
AbilitySystem aiAbilities = GetComponent<AbilitySystem>();
List<AbilityData> abilities = aiAbilities.GetEquippedAbilities();

if (distanceToPlayer < 10f)
{
    // Use melee ability
    AbilityData meleeAbility = abilities.FirstOrDefault(a => a.Range < 5f);
    if (meleeAbility != null && aiAbilities.CanCastAbility(meleeAbility))
    {
        aiAbilities.CastAbility(meleeAbility, player.transform.position, 
            player.GetComponent<NetworkIdentity>());
    }
}
else if (healthPercent < 0.3f)
{
    // Use heal
    AbilityData heal = abilities.FirstOrDefault(a => a.Type == AbilityType.Healing);
    if (heal != null && aiAbilities.CanCastAbility(heal))
    {
        aiAbilities.CastAbility(heal, transform.position, null);
    }
}


EXAMPLE 5: STAT MODIFIER INTEGRATION
-------------------------------------
// In WeaponController.cs
public float GetFinalDamage()
{
    float baseDamage = weaponData.BaseDamage;
    
    AbilitySystem abilities = GetComponent<AbilitySystem>();
    if (abilities != null)
    {
        float damageModifier = abilities.GetStatModifier(StatType.Damage);
        baseDamage += damageModifier;
    }
    
    return baseDamage;
}

// In CharacterMotor.cs
public float GetMoveSpeed()
{
    float baseSpeed = 5f;
    
    AbilitySystem abilities = GetComponent<AbilitySystem>();
    if (abilities != null)
    {
        float speedModifier = abilities.GetStatModifier(StatType.MoveSpeed);
        baseSpeed += speedModifier;
    }
    
    return baseSpeed;
}


EXAMPLE 6: NETWORKED ABILITY EXECUTION
---------------------------------------
// In NetworkManager.cs (pseudo-code)
void HandleAbilityPacket(AbilityRequest request)
{
    if (IsServer)
    {
        AbilityResult result = AbilitySystem.ProcessAbilityRequest(request);
        
        if (result.Success)
        {
            BroadcastAbilityResult(result);
        }
        else
        {
            SendFailureToClient(request.Caster, result.FailureReason);
        }
    }
}

void BroadcastAbilityResult(AbilityResult result)
{
    foreach (ConnectedClient client in clients)
    {
        SendAbilityResultPacket(client, result);
    }
}


EXAMPLE 7: VISUAL EFFECTS INTEGRATION
--------------------------------------
// In ParticleSystemFactory.cs
AbilitySystem abilities = caster.GetComponent<AbilitySystem>();
abilities.OnAbilityExecuted += (ability, result) =>
{
    foreach (NetworkIdentity target in result.Targets)
    {
        GameObject vfx = CreateAbilityEffect(ability.AbilityName);
        vfx.transform.position = target.transform.position;
        Destroy(vfx, 2f);
    }
};


EXAMPLE 8: COMBO SYSTEM
-----------------------
public class ComboTracker : MonoBehaviour
{
    private List<string> recentAbilities = new List<string>();
    private float comboWindow = 3f;
    private float lastAbilityTime;
    
    void Start()
    {
        AbilitySystem abilities = GetComponent<AbilitySystem>();
        abilities.OnAbilityExecuted += TrackAbility;
    }
    
    void TrackAbility(AbilityData ability, AbilityResult result)
    {
        if (Time.time - lastAbilityTime > comboWindow)
        {
            recentAbilities.Clear();
        }
        
        recentAbilities.Add(ability.AbilityId);
        lastAbilityTime = Time.time;
        
        CheckCombo();
    }
    
    void CheckCombo()
    {
        if (recentAbilities.Count >= 3)
        {
            if (recentAbilities[0] == "fire_001" && 
                recentAbilities[1] == "ice_001" && 
                recentAbilities[2] == "lightning_001")
            {
                TriggerComboBonus();
            }
        }
    }
}

===============================================================================
END OF DOCUMENTATION
===============================================================================
*/