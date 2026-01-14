# QUANTUM MECHANIC - SESSION 10 - CHUNK 2/3 KICKOFF
## ProjectileSystem.cs - Spawning & Movement Implementation

---

## ✅ WHAT'S ALREADY COMPLETE (Chunk 1)

You have a working foundation with:
- ✅ All data structures (ProjectileData, ProjectileInstance, ProjectileSpawnRequest, ProjectileHitResult)
- ✅ All enums (ProjectileType, CollisionBehavior, RotationMode)
- ✅ Singleton pattern initialized in Awake()
- ✅ Inspector fields configured
- ✅ Private state (activeProjectiles list, pools, system references)
- ✅ 4 events declared (OnProjectileSpawned, OnProjectileHit, OnProjectileExploded, OnProjectileDestroyed)
- ✅ Update() iterates projectiles and handles lifetime/distance checks
- ✅ Public management API (GetActiveProjectiles, GetProjectilesFromCaster, DestroyProjectile, etc.)
- ✅ ReturnToPool() helper ready

**Line count so far:** ~380 lines

---

## 🎯 CHUNK 2 OBJECTIVE: Spawning & Movement Logic

Implement the following **without modifying Chunk 1 code**:

### 1. Public API - Spawning (3 methods)
```csharp
/// <summary>
/// Spawns a projectile based on the provided request.
/// </summary>
public ProjectileInstance SpawnProjectile(ProjectileSpawnRequest request)

/// <summary>
/// Spawns a projectile from an ability definition.
/// </summary>
public ProjectileInstance SpawnProjectileFromAbility(AbilityData ability, NetworkIdentity caster, Vector3 origin, Vector3 direction, NetworkIdentity target = null)

/// <summary>
/// Gets a pooled projectile GameObject or creates a new one.
/// </summary>
private GameObject GetPooledProjectile(ProjectileData data)
```

### 2. Projectile Update Dispatcher (1 method)
```csharp
/// <summary>
/// Updates projectile position based on its type.
/// Called from Update() loop (uncomment the call in Update()).
/// </summary>
private void UpdateProjectile(ProjectileInstance projectile)
```

### 3. Movement Implementations (5 methods)
```csharp
/// <summary>
/// Updates straight projectile (linear velocity).
/// </summary>
private void UpdateStraight(ProjectileInstance projectile)

/// <summary>
/// Updates homing projectile (RotateTowards target).
/// </summary>
private void UpdateHoming(ProjectileInstance projectile)

/// <summary>
/// Updates arcing projectile (applies gravity).
/// </summary>
private void UpdateArcing(ProjectileInstance projectile)

/// <summary>
/// Updates boomerang projectile (returns to caster).
/// </summary>
private void UpdateBoomerang(ProjectileInstance projectile)

/// <summary>
/// Updates chaining projectile (finds next target).
/// </summary>
private void UpdateChaining(ProjectileInstance projectile)
```

### 4. Visual Effects (3 methods)
```csharp
/// <summary>
/// Applies rotation based on RotationMode.
/// </summary>
private void ApplyRotation(ProjectileInstance projectile)

/// <summary>
/// Configures the TrailRenderer component.
/// </summary>
private void SetupTrail(ProjectileInstance projectile)

/// <summary>
/// Initializes the object pool for a projectile type.
/// </summary>
private void InitializePool(ProjectileData data, int initialSize)
```

---

## 📋 IMPLEMENTATION REQUIREMENTS

### SpawnProjectile() Requirements:
1. Check if max projectile limits are exceeded (maxActiveProjectiles, maxProjectilesPerCaster)
2. Get pooled/new GameObject via GetPooledProjectile()
3. Create ProjectileInstance and populate all fields
4. Set initial velocity: `Direction.normalized * Speed`
5. Set LifetimeRemaining = Data.Lifetime
6. Set HitsRemaining based on collision mode (Pierce = MaxPierceTargets, Chain = MaxChainTargets, else 1)
7. Configure visual components (SetupTrail, apply color/scale)
8. Add to activeProjectiles list
9. Invoke OnProjectileSpawned event
10. Return the ProjectileInstance

### Movement Update Examples:

**UpdateStraight:**
```csharp
Vector3 movement = projectile.CurrentVelocity * Time.deltaTime;
projectile.Transform.position += movement;
projectile.DistanceTraveled += movement.magnitude;
ApplyRotation(projectile);
```

**UpdateHoming:**
```csharp
// Only home after activation delay
if (projectile.InitialTarget != null && 
    projectile.LifetimeRemaining < projectile.Data.Lifetime - projectile.Data.HomingActivationDelay)
{
    Vector3 targetDir = (projectile.InitialTarget.transform.position - projectile.Transform.position).normalized;
    Vector3 currentDir = projectile.CurrentVelocity.normalized;
    
    Vector3 newDir = Vector3.RotateTowards(currentDir, targetDir, 
        projectile.Data.HomingStrength * Time.deltaTime, 0f);
    
    projectile.CurrentVelocity = newDir * projectile.Data.Speed;
}

Vector3 movement = projectile.CurrentVelocity * Time.deltaTime;
projectile.Transform.position += movement;
projectile.DistanceTraveled += movement.magnitude;
projectile.Transform.rotation = Quaternion.LookRotation(projectile.CurrentVelocity);
```

**UpdateArcing:**
```csharp
// Apply gravity
projectile.CurrentVelocity += Physics.gravity * projectile.Data.GravityMultiplier * Time.deltaTime;

Vector3 movement = projectile.CurrentVelocity * Time.deltaTime;
projectile.Transform.position += movement;
projectile.DistanceTraveled += movement.magnitude;
projectile.Transform.rotation = Quaternion.LookRotation(projectile.CurrentVelocity);
```

**UpdateBoomerang:**
```csharp
// Check if should start returning
if (!projectile.IsReturning && projectile.DistanceTraveled >= projectile.Data.MaxDistance)
{
    projectile.IsReturning = true;
}

if (projectile.IsReturning && projectile.Caster != null)
{
    Vector3 returnDir = (projectile.Caster.transform.position - projectile.Transform.position).normalized;
    projectile.CurrentVelocity = returnDir * projectile.Data.ReturnSpeed;
    
    // Check if returned to caster (within 1 unit)
    if (Vector3.Distance(projectile.Transform.position, projectile.Caster.transform.position) < 1f)
    {
        DestroyProjectile(projectile);
        return;
    }
}

Vector3 movement = projectile.CurrentVelocity * Time.deltaTime;
projectile.Transform.position += movement;
projectile.DistanceTraveled += movement.magnitude;
ApplyRotation(projectile);
```

**UpdateChaining:**
```csharp
// For now, just move straight - full chain logic in Chunk 3 collision detection
UpdateStraight(projectile);
```

### ApplyRotation() Requirements:
```csharp
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
        // Do nothing
        break;
        
    case RotationMode.Random:
        projectile.Transform.rotation = Random.rotation;
        break;
}
```

### SetupTrail() Requirements:
```csharp
if (!projectile.Data.UseTrail) return;

TrailRenderer trail = projectile.GameObject.GetComponent<TrailRenderer>();
if (trail == null)
{
    trail = projectile.GameObject.AddComponent<TrailRenderer>();
}

trail.time = 0.5f;
trail.startWidth = projectile.Data.ProjectileScale * 0.3f;
trail.endWidth = 0f;
trail.startColor = projectile.Data.ProjectileColor;
trail.endColor = new Color(projectile.Data.ProjectileColor.r, projectile.Data.ProjectileColor.g, projectile.Data.ProjectileColor.b, 0f);

projectile.Trail = trail;
```

### GetPooledProjectile() Requirements:
```csharp
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
    return Instantiate(data.ProjectilePrefab);
}

// Fallback: create primitive sphere
GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
sphere.transform.localScale = Vector3.one * data.ProjectileScale;
Renderer renderer = sphere.GetComponent<Renderer>();
if (renderer != null)
{
    renderer.material.color = data.ProjectileColor;
}
return sphere;
```

### InitializePool() Requirements:
```csharp
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
    }
    
    projectile.SetActive(false);
    pool.Enqueue(projectile);
}

projectilePools[data.ProjectileId] = pool;
```

---

## 🔧 INTEGRATION WITH CHUNK 1

**In Update() method, uncomment this line:**
```csharp
// Movement update will be implemented in Chunk 2
UpdateProjectile(projectile);  // ← UNCOMMENT THIS
```

---

## 📊 EXPECTED OUTPUT

- **~200-250 lines** of new code (total will be ~580-630 lines)
- **11 new methods** with full XML documentation
- **3 #regions**: 
  - `#region Public API - Spawning`
  - `#region Projectile Update (Private)`
  - `#region Visual Effects (Private)`
- **Compiles successfully** with Chunk 1 foundation
- **All movement types functional** (can be tested by spawning projectiles)

---

## ⚠️ CRITICAL REMINDERS

- **Do NOT modify Chunk 1 code** (except uncommenting UpdateProjectile call in Update())
- **Do NOT implement collision detection yet** (that's Chunk 3)
- **Do NOT implement HandleExplosion() yet** (that's Chunk 3)
- **Do NOT implement chain target finding yet** (that's Chunk 3 - UpdateChaining just calls UpdateStraight for now)
- Include full XML documentation for all methods
- Use proper error handling (null checks for caster, target, etc.)
- Log events if logProjectileEvents is true

---

## 🚀 READY TO IMPLEMENT CHUNK 2?

Copy this prompt into your chat:

```
I'm continuing Quantum Mechanic Session 10 - ProjectileSystem.cs (Chunk 2/3).

Chunk 1 is complete with all data structures, singleton setup, events, and management API.

Now implement SPAWNING & MOVEMENT:
- SpawnProjectile() and SpawnProjectileFromAbility()
- GetPooledProjectile() and InitializePool()
- UpdateProjectile() dispatcher
- All 5 movement types (UpdateStraight, UpdateHoming, UpdateArcing, UpdateBoomerang, UpdateChaining)
- ApplyRotation() and SetupTrail()

DO NOT implement collision detection or HandleExplosion() - those are Chunk 3.

Output: ~200-250 lines with XML docs and #regions.
Namespace: QuantumMechanic.Combat

Ready to generate Chunk 2?
```

---

## 📈 PROGRESS TRACKER

- ✅ **Chunk 1:** Foundation (380 lines) - COMPLETE
- 🔄 **Chunk 2:** Spawning & Movement (200-250 lines) - NEXT
- ⏳ **Chunk 3:** Collision & Pooling (150-200 lines) - PENDING

**Estimated Total:** 730-830 lines (target was 450-600, but we're adding more functionality!)
