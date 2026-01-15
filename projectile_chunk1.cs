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
#region Public API - Spawning

        /// <summary>
        /// Spawns a projectile based on the provided request.
        /// </summary>
        public ProjectileInstance SpawnProjectile(ProjectileSpawnRequest request)
        {
            // Check max projectile limits
            if (activeProjectiles.Count >= maxActiveProjectiles)
            {
                if (logProjectileEvents)
                    Debug.LogWarning($"[ProjectileSystem] Max active projectiles ({maxActiveProjectiles}) reached. Cannot spawn new projectile.");
                return null;
            }

            // Check per-caster limit
            if (request.Caster != null)
            {
                int casterProjectileCount = activeProjectiles.Count(p => p.Caster == request.Caster);
                if (casterProjectileCount >= maxProjectilesPerCaster)
                {
                    if (logProjectileEvents)
                        Debug.LogWarning($"[ProjectileSystem] Max projectiles per caster ({maxProjectilesPerCaster}) reached for {request.Caster.name}.");
                    return null;
                }
            }

            // Get pooled or new GameObject
            GameObject projectileObj = GetPooledProjectile(request.Data);
            if (projectileObj == null)
            {
                Debug.LogError("[ProjectileSystem] Failed to get pooled projectile.");
                return null;
            }

            // Set initial position and rotation
            projectileObj.transform.position = request.Origin;
            projectileObj.transform.rotation = Quaternion.LookRotation(request.Direction);

            // Create ProjectileInstance
            ProjectileInstance instance = new ProjectileInstance
            {
                GameObject = projectileObj,
                Transform = projectileObj.transform,
                Data = request.Data,
                Caster = request.Caster,
                InitialTarget = request.Target,
                CurrentTarget = request.Target,
                Direction = request.Direction.normalized,
                CurrentVelocity = request.Direction.normalized * request.Data.Speed,
                LifetimeRemaining = request.Data.Lifetime,
                DistanceTraveled = 0f,
                IsReturning = false,
                ChainCount = 0,
                HitTargets = new List<NetworkIdentity>()
            };

            // Set HitsRemaining based on collision behavior
            switch (request.Data.CollisionMode)
            {
                case CollisionBehavior.Pierce:
                    instance.HitsRemaining = request.Data.MaxPierceTargets;
                    break;
                case CollisionBehavior.Chain:
                    instance.HitsRemaining = request.Data.MaxChainTargets;
                    break;
                default:
                    instance.HitsRemaining = 1;
                    break;
            }

            // Setup visual components
            SetupTrail(instance);

            // Apply scale and color to renderer if present
            Renderer renderer = projectileObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = request.Data.ProjectileColor;
            }
            projectileObj.transform.localScale = Vector3.one * request.Data.ProjectileScale;

            // Add to active projectiles
            activeProjectiles.Add(instance);

            // Invoke event
            OnProjectileSpawned?.Invoke(instance);

            if (logProjectileEvents)
                Debug.Log($"[ProjectileSystem] Spawned projectile {request.Data.ProjectileId} from {request.Caster?.name ?? "Unknown"}");

            return instance;
        }

        /// <summary>
        /// Spawns a projectile from an ability definition.
        /// </summary>
        public ProjectileInstance SpawnProjectileFromAbility(AbilityData ability, NetworkIdentity caster, Vector3 origin, Vector3 direction, NetworkIdentity target = null)
        {
            if (ability == null)
            {
                Debug.LogError("[ProjectileSystem] Cannot spawn projectile from null ability.");
                return null;
            }

            if (ability.ProjectileData == null)
            {
                Debug.LogError($"[ProjectileSystem] Ability {ability.abilityName} has no projectile data.");
                return null;
            }

            ProjectileSpawnRequest request = new ProjectileSpawnRequest
            {
                Data = ability.ProjectileData,
                Caster = caster,
                Origin = origin,
                Direction = direction,
                Target = target
            };

            return SpawnProjectile(request);
        }

        /// <summary>
        /// Gets a pooled projectile GameObject or creates a new one.
        /// </summary>
        private GameObject GetPooledProjectile(ProjectileData data)
        {
            if (data == null)
            {
                Debug.LogError("[ProjectileSystem] Cannot get pooled projectile for null data.");
                return null;
            }

            // Check if pool exists, if not initialize it
            if (!projectilePools.ContainsKey(data.ProjectileId))
            {
                InitializePool(data, poolInitialSize);
            }

            Queue<GameObject> pool = projectilePools[data.ProjectileId];

            // Try to get from pool
            if (pool.Count > 0)
            {
                GameObject pooled = pool.Dequeue();
                pooled.SetActive(true);
                return pooled;
            }

            // Pool empty, create new
            if (data.ProjectilePrefab != null)
            {
                GameObject newProjectile = Instantiate(data.ProjectilePrefab);
                if (logProjectileEvents)
                    Debug.Log($"[ProjectileSystem] Pool empty, created new projectile for {data.ProjectileId}");
                return newProjectile;
            }

            // Fallback: create primitive sphere
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = Vector3.one * data.ProjectileScale;
            Renderer renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = data.ProjectileColor;
            }

            if (logProjectileEvents)
                Debug.Log($"[ProjectileSystem] Created fallback sphere projectile for {data.ProjectileId}");

            return sphere;
        }

        #endregion

        #region Projectile Update (Private)

        /// <summary>
        /// Updates projectile position based on its type.
        /// Called from Update() loop.
        /// </summary>
        private void UpdateProjectile(ProjectileInstance projectile)
        {
            if (projectile == null || projectile.GameObject == null)
                return;

            switch (projectile.Data.Type)
            {
                case ProjectileType.Straight:
                    UpdateStraight(projectile);
                    break;
                case ProjectileType.Homing:
                    UpdateHoming(projectile);
                    break;
                case ProjectileType.Arcing:
                    UpdateArcing(projectile);
                    break;
                case ProjectileType.Boomerang:
                    UpdateBoomerang(projectile);
                    break;
                case ProjectileType.Chaining:
                    UpdateChaining(projectile);
                    break;
                default:
                    UpdateStraight(projectile);
                    break;
            }
        }

        /// <summary>
        /// Updates straight projectile (linear velocity).
        /// </summary>
        private void UpdateStraight(ProjectileInstance projectile)
        {
            Vector3 movement = projectile.CurrentVelocity * Time.deltaTime;
            projectile.Transform.position += movement;
            projectile.DistanceTraveled += movement.magnitude;
            ApplyRotation(projectile);
        }

        /// <summary>
        /// Updates homing projectile (RotateTowards target).
        /// </summary>
        private void UpdateHoming(ProjectileInstance projectile)
        {
            // Only home after activation delay
            if (projectile.InitialTarget != null &&
                projectile.LifetimeRemaining < projectile.Data.Lifetime - projectile.Data.HomingActivationDelay)
            {
                Vector3 targetPos = projectile.InitialTarget.transform.position;
                Vector3 targetDir = (targetPos - projectile.Transform.position).normalized;
                Vector3 currentDir = projectile.CurrentVelocity.normalized;

                Vector3 newDir = Vector3.RotateTowards(currentDir, targetDir,
                    projectile.Data.HomingStrength * Time.deltaTime, 0f);

                projectile.CurrentVelocity = newDir * projectile.Data.Speed;
            }

            Vector3 movement = projectile.CurrentVelocity * Time.deltaTime;
            projectile.Transform.position += movement;
            projectile.DistanceTraveled += movement.magnitude;
            projectile.Transform.rotation = Quaternion.LookRotation(projectile.CurrentVelocity);
        }

        /// <summary>
        /// Updates arcing projectile (applies gravity).
        /// </summary>
        private void UpdateArcing(ProjectileInstance projectile)
        {
            // Apply gravity
            projectile.CurrentVelocity += Physics.gravity * projectile.Data.GravityMultiplier * Time.deltaTime;

            Vector3 movement = projectile.CurrentVelocity * Time.deltaTime;
            projectile.Transform.position += movement;
            projectile.DistanceTraveled += movement.magnitude;
            projectile.Transform.rotation = Quaternion.LookRotation(projectile.CurrentVelocity);
        }

        /// <summary>
        /// Updates boomerang projectile (returns to caster).
        /// </summary>
        private void UpdateBoomerang(ProjectileInstance projectile)
        {
            // Check if should start returning
            if (!projectile.IsReturning && projectile.DistanceTraveled >= projectile.Data.MaxDistance)
            {
                projectile.IsReturning = true;
                if (logProjectileEvents)
                    Debug.Log($"[ProjectileSystem] Boomerang projectile {projectile.Data.ProjectileId} starting return.");
            }

            if (projectile.IsReturning && projectile.Caster != null)
            {
                Vector3 casterPos = projectile.Caster.transform.position;
                Vector3 returnDir = (casterPos - projectile.Transform.position).normalized;
                projectile.CurrentVelocity = returnDir * projectile.Data.ReturnSpeed;

                // Check if returned to caster (within 1 unit)
                if (Vector3.Distance(projectile.Transform.position, casterPos) < 1f)
                {
                    if (logProjectileEvents)
                        Debug.Log($"[ProjectileSystem] Boomerang projectile {projectile.Data.ProjectileId} returned to caster.");
                    DestroyProjectile(projectile);
                    return;
                }
            }

            Vector3 movement = projectile.CurrentVelocity * Time.deltaTime;
            projectile.Transform.position += movement;
            projectile.DistanceTraveled += movement.magnitude;
            ApplyRotation(projectile);
        }

        /// <summary>
        /// Updates chaining projectile (finds next target).
        /// </summary>
        private void UpdateChaining(ProjectileInstance projectile)
        {
            // For now, just move straight - full chain logic in Chunk 3 collision detection
            UpdateStraight(projectile);
        }

        #endregion

        #region Visual Effects (Private)

        /// <summary>
        /// Applies rotation based on RotationMode.
        /// </summary>
        private void ApplyRotation(ProjectileInstance projectile)
        {
            if (projectile == null || projectile.Transform == null)
                return;

            switch (projectile.Data.Rotation)
            {
                case RotationMode.FaceDirection:
                    if (projectile.CurrentVelocity != Vector3.zero)
                        projectile.Transform.rotation = Quaternion.LookRotation(projectile.CurrentVelocity);
                    break;

                case RotationMode.Spin:
                    projectile.Transform.Rotate(Vector3.forward, projectile.Data.SpinSpeed * Time.deltaTime);
                    break;

                case RotationMode.Fixed:
                    // Do nothing - maintain initial rotation
                    break;

                case RotationMode.Random:
                    projectile.Transform.rotation = Random.rotation;
                    break;
            }
        }

        /// <summary>
        /// Configures the TrailRenderer component.
        /// </summary>
        private void SetupTrail(ProjectileInstance projectile)
        {
            if (projectile == null || projectile.GameObject == null)
                return;

            if (!projectile.Data.UseTrail)
                return;

            TrailRenderer trail = projectile.GameObject.GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = projectile.GameObject.AddComponent<TrailRenderer>();
            }

            trail.time = 0.5f;
            trail.startWidth = projectile.Data.ProjectileScale * 0.3f;
            trail.endWidth = 0f;
            trail.startColor = projectile.Data.ProjectileColor;
            trail.endColor = new Color(
                projectile.Data.ProjectileColor.r,
                projectile.Data.ProjectileColor.g,
                projectile.Data.ProjectileColor.b,
                0f
            );

            projectile.Trail = trail;

            if (logProjectileEvents)
                Debug.Log($"[ProjectileSystem] Setup trail for projectile {projectile.Data.ProjectileId}");
        }

        /// <summary>
        /// Initializes the object pool for a projectile type.
        /// </summary>
        private void InitializePool(ProjectileData data, int initialSize)
        {
            if (data == null)
            {
                Debug.LogError("[ProjectileSystem] Cannot initialize pool for null data.");
                return;
            }

            if (projectilePools.ContainsKey(data.ProjectileId))
            {
                if (logProjectileEvents)
                    Debug.LogWarning($"[ProjectileSystem] Pool for {data.ProjectileId} already exists.");
                return;
            }

            // Create pool parent object
            GameObject poolParent = new GameObject($"Pool_{data.ProjectileId}");
            poolParent.transform.SetParent(transform);
            poolParents[data.ProjectileId] = poolParent;

            Queue<GameObject> pool = new Queue<GameObject>();

            for (int i = 0; i < initialSize; i++)
            {
                GameObject projectile;
                if (data.ProjectilePrefab != null)
                {
                    projectile = Instantiate(data.ProjectilePrefab, poolParent.transform);
                }
                else
                {
                    projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    projectile.transform.SetParent(poolParent.transform);
                    projectile.transform.localScale = Vector3.one * data.ProjectileScale;
                    
                    Renderer renderer = projectile.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = data.ProjectileColor;
                    }
                }

                projectile.name = $"{data.ProjectileId}_Pooled_{i}";
                projectile.SetActive(false);
                pool.Enqueue(projectile);
            }

            projectilePools[data.ProjectileId] = pool;

            if (logProjectileEvents)
                Debug.Log($"[ProjectileSystem] Initialized pool for {data.ProjectileId} with {initialSize} projectiles.");
        }

        #endregion

        #region Collision Detection (Private)

        /// <summary>
        /// Checks for collisions with targets in the scene.
        /// Called from Update() loop.
        /// </summary>
        /// <param name="projectile">The projectile instance to check collisions for.</param>
        private void CheckCollisions(ProjectileInstance projectile)
        {
            if (projectile == null || projectile.GameObject == null)
                return;

            // Get all valid targets
            List<NetworkIdentity> validTargets = GetValidTargets(projectile);

            // Check collision with each target
            foreach (NetworkIdentity target in validTargets)
            {
                if (target == null || target.gameObject == null)
                    continue;

                // Calculate distance to target
                float distance = Vector3.Distance(projectile.Transform.position, target.transform.position);

                // Check if within collision radius
                if (distance <= projectile.Data.CollisionRadius)
                {
                    HandleCollision(projectile, target);
                    return; // Exit after handling collision
                }
            }
        }

        /// <summary>
        /// Handles collision with a single target.
        /// Processes collision behavior (Pierce, Bounce, Explode, Chain, Stop) and invokes events.
        /// </summary>
        /// <param name="projectile">The projectile that collided.</param>
        /// <param name="target">The target that was hit.</param>
        private void HandleCollision(ProjectileInstance projectile, NetworkIdentity target)
        {
            if (projectile == null || target == null)
                return;

            // Add target to hit list
            projectile.HitTargets.Add(target);

            // Create hit result
            ProjectileHitResult result = new ProjectileHitResult
            {
                Projectile = projectile,
                HitTarget = target,
                HitPosition = target.transform.position,
                HitNormal = (target.transform.position - projectile.Transform.position).normalized,
                Damage = projectile.Data.Damage,
                IsExplosion = false
            };

            // Invoke hit event
            OnProjectileHit?.Invoke(result);

            // Decrement hits remaining
            projectile.HitsRemaining--;

            if (logProjectileEvents)
                Debug.Log($"[ProjectileSystem] Projectile hit {target.name} at {target.transform.position}. Hits remaining: {projectile.HitsRemaining}");

            // Handle collision behavior
            switch (projectile.Data.CollisionBehavior)
            {
                case CollisionBehavior.Pierce:
                    if (projectile.HitsRemaining <= 0)
                    {
                        DestroyProjectile(projectile);
                    }
                    // else continue moving through target
                    break;

                case CollisionBehavior.Bounce:
                    if (projectile.HitsRemaining <= 0)
                    {
                        DestroyProjectile(projectile);
                    }
                    else
                    {
                        // Simple reflection off target
                        Vector3 hitDir = (projectile.Transform.position - target.transform.position).normalized;
                        projectile.CurrentVelocity = hitDir * projectile.Data.Speed;

                        if (logProjectileEvents)
                            Debug.Log($"[ProjectileSystem] Projectile bounced with new velocity: {projectile.CurrentVelocity}");
                    }
                    break;

                case CollisionBehavior.Explode:
                    HandleExplosion(projectile);
                    DestroyProjectile(projectile);
                    break;

                case CollisionBehavior.Chain:
                    projectile.ChainCount++;

                    if (projectile.HitsRemaining > 0)
                    {
                        // Find next target
                        NetworkIdentity nextTarget = FindNextChainTarget(projectile);

                        if (nextTarget != null)
                        {
                            projectile.CurrentTarget = nextTarget;
                            Vector3 chainDir = (nextTarget.transform.position - projectile.Transform.position).normalized;
                            projectile.CurrentVelocity = chainDir * projectile.Data.Speed;

                            if (logProjectileEvents)
                                Debug.Log($"[ProjectileSystem] Projectile chained to {nextTarget.name} (Chain #{projectile.ChainCount})");
                        }
                        else
                        {
                            // No more targets, destroy
                            if (logProjectileEvents)
                                Debug.Log($"[ProjectileSystem] No more chain targets found. Destroying projectile.");
                            DestroyProjectile(projectile);
                        }
                    }
                    else
                    {
                        DestroyProjectile(projectile);
                    }
                    break;

                case CollisionBehavior.Stop:
                    DestroyProjectile(projectile);
                    break;
            }
        }

        /// <summary>
        /// Finds the next chain target within range.
        /// Returns the closest valid target that hasn't been hit yet.
        /// </summary>
        /// <param name="projectile">The projectile looking for a chain target.</param>
        /// <returns>The next valid chain target, or null if none found.</returns>
        private NetworkIdentity FindNextChainTarget(ProjectileInstance projectile)
        {
            List<NetworkIdentity> validTargets = GetValidTargets(projectile);

            if (validTargets.Count == 0)
                return null;

            NetworkIdentity closestTarget = null;
            float closestDistance = projectile.Data.ChainRange;

            foreach (NetworkIdentity target in validTargets)
            {
                if (target == null || target.gameObject == null)
                    continue;

                float distance = Vector3.Distance(projectile.Transform.position, target.transform.position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = target;
                }
            }

            return closestTarget;
        }

        /// <summary>
        /// Handles explosion damage and effects.
        /// Applies damage to all targets within explosion radius and invokes explosion events.
        /// </summary>
        /// <param name="projectile">The projectile that is exploding.</param>
        private void HandleExplosion(ProjectileInstance projectile)
        {
            if (projectile.Data.ExplosionRadius <= 0f)
                return;

            // Get all NetworkIdentities in scene
            NetworkIdentity[] allTargets = FindObjectsOfType<NetworkIdentity>();

            List<NetworkIdentity> hitByExplosion = new List<NetworkIdentity>();

            foreach (NetworkIdentity target in allTargets)
            {
                if (target == null || target.gameObject == null)
                    continue;

                // Skip caster
                if (target == projectile.Caster)
                    continue;

                float distance = Vector3.Distance(projectile.Transform.position, target.transform.position);

                if (distance <= projectile.Data.ExplosionRadius)
                {
                    hitByExplosion.Add(target);

                    // Create hit result for each explosion victim
                    ProjectileHitResult result = new ProjectileHitResult
                    {
                        Projectile = projectile,
                        HitTarget = target,
                        HitPosition = target.transform.position,
                        HitNormal = (target.transform.position - projectile.Transform.position).normalized,
                        Damage = projectile.Data.Damage * projectile.Data.ExplosionDamageMultiplier,
                        IsExplosion = true
                    };

                    OnProjectileHit?.Invoke(result);
                }
            }

            // Invoke explosion event
            OnProjectileExploded?.Invoke(projectile, hitByExplosion);

            if (logProjectileEvents)
                Debug.Log($"[ProjectileSystem] Explosion hit {hitByExplosion.Count} targets at {projectile.Transform.position} with radius {projectile.Data.ExplosionRadius}");
        }

        #endregion

        #region Helper Methods (Private)

        /// <summary>
        /// Gets all valid targets in the scene (excluding caster and already hit targets).
        /// </summary>
        /// <param name="projectile">The projectile to get valid targets for.</param>
        /// <returns>List of valid NetworkIdentity targets.</returns>
        private List<NetworkIdentity> GetValidTargets(ProjectileInstance projectile)
        {
            NetworkIdentity[] allTargets = FindObjectsOfType<NetworkIdentity>();
            List<NetworkIdentity> validTargets = new List<NetworkIdentity>();

            foreach (NetworkIdentity target in allTargets)
            {
                if (IsValidTarget(projectile, target))
                {
                    validTargets.Add(target);
                }
            }

            return validTargets;
        }

        /// <summary>
        /// Checks if a target is valid for this projectile.
        /// Validates target existence, caster check, and hit history.
        /// </summary>
        /// <param name="projectile">The projectile checking for valid targets.</param>
        /// <param name="target">The potential target to validate.</param>
        /// <returns>True if target is valid, false otherwise.</returns>
        private bool IsValidTarget(ProjectileInstance projectile, NetworkIdentity target)
        {
            if (target == null || target.gameObject == null)
                return false;

            // Can't hit self (caster)
            if (target == projectile.Caster)
                return false;

            // Can't hit already-hit targets (unless bounce/pierce allows multiple hits)
            if (projectile.HitTargets.Contains(target))
                return false;

            // Additional validation could go here (team checks, layer masks, etc.)

            return true;
        }

        #endregion
    }
}