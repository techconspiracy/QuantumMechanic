// MODULE: Combat-03C
// FILE: CastingSystem.cs
// DEPENDENCIES: NetworkIdentity.cs
// INTEGRATES WITH: AbilitySystem.cs (future), PlayerController.cs (future), DamageSystem.cs (existing), UIManager.cs (future)
// PURPOSE: Manages cast time progression with movement/damage interruption and server validation

using UnityEngine;

namespace QuantumMechanic.Combat
{
    /// <summary>
    /// Reasons why a cast can be interrupted
    /// </summary>
    public enum CastInterruptReason
    {
        Manual,
        Movement,
        Damage,
        Death,
        Stunned
    }

    /// <summary>
    /// Represents an active casting operation
    /// </summary>
    public class CastInstance
    {
        public string AbilityId;
        public float CastTime;
        public float ElapsedTime;
        public bool CanCastWhileMoving;
        public bool InterruptedByDamage;
        public NetworkIdentity Caster;
        public Vector3 TargetPosition;
        public NetworkIdentity TargetEntity;
    }

    /// <summary>
    /// Manages ability cast times with interruption mechanics, input locking, and server validation.
    /// Supports movement interruption, damage interruption, manual cancellation, and instant casts.
    /// </summary>
    /// <example>
    /// Setup:
    /// 1. Attach to player GameObject
    /// 2. Assign NetworkIdentity reference
    /// 3. Subscribe to events for UI updates
    /// 
    /// Usage Example:
    /// <code>
    /// // Start a 2-second cast for a fireball
    /// castingSystem.StartCast("fireball_001", 2f, false, true);
    /// 
    /// // Check casting state
    /// if (castingSystem.IsCasting())
    /// {
    ///     float progress = castingSystem.GetCastProgress(); // 0.0 to 1.0
    ///     Debug.Log($"Casting progress: {progress * 100f}%");
    /// }
    /// 
    /// // Cancel cast manually (e.g., on ESC press)
    /// if (Input.GetKeyDown(KeyCode.Escape))
    /// {
    ///     castingSystem.CancelCast(CastInterruptReason.Manual);
    /// }
    /// 
    /// // Listen for cast events
    /// castingSystem.OnCastStarted += (abilityId) => Debug.Log($"Started casting {abilityId}");
    /// castingSystem.OnCastCompleted += (abilityId) => Debug.Log($"Cast complete: {abilityId}");
    /// castingSystem.OnCastInterrupted += (abilityId, reason) => Debug.Log($"Cast interrupted: {reason}");
    /// castingSystem.OnCastProgress += (abilityId, progress) => UpdateCastBar(progress);
    /// </code>
    /// </example>
    public class CastingSystem : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("NetworkIdentity for server validation")]
        [SerializeField] private NetworkIdentity identity;

        [Header("Movement Tracking")]
        [Tooltip("Distance threshold for movement detection (meters)")]
        [SerializeField] private float movementThreshold = 0.1f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        // Active cast state
        private CastInstance currentCast;
        private Vector3 lastPosition;
        private bool isInputLocked;

        // Events
        /// <summary>Fired when a cast begins</summary>
        public event System.Action<string> OnCastStarted;

        /// <summary>Fired when a cast completes successfully</summary>
        public event System.Action<string> OnCastCompleted;

        /// <summary>Fired when a cast is interrupted</summary>
        public event System.Action<string, CastInterruptReason> OnCastInterrupted;

        /// <summary>Fired during casting for UI updates (abilityId, progress 0-1)</summary>
        public event System.Action<string, float> OnCastProgress;

        private void Awake()
        {
            if (identity == null)
            {
                identity = GetComponent<NetworkIdentity>();
            }
        }

        private void Update()
        {
            if (currentCast == null) return;

            // Check for movement interruption
            if (!currentCast.CanCastWhileMoving)
            {
                float distanceMoved = Vector3.Distance(transform.position, lastPosition);
                if (distanceMoved > movementThreshold)
                {
                    if (debugMode)
                    {
                        Debug.Log($"[CastingSystem] Cast interrupted by movement: {distanceMoved:F3}m");
                    }
                    CancelCast(CastInterruptReason.Movement);
                    return;
                }
            }

            // Update cast progress
            currentCast.ElapsedTime += Time.deltaTime;
            float progress = GetCastProgress();

            // Fire progress event for UI
            OnCastProgress?.Invoke(currentCast.AbilityId, progress);

            // Check for completion
            if (currentCast.ElapsedTime >= currentCast.CastTime)
            {
                CompleteCast();
            }
        }

        /// <summary>
        /// Starts a new cast. Instant casts (castTime = 0) complete immediately.
        /// </summary>
        /// <param name="abilityId">Unique identifier for the ability</param>
        /// <param name="castTime">Duration of the cast in seconds (0 for instant)</param>
        /// <param name="canMoveWhileCasting">If false, movement interrupts the cast</param>
        /// <param name="interruptedByDamage">If true, taking damage interrupts the cast</param>
        public void StartCast(string abilityId, float castTime, bool canMoveWhileCasting, bool interruptedByDamage)
        {
            // Cancel existing cast
            if (currentCast != null)
            {
                CancelCast(CastInterruptReason.Manual);
            }

            // Handle instant cast
            if (castTime <= 0f)
            {
                if (debugMode)
                {
                    Debug.Log($"[CastingSystem] Instant cast: {abilityId}");
                }
                OnCastStarted?.Invoke(abilityId);
                OnCastCompleted?.Invoke(abilityId);
                return;
            }

            // Create cast instance
            currentCast = new CastInstance
            {
                AbilityId = abilityId,
                CastTime = castTime,
                ElapsedTime = 0f,
                CanCastWhileMoving = canMoveWhileCasting,
                InterruptedByDamage = interruptedByDamage,
                Caster = identity
            };

            lastPosition = transform.position;
            isInputLocked = true;

            if (debugMode)
            {
                Debug.Log($"[CastingSystem] Started cast: {abilityId} ({castTime}s)");
            }

            OnCastStarted?.Invoke(abilityId);
            OnCastProgress?.Invoke(abilityId, 0f);
        }

        /// <summary>
        /// Starts a cast with targeting information
        /// </summary>
        /// <param name="abilityId">Unique identifier for the ability</param>
        /// <param name="castTime">Duration of the cast in seconds</param>
        /// <param name="canMoveWhileCasting">If false, movement interrupts the cast</param>
        /// <param name="interruptedByDamage">If true, taking damage interrupts the cast</param>
        /// <param name="targetPosition">World position target</param>
        /// <param name="targetEntity">Target entity (optional)</param>
        public void StartCast(string abilityId, float castTime, bool canMoveWhileCasting, 
                            bool interruptedByDamage, Vector3 targetPosition, NetworkIdentity targetEntity = null)
        {
            StartCast(abilityId, castTime, canMoveWhileCasting, interruptedByDamage);

            if (currentCast != null)
            {
                currentCast.TargetPosition = targetPosition;
                currentCast.TargetEntity = targetEntity;
            }
        }

        /// <summary>
        /// Cancels the current cast with a specified reason
        /// </summary>
        /// <param name="reason">Reason for interruption</param>
        public void CancelCast(CastInterruptReason reason)
        {
            if (currentCast == null) return;

            string abilityId = currentCast.AbilityId;

            if (debugMode)
            {
                Debug.Log($"[CastingSystem] Cast cancelled: {abilityId} (Reason: {reason})");
            }

            currentCast = null;
            isInputLocked = false;

            OnCastInterrupted?.Invoke(abilityId, reason);
        }

        /// <summary>
        /// Completes the current cast successfully
        /// </summary>
        private void CompleteCast()
        {
            if (currentCast == null) return;

            string abilityId = currentCast.AbilityId;

            // Server validation (placeholder for future network integration)
            if (identity != null && identity.HasAuthority)
            {
                // Client predicted completion - server will validate
                if (debugMode)
                {
                    Debug.Log($"[CastingSystem] Cast completed (client): {abilityId}");
                }
            }
            else
            {
                // Server confirmed completion
                if (debugMode)
                {
                    Debug.Log($"[CastingSystem] Cast completed (server): {abilityId}");
                }
            }

            currentCast = null;
            isInputLocked = false;

            OnCastProgress?.Invoke(abilityId, 1f);
            OnCastCompleted?.Invoke(abilityId);
        }

        /// <summary>
        /// Checks if currently casting an ability
        /// </summary>
        /// <returns>True if a cast is in progress</returns>
        public bool IsCasting()
        {
            return currentCast != null;
        }

        /// <summary>
        /// Gets the current cast progress as a percentage
        /// </summary>
        /// <returns>Progress from 0.0 to 1.0, or 0 if not casting</returns>
        public float GetCastProgress()
        {
            if (currentCast == null) return 0f;
            return Mathf.Clamp01(currentCast.ElapsedTime / currentCast.CastTime);
        }

        /// <summary>
        /// Gets the current active cast instance
        /// </summary>
        /// <returns>Current cast or null if not casting</returns>
        public CastInstance GetCurrentCast()
        {
            return currentCast;
        }

        /// <summary>
        /// Checks if player input is locked during casting
        /// </summary>
        /// <returns>True if input should be blocked</returns>
        public bool IsInputLocked()
        {
            return isInputLocked;
        }

        /// <summary>
        /// Called by DamageSystem when this entity takes damage.
        /// Interrupts cast if InterruptedByDamage is true.
        /// </summary>
        /// <param name="damageAmount">Amount of damage taken</param>
        public void OnDamageTaken(float damageAmount)
        {
            if (currentCast == null) return;

            if (currentCast.InterruptedByDamage)
            {
                if (debugMode)
                {
                    Debug.Log($"[CastingSystem] Cast interrupted by damage: {damageAmount}");
                }
                CancelCast(CastInterruptReason.Damage);
            }
        }

        /// <summary>
        /// Forces cast completion from server (network validation)
        /// </summary>
        /// <param name="abilityId">Ability ID to validate</param>
        public void ServerCompleteCast(string abilityId)
        {
            if (currentCast == null || currentCast.AbilityId != abilityId)
            {
                if (debugMode)
                {
                    Debug.LogWarning($"[CastingSystem] Server completion mismatch: expected {currentCast?.AbilityId}, got {abilityId}");
                }
                return;
            }

            if (debugMode)
            {
                Debug.Log($"[CastingSystem] Server validated cast: {abilityId}");
            }

            CompleteCast();
        }

        /// <summary>
        /// Forces cast interruption from server (anti-cheat)
        /// </summary>
        /// <param name="reason">Reason for server interruption</param>
        public void ServerInterruptCast(CastInterruptReason reason)
        {
            if (currentCast == null) return;

            if (debugMode)
            {
                Debug.Log($"[CastingSystem] Server interrupted cast: {reason}");
            }

            CancelCast(reason);
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugMode || currentCast == null) return;

            // Draw cast progress sphere
            Gizmos.color = Color.Lerp(Color.yellow, Color.green, GetCastProgress());
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.3f);

            // Draw target position if set
            if (currentCast.TargetPosition != Vector3.zero)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(currentCast.TargetPosition, 0.5f);
                Gizmos.DrawLine(transform.position, currentCast.TargetPosition);
            }
        }
    }
}

