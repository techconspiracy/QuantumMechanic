// MODULE: Combat-04A (Chunk 1/3 - Foundation)
// FILE: ProjectileSystem.cs
// DEPENDENCIES: NetworkIdentity.cs, DamageSystem.cs, AbilitySystem.cs, ParticleSystemFactory.cs
// PURPOSE: Physics-based projectile system with 5 movement types and 4 collision behaviors
// STATUS: Foundation complete - ready for Chunk 2 (Spawning & Movement)

using UnityEngine;
using System.Collections.Generic;

namespace QuantumMechanic.Combat
{
    #region Enums

    /// <summary>
    /// Defines the movement behavior of a projectile.
    /// </summary>
    public enum ProjectileType
    {
        /// <summary>Linear trajectory at constant speed.</summary>
        Straight,
        /// <summary>Parabolic arc affected by gravity.</summary>
        Arcing,
        /// <summary>Seeks target with turn rate limits.</summary>
        Homing,
        /// <summary>Returns to caster after reaching max distance.</summary>
        Boomerang,
        /// <summary>Bounces between multiple targets.</summary>
        Chaining
    }

    /// <summary>
    /// Defines what happens when a projectile hits a target.
    /// </summary>
    public enum CollisionBehavior
    {
        /// <summary>Passes through targets, hits all in path.</summary>
        Pierce,
        /// <summary>Detonates on impact, AoE damage.</summary>
        Explode,
        /// <summary>Stops on first hit, single target.</summary>
        Stop,
        /// <summary>Ricochets off surfaces, continues traveling.</summary>
        Bounce
    }

    /// <summary>
    /// Defines how the projectile visually rotates during flight.
    /// </summary>
    public enum RotationMode
    {
        /// <summary>Always faces movement direction.</summary>
        FaceDirection,
        /// <summary>Spins around forward axis.</summary>
        Spin,
        /// <summary>Maintains spawn rotation.</summary>
        Fixed,
        /// <summary>Random rotation each frame.</summary>
        Random
    }

    #endregion

    #region Data Structures

    /// <summary>
    /// Configuration data for a projectile type.
    /// </summary>
    [System.Serializable]
    public class ProjectileData
    {
        [Header("Identity")]
        public string ProjectileId = "fireball_projectile";
        public string ProjectileName = "Fireball Projectile";

        [Header("Behavior")]
        public ProjectileType Type = ProjectileType.Straight;
        public CollisionBehavior CollisionMode = CollisionBehavior.Explode;

        [Header("Movement")]
        public float Speed = 20f;
        public float Lifetime = 5f;
        public float MaxDistance = 100f;

        [Header("Homing Parameters")]
        public float HomingStrength = 5f;
        public float HomingActivationDelay = 0.2f;

        [Header("Arcing Parameters")]
        public float ArcHeight = 5f;
        public float GravityMultiplier = 1f;

        [Header("Boomerang Parameters")]
        public float ReturnSpeed = 25f;
        public float ReturnDelay = 0.5f;

        [Header("Chaining Parameters")]
        public int MaxChainTargets = 3;
        public float ChainRange = 10f;

        [Header("Explosion Parameters")]
        public float ExplosionRadius = 5f;
        public int MaxExplosionTargets = 8;

        [Header("Pierce Parameters")]
        public int MaxPierceTargets = 5;

        [Header("Visual Parameters")]
        public GameObject ProjectilePrefab;
        public GameObject HitEffectPrefab;
        public GameObject ExplosionEffectPrefab;
        public Color ProjectileColor = Color.red;
        public float ProjectileScale = 1f;
        public bool UseTrail = true;
        public RotationMode Rotation = RotationMode.FaceDirection;
        public float SpinSpeed = 360f;

        [Header("Physics Parameters")]
        public float CollisionRadius = 0.5f;
        public LayerMask TargetLayers = -1;
        public LayerMask ObstacleLayers = -1;
        public bool IgnoreCaster = true;

        [Header("Damage Parameters")]
        public float Damage = 50f;
        public DamageType DamageType = DamageType.Fire;
        public bool CanCrit = true;
        public float KnockbackForce = 5f;
    }

    /// <summary>
    /// Runtime instance of an active projectile.
    /// </summary>
    public class ProjectileInstance
    {
        public string InstanceId;
        public ProjectileData Data;
        public GameObject GameObject;
        public Transform Transform;

        public NetworkIdentity Caster;
        public NetworkIdentity InitialTarget;
        public Vector3 SpawnPosition;
        public Vector3 CurrentVelocity;

        public float LifetimeRemaining;
        public float DistanceTraveled;
        public bool IsReturning;
        public int HitsRemaining;
        public List<NetworkIdentity> HitTargets;

        public TrailRenderer Trail;
        public ParticleSystem TravelEffect;

        /// <summary>
        /// Creates a new projectile instance with initialized collections.
        /// </summary>
        public ProjectileInstance()
        {
            InstanceId = System.Guid.NewGuid().ToString();
            HitTargets = new List<NetworkIdentity>();
        }
    }

    /// <summary>
    /// Request data for spawning a new projectile.
    /// </summary>
    public class ProjectileSpawnRequest
    {
        public ProjectileData ProjectileData;
        public NetworkIdentity Caster;
        public Vector3 SpawnPosition;
        public Vector3 Direction;
        public NetworkIdentity Target;
        public float DamageMultiplier = 1f;
        public AbilityData SourceAbility;
    }

    /// <summary>
    /// Result data from a projectile hitting a target.
    /// </summary>
    public class ProjectileHitResult
    {
        public ProjectileInstance Projectile;
        public NetworkIdentity Target;
        public Vector3 HitPosition;
        public Vector3 HitNormal;
        public DamageResult DamageResult;
        public bool ShouldDestroy;
    }

    #endregion

    /// <summary>
    /// Manages all projectile spawning, movement, collision detection, and visual effects.
    /// Supports 5 projectile types (Straight, Arcing, Homing, Boomerang, Chaining) and
    /// 4 collision behaviors (Pierce, Explode, Stop, Bounce).
    /// </summary>
    public class ProjectileSystem : MonoBehaviour
    {
        #region Singleton

        private static ProjectileSystem _instance;

        /// <summary>
        /// Singleton instance of the ProjectileSystem.
        /// </summary>
        public static ProjectileSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ProjectileSystem>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("ProjectileSystem");
                        _instance = go.AddComponent<ProjectileSystem>();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Inspector Fields

        [Header("Performance Settings")]
        [SerializeField] private int maxActiveProjectiles = 100;
        [SerializeField] private int maxProjectilesPerCaster = 20;
        [SerializeField] private int poolInitialSize = 10;

        [Header("Debug Settings")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool logProjectileEvents = false;

        #endregion

        #region Private State

        private List<ProjectileInstance> activeProjectiles = new List<ProjectileInstance>();
        private Dictionary<string, Queue<GameObject>> projectilePools = new Dictionary<string, Queue<GameObject>>();
        private Dictionary<string, GameObject> poolParents = new Dictionary<string, GameObject>();
        private DamageSystem damageSystem;
        private AbilitySystem abilitySystem;

        #endregion

        #region Events

        /// <summary>
        /// Invoked when a projectile is spawned.
        /// </summary>
        public event System.Action<ProjectileInstance> OnProjectileSpawned;

        /// <summary>
        /// Invoked when a projectile hits a target.
        /// </summary>
        public event System.Action<ProjectileHitResult> OnProjectileHit;

        /// <summary>
        /// Invoked when a projectile explodes (AoE damage).
        /// </summary>
        public event System.Action<ProjectileInstance, Vector3> OnProjectileExploded;

        /// <summary>
        /// Invoked when a projectile is destroyed.
        /// </summary>
        public event System.Action<ProjectileInstance> OnProjectileDestroyed;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Initializes the singleton and caches system references.
        /// </summary>
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            damageSystem = DamageSystem.Instance;
            abilitySystem = AbilitySystem.Instance;

            if (logProjectileEvents)
            {
                Debug.Log("[ProjectileSystem] Initialized successfully.");
            }
        }

        /// <summary>
        /// Updates all active projectiles each frame.
        /// </summary>
        private void Update()
        {
            // Iterate backwards to safely remove projectiles during iteration
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                ProjectileInstance projectile = activeProjectiles[i];

                // Update lifetime
                projectile.LifetimeRemaining -= Time.deltaTime;
                if (projectile.LifetimeRemaining <= 0f)
                {
                    DestroyProjectile(projectile);
                    continue;
                }

                // Check max distance
                if (projectile.DistanceTraveled >= projectile.Data.MaxDistance && !projectile.IsReturning)
                {
                    if (projectile.Data.Type == ProjectileType.Boomerang)
                    {
                        projectile.IsReturning = true;
                    }
                    else
                    {
                        DestroyProjectile(projectile);
                        continue;
                    }
                }

                // Movement update will be implemented in Chunk 2
                // UpdateProjectile(projectile);

                // Collision detection will be implemented in Chunk 3
                // CheckCollisions(projectile);
            }
        }

        #endregion

        #region Public API - Management

        /// <summary>
        /// Gets all currently active projectiles.
        /// </summary>
        /// <returns>List of active projectile instances.</returns>
        public List<ProjectileInstance> GetActiveProjectiles()
        {
            return new List<ProjectileInstance>(activeProjectiles);
        }

        /// <summary>
        /// Gets all projectiles spawned by a specific caster.
        /// </summary>
        /// <param name="caster">The caster to filter by.</param>
        /// <returns>List of projectiles from the specified caster.</returns>
        public List<ProjectileInstance> GetProjectilesFromCaster(NetworkIdentity caster)
        {
            List<ProjectileInstance> result = new List<ProjectileInstance>();
            foreach (ProjectileInstance projectile in activeProjectiles)
            {
                if (projectile.Caster == caster)
                {
                    result.Add(projectile);
                }
            }
            return result;
        }

        /// <summary>
        /// Gets the total number of active projectiles.
        /// </summary>
        /// <returns>Count of active projectiles.</returns>
        public int GetActiveProjectileCount()
        {
            return activeProjectiles.Count;
        }

        /// <summary>
        /// Gets the number of projectiles spawned by a specific caster.
        /// </summary>
        /// <param name="caster">The caster to count projectiles for.</param>
        /// <returns>Count of projectiles from the specified caster.</returns>
        public int GetProjectileCountFromCaster(NetworkIdentity caster)
        {
            int count = 0;
            foreach (ProjectileInstance projectile in activeProjectiles)
            {
                if (projectile.Caster == caster)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Destroys all projectiles spawned by a specific caster.
        /// </summary>
        /// <param name="caster">The caster whose projectiles should be destroyed.</param>
        public void DestroyAllProjectilesFromCaster(NetworkIdentity caster)
        {
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                if (activeProjectiles[i].Caster == caster)
                {
                    DestroyProjectile(activeProjectiles[i]);
                }
            }
        }

        /// <summary>
        /// Destroys a specific projectile instance.
        /// </summary>
        /// <param name="projectile">The projectile to destroy.</param>
        public void DestroyProjectile(ProjectileInstance projectile)
        {
            if (projectile == null) return;

            activeProjectiles.Remove(projectile);

            if (projectile.GameObject != null)
            {
                ReturnToPool(projectile.GameObject, projectile.Data.ProjectileId);
            }

            OnProjectileDestroyed?.Invoke(projectile);

            if (logProjectileEvents)
            {
                Debug.Log($"[ProjectileSystem] Destroyed projectile: {projectile.Data.ProjectileName}");
            }
        }

        #endregion

        #region Object Pooling (Private)

        /// <summary>
        /// Returns a projectile GameObject to the pool for reuse.
        /// </summary>
        /// <param name="projectile">The projectile GameObject to return.</param>
        /// <param name="poolId">The pool identifier.</param>
        private void ReturnToPool(GameObject projectile, string poolId)
        {
            projectile.SetActive(false);
            projectile.transform.position = Vector3.zero;

            if (poolParents.ContainsKey(poolId))
            {
                projectile.transform.SetParent(poolParents[poolId].transform);
            }

            if (projectilePools.ContainsKey(poolId))
            {
                projectilePools[poolId].Enqueue(projectile);
            }
            else
            {
                Destroy(projectile);
            }
        }

        #endregion
    }
}

/*
CHUNK 1/3 STATUS: ✅ COMPLETE
================================

IMPLEMENTED:
✓ All data structures (ProjectileData, ProjectileInstance, ProjectileSpawnRequest, ProjectileHitResult)
✓ All enums (ProjectileType, CollisionBehavior, RotationMode)
✓ Singleton pattern with Instance property
✓ Inspector fields for configuration
✓ Private state management (activeProjectiles list, pools, system references)
✓ Event declarations (4 events)
✓ Awake() for singleton initialization
✓ Update() skeleton that iterates projectiles and handles lifetime/distance
✓ Public management API (GetActiveProjectiles, GetProjectilesFromCaster, etc.)
✓ DestroyProjectile() implementation
✓ ReturnToPool() helper for object pooling

READY FOR CHUNK 2:
- SpawnProjectile() methods
- UpdateProjectile() dispatcher
- Movement implementations (UpdateStraight, UpdateHoming, UpdateArcing, UpdateBoomerang, UpdateChaining)
- ApplyRotation() visual logic
- SetupTrail() configuration

Line Count: ~380 lines (includes comments and XML docs)
Compilation Status: ✅ Should compile (references DamageSystem, AbilitySystem, NetworkIdentity)
*/