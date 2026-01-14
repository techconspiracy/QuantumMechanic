# 🎯 QUANTUM MECHANIC - SESSION 22 - FULL GENERATION

## ParticleSystem.cs - Complete Particle & Visual Effects Framework

Generate complete particle and VFX system in 3 chunks as artifacts:

### CHUNK 1 (140 lines): Particle Core Framework
- ParticleManager singleton (particle pooling system)
- ParticleEffect class (wrapper for Unity particle systems)
- Particle pool management (create, spawn, return to pool)
- Particle preloading and caching
- Particle emission control (play, stop, pause)
- Particle color and size modulation
- Particle lifetime management
- Particle collision detection
- Particle performance profiler (active count, memory)

### CHUNK 2 (140 lines): VFX Systems
- Ability VFX system (skill effects, projectile trails)
- Combat VFX (hit effects, blood, sparks, explosions)
- Environmental VFX (dust, leaves, water splashes)
- Weather VFX (rain, snow, fog particles)
- Magic effects (spells, enchantments, buffs/debuffs)
- Trail renderer system (weapon trails, movement trails)
- Beam VFX system (lasers, energy beams)
- Shield and barrier effects
- Teleportation and portal effects

### CHUNK 3 (120 lines): Advanced VFX Features
- Screen space effects (post-process integration)
- Camera shake system (impact, explosions, damage)
- Flash effects (damage flash, invincibility blink)
- Decal system (bullet holes, footprints, blood)
- Procedural lightning generator
- Material property animation (glow, dissolve, fade)
- VFX sequencer (timed multi-effect combos)
- VFX audio sync (particles trigger sounds)
- VFX event callbacks (onStart, onComplete)
- Custom particle behaviors (homing, orbit, spiral)

**Namespace:** `QuantumMechanic.VFX`
**Dependencies:** Audio, Combat, Magic, Weather
**Total:** ~400 lines with XML docs

Generate all 3 chunks now + session 23 starter prompt artifact

---

## Integration Notes for Session 22:
- Sync particle effects with combat hit detection
- Weather particles tied to weather system
- Magic VFX linked to spell casting system
- Camera shake on impact and explosions
- Audio triggers from particle events
- Decals spawned on bullet/projectile hit
- Performance optimization through object pooling
- Support for custom particle shaders and materials
- VFX scaling based on graphics settings

## Example Usage:
```csharp
// Spawn hit effect
ParticleManager.Instance.SpawnEffect("HitSpark", hitPosition, hitNormal);

// Camera shake on explosion
CameraShakeManager.Instance.Shake(intensity: 0.5f, duration: 0.3f);

// Magic spell with trail
MagicVFX.Instance.CastSpell("Fireball", startPos, targetPos, () => {
    // Explosion on impact
    ParticleManager.Instance.SpawnEffect("Explosion", targetPos);
});

// Weather particles
WeatherVFX.Instance.SetWeather(WeatherType.Rain, intensity: 0.8f);
```

---

**Copy this prompt to continue with Session 22: Particle & VFX System**