/*
TESTING GUIDE:
=============

1. Basic Setup:
   - Attach CastingSystem to player GameObject
   - Assign NetworkIdentity in inspector
   - Enable Debug Mode for console logging

2. Test Instant Cast:
   castingSystem.StartCast("instant_heal", 0f, true, false);
   // Should fire OnCastStarted and OnCastCompleted immediately

3. Test Normal Cast:
   castingSystem.StartCast("fireball", 2.5f, false, true);
   // Should take 2.5 seconds to complete
   // Progress should update 60 times per second

4. Test Movement Interruption:
   castingSystem.StartCast("long_cast", 5f, false, true);
   // Move the character - cast should interrupt with Movement reason

5. Test Damage Interruption:
   castingSystem.StartCast("fireball", 2f, false, true);
   castingSystem.OnDamageTaken(10f);
   // Cast should interrupt with Damage reason

6. Test Manual Cancel:
   castingSystem.StartCast("channeled_spell", 10f, true, false);
   castingSystem.CancelCast(CastInterruptReason.Manual);
   // Cast should interrupt with Manual reason

7. Test Cast While Moving:
   castingSystem.StartCast("instant_shot", 1f, true, false);
   // Move the character - cast should NOT interrupt

8. Test Progress Tracking:
   castingSystem.OnCastProgress += (id, progress) => 
   {
       Debug.Log($"{id}: {progress * 100f}%");
   };
   castingSystem.StartCast("test", 3f, true, false);
   // Should print progress updates every frame

9. Test Input Locking:
   castingSystem.StartCast("meditation", 5f, false, false);
   Debug.Log($"Input locked: {castingSystem.IsInputLocked()}"); // Should be true
   // Wait for completion
   Debug.Log($"Input locked: {castingSystem.IsInputLocked()}"); // Should be false

10. Network Testing (with future NetworkManager):
    // Client side
    castingSystem.StartCast("heal", 2f, false, true);
    // Server validates after 2 seconds
    castingSystem.ServerCompleteCast("heal");

INTEGRATION EXAMPLES:
====================

// Example: PlayerController movement check
void Update()
{
    if (!castingSystem.IsInputLocked())
    {
        HandleMovementInput();
    }
}

// Example: DamageSystem integration
public void ApplyDamage(DamageRequest request)
{
    // ... apply damage ...
    CastingSystem casting = target.GetComponent<CastingSystem>();
    if (casting != null)
    {
        casting.OnDamageTaken(finalDamage);
    }
}

// Example: UI Cast Bar
void Start()
{
    castingSystem.OnCastStarted += ShowCastBar;
    castingSystem.OnCastCompleted += HideCastBar;
    castingSystem.OnCastInterrupted += (id, reason) => HideCastBar(id);
    castingSystem.OnCastProgress += UpdateCastBar;
}

void UpdateCastBar(string abilityId, float progress)
{
    castBarFillImage.fillAmount = progress;
    castBarText.text = $"{abilityId} ({progress * 100f:F0}%)";
}

// Example: AbilitySystem integration (future)
public void CastAbility(AbilityData ability)
{
    if (castingSystem.IsCasting()) return;
    
    castingSystem.OnCastCompleted += OnAbilityCastComplete;
    castingSystem.StartCast(ability.AbilityId, ability.CastTime, 
                          ability.CanCastWhileMoving, ability.InterruptedByDamage);
}

void OnAbilityCastComplete(string abilityId)
{
    castingSystem.OnCastCompleted -= OnAbilityCastComplete;
    ExecuteAbility(abilityId);
}
*/