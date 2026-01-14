# QUANTUM MECHANIC - SESSION 10 - CHUNK 3/3 KICKOFF
## ProjectileSystem.cs - Collision Detection & Cleanup Implementation

---

## ✅ WHAT'S ALREADY COMPLETE (Chunks 1 & 2)

You have a fully functional spawning and movement system:
- ✅ All data structures, enums, and singleton setup (Chunk 1)
- ✅ Events, management API, and lifecycle management (Chunk 1)
- ✅ SpawnProjectile() and SpawnProjectileFromAbility() (Chunk 2)
- ✅ Object pooling system (GetPooledProjectile, InitializePool) (Chunk 2)
- ✅ All 5 movement types working (Straight, Homing, Arcing, Boomerang, Chaining) (Chunk 2)
- ✅ Visual effects (ApplyRotation, SetupTrail) (Chunk 2)

**Line count so far:** ~630 lines

---

## 🎯 CHUNK 3 OBJECTIVE: Collision Detection & Cleanup

Implement the final systems **without modifying Chunk 1 or 2 code**:

### 1. Collision Detection (4 methods)
```csharp
/// <summary>
/// Checks for collisions with targets in the scene.
/// Called from Update() loop (uncomment the call in Update()).
/// </summary>
private void CheckCollisions(ProjectileInstance projectile)

/// <summary>
/// Handles collision with a single target.
/// </summary>
private void HandleCollision(ProjectileInstance projectile, NetworkIdentity target)

/// <summary>
/// Finds the next chain target within range.
/// </summary>
private NetworkIdentity FindNextChainTarget(ProjectileInstance projectile)

/// <summary>
/// Handles explosion damage and effects.
/// </summary>
private void HandleExplosion(ProjectileInstance projectile)
```

### 2. Helper Methods (2 methods)
```csharp
/// <summary>
/// Gets all valid targets in the scene (excluding caster and already hit targets).
/// </summary>
private List<NetworkIdentity> GetValidTargets(ProjectileInstance projectile)

/// <summary>
/// Checks if a target is valid for this projectile.
/// </summary>
private bool IsValidTarget(ProjectileInstance projectile, NetworkIdentity target)
```

---

## 📋 IMPLEMENTATION REQUIREMENTS

### CheckCollisions() Requirements:
This is the core collision detection loop that gets called from Update().

**Implementation:**
```csharp
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
```

### HandleCollision() Requirements:
Handles what happens when a projectile hits a target.

**Key Logic:**
1. Add target to HitTargets list
2. Create ProjectileHitResult with all relevant data
3. Invoke OnProjectileHit event
4. Decrement HitsRemaining
5. Handle collision behavior:
   - **Pierce:** Continue if HitsRemaining > 0, else destroy
   - **Bounce:** Reflect velocity (implement basic reflection)
   - **Explode:** Call HandleExplosion(), then destroy
   - **Chain:** Find next target via FindNextChainTarget(), update CurrentTarget and velocity
   - **Stop:** Just destroy the projectile
6. Log event if logProjectileEvents is true

**Example Pierce Logic:**
```csharp
case CollisionBehavior.Pierce:
    if (projectile.HitsRemaining <= 0)
    {
        DestroyProjectile(projectile);
    }
    // else continue moving
    break;
```

**Example Chain Logic:**
```csharp
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
                Debug.Log($"[ProjectileSystem] Projectile chained to {nextTarget.name}");
        }
        else
        {
            // No more targets, destroy
            DestroyProjectile(projectile);
        }
    }
    else
    {
        DestroyProjectile(projectile);
    }
    break;
```

**Example Bounce Logic:**
```csharp
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
    }
    break;
```

### FindNextChainTarget() Requirements:
Finds the closest valid target within chain range.

**Implementation:**
```csharp
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
```

### HandleExplosion() Requirements:
Handles AoE explosion damage.

**Implementation:**
```csharp
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
        Debug.Log($"[ProjectileSystem] Explosion hit {hitByExplosion.Count} targets at {projectile.Transform.position}");
}
```

### GetValidTargets() Requirements:
Returns all NetworkIdentity objects that are valid targets.

**Implementation:**
```csharp
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
```

### IsValidTarget() Requirements:
Checks if a target is valid for collision.

**Implementation:**
```csharp
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
    
    // Additional validation could go here (team checks, etc.)
    
    return true;
}
```

---

## 🔧 INTEGRATION WITH PREVIOUS CHUNKS

**In Update() method, uncomment this line:**
```csharp
// Collision detection will be implemented in Chunk 3
CheckCollisions(projectile);  // ← UNCOMMENT THIS
```

**Note:** The Update() method should now call both:
- `UpdateProjectile(projectile);` (Chunk 2 - already uncommented)
- `CheckCollisions(projectile);` (Chunk 3 - uncomment now)

---

## 📊 EXPECTED OUTPUT

- **~150-200 lines** of new code (total will be ~780-830 lines)
- **6 new methods** with full XML documentation
- **2 #regions**: 
  - `#region Collision Detection (Private)`
  - `#region Helper Methods (Private)`
- **Compiles successfully** with Chunks 1 & 2
- **Full collision system functional** with all behaviors (Pierce, Bounce, Explode, Chain, Stop)
- **All events properly invoked** (OnProjectileHit, OnProjectileExploded)

---

## ⚠️ CRITICAL REMINDERS

- **Do NOT modify Chunk 1 or 2 code** (except uncommenting CheckCollisions call in Update())
- Include full XML documentation for all methods
- Use proper null checks for all target interactions
- Log events if logProjectileEvents is true
- Ensure explosion events include all affected targets
- Chain targets must be within ChainRange
- Bounce should reflect velocity off the hit target
- Pierce continues until HitsRemaining reaches 0

---

## 🚀 READY TO IMPLEMENT CHUNK 3?

Copy this prompt into your chat:

```
I'm completing Quantum Mechanic Session 10 - ProjectileSystem.cs (Chunk 3/3 - FINAL).

Chunks 1 & 2 are complete with all data structures, spawning, movement, and visual effects.

Now implement COLLISION DETECTION & CLEANUP:
- CheckCollisions() main loop
- HandleCollision() with all 5 collision behaviors (Pierce, Bounce, Explode, Chain, Stop)
- HandleExplosion() for AoE damage
- FindNextChainTarget() for chaining projectiles
- GetValidTargets() and IsValidTarget() helpers

This completes the entire ProjectileSystem.

Output: ~150-200 lines with XML docs and #regions.
Namespace: QuantumMechanic.Combat

Ready to generate Chunk 3 (FINAL)?
```

---

## 📈 PROGRESS TRACKER

- ✅ **Chunk 1:** Foundation (380 lines) - COMPLETE
- ✅ **Chunk 2:** Spawning & Movement (250 lines) - COMPLETE
- 🔄 **Chunk 3:** Collision & Cleanup (150-200 lines) - NEXT (FINAL)

**Estimated Total:** 780-830 lines

---

## 🎉 COMPLETION CHECKLIST

After Chunk 3, verify:
- [ ] All projectile types spawn correctly
- [ ] All movement types work (Straight, Homing, Arcing, Boomerang, Chaining)
- [ ] Collision detection fires OnProjectileHit events
- [ ] Pierce projectiles hit multiple targets
- [ ] Bounce projectiles reflect off targets
- [ ] Explode projectiles trigger HandleExplosion with AoE
- [ ] Chain projectiles jump to nearby targets
- [ ] Stop projectiles destroy on first hit
- [ ] Object pooling returns projectiles correctly
- [ ] All events invoke with proper data
- [ ] Logging outputs expected messages

**🎊 SESSION 10 WILL BE COMPLETE! 🎊**