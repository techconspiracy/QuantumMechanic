# 🎯 QUANTUM MECHANIC - SESSION 23 - FULL GENERATION

## PostProcessing.cs - Complete Post-Processing & Screen Effects Framework

Generate complete post-processing and screen effects system in 3 chunks as artifacts:

### CHUNK 1 (140 lines): Core Post-Processing Framework
- PostProcessManager singleton (manages all post-process effects)
- Volume profile system (intensity, blending, priority)
- Effect stack management (add, remove, blend effects)
- Bloom and glow effects
- Color grading system (temperature, tint, saturation, contrast)
- Vignette effects (intensity, smoothness, color)
- Chromatic aberration
- Lens distortion
- Grain and film effects
- Effect presets (cinematic, horror, dreamlike, retro)

### CHUNK 2 (140 lines): Advanced Screen Effects
- Motion blur (camera and object-based)
- Depth of field (focus distance, aperture, bokeh)
- Ambient occlusion (SSAO)
- Screen space reflections (SSR)
- Anti-aliasing (FXAA, SMAA, TAA)
- Global illumination approximation
- God rays and volumetric lighting
- Screen distortion effects (damage, drunk, underwater)
- Heat distortion and refraction
- Edge detection and outline effects

### CHUNK 3 (120 lines): Dynamic Effect Systems
- Combat effect triggers (damage flash, death effects)
- Environment-based effects (underwater, fog, darkness)
- Health-based post-processing (low health vignette, bloodshot)
- Dynamic exposure adjustment
- Weather-based effects (rain on lens, snow blur)
- Transition effects (fade, wipe, dissolve)
- Speed-based effects (motion lines, speed blur)
- Status effect visuals (poison green tint, fire orange)
- Cinematic mode (letterbox, focus, color grade)
- Performance profiler and LOD system for effects

**Namespace:** `QuantumMechanic.Rendering`
**Dependencies:** VFX, Combat, Health, Weather, Camera
**Total:** ~400 lines with XML docs

Generate all 3 chunks now + session 24 starter prompt artifact

---

## Integration Notes for Session 23:
- Hook into health system for low-health effects
- Sync with combat system for damage flashes
- Weather system triggers appropriate screen effects
- Camera system integration for smooth transitions
- Performance-based effect quality scaling
- Save/load effect preferences
- Custom shader support for unique effects
- VR/mobile-friendly fallback modes
- Temporal effects for smooth transitions

## Example Usage:
```csharp
// Apply cinematic color grading
PostProcessManager.Instance.ApplyColorGrade(temperature: 10f, saturation: 1.2f);

// Low health vignette
PostProcessManager.Instance.SetHealthVignette(healthPercent: 0.3f);

// Damage flash
PostProcessManager.Instance.TriggerDamageFlash(intensity: 0.8f);

// Underwater effect
PostProcessManager.Instance.SetEnvironmentEffect(EnvironmentType.Underwater);

// Dynamic depth of field
PostProcessManager.Instance.SetDepthOfField(focusDistance: 10f, aperture: 2.8f);

// Transition effect
PostProcessManager.Instance.FadeToBlack(duration: 1f, () => {
    // Load new scene
});
```

---

**Copy this prompt to continue with Session 23: Post-Processing & Screen Effects**
