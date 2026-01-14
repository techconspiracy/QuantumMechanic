# 🎯 QUANTUM MECHANIC - SESSION 24 - FULL GENERATION

## AudioManager.cs - Complete Audio & Sound System Framework

Generate complete audio management and sound system in 3 chunks as artifacts:

### CHUNK 1 (140 lines): Core Audio Framework
- AudioManager singleton (manages all audio sources)
- Audio channel system (music, sfx, ambient, voice, ui)
- Volume control and mixing (master, per-channel)
- Audio pool management (object pooling for sound effects)
- 3D spatial audio (distance attenuation, doppler, reverb zones)
- Audio source priority system
- Dynamic audio loading (addressables, resource management)
- Audio fade in/out (cross-fading, smooth transitions)
- Audio ducking (reduce music when dialogue plays)
- Audio settings persistence (save/load preferences)

### CHUNK 2 (140 lines): Advanced Sound Systems
- Music system (playlist, shuffle, crossfade, layers)
- Adaptive music (combat intensity, exploration, stealth)
- Dynamic music stems (layer instruments based on gameplay)
- Footstep system (surface detection, material-based sounds)
- Weapon sound system (firing, reload, impact sounds)
- Environmental ambience (weather sounds, room tone)
- Random sound variation (pitch, volume randomization)
- Audio occlusion (walls block sound realistically)
- Sound propagation (echo, reverb, distance)
- Audio snapshot system (preset mixing configurations)

### CHUNK 3 (120 lines): Interactive Audio Features
- Dialogue system integration (voice lines, subtitles sync)
- Combat audio triggers (hit sounds, critical hits, death)
- UI sound feedback (button clicks, hover, notifications)
- Dynamic soundscape (crowd noise, battle ambience)
- Audio cues for gameplay (low health warning, danger proximity)
- Voice synthesis integration (TTS for dynamic content)
- Audio visualization (spectrum analyzer, waveform display)
- Performance monitoring (audio memory, CPU usage)
- Accessibility features (subtitles, visual indicators, mono audio)
- Audio event system (trigger sounds from game events)

**Namespace:** `QuantumMechanic.Audio`
**Dependencies:** Physics, Combat, Player, UI, Events
**Total:** ~400 lines with XML docs

Generate all 3 chunks now + session 25 starter prompt artifact

---

## Integration Notes for Session 24:
- Hook into combat system for hit sounds and reactions
- Connect to player movement for footsteps
- Weather system triggers ambient sound changes
- UI system plays feedback sounds
- Health system triggers warning audio cues
- Dialogue system for NPC conversations
- Save system stores audio preferences
- Performance profiler for audio optimization
- VR/3D audio spatialization support
- Platform-specific audio compression

## Example Usage:
```csharp
// Play sound effect
AudioManager.Instance.PlaySFX("gunshot", position: transform.position, volume: 0.8f);

// Start background music with crossfade
AudioManager.Instance.PlayMusic("combat_theme", fadeInDuration: 2f);

// Play footstep based on surface
AudioManager.Instance.PlayFootstep(surfaceType: SurfaceType.Metal, volume: 0.6f);

// Apply combat intensity to adaptive music
AudioManager.Instance.SetMusicIntensity(intensity: 0.8f);

// Play UI click sound
AudioManager.Instance.PlayUI("button_click");

// Set master volume
AudioManager.Instance.SetMasterVolume(0.7f);

// Enable audio ducking for dialogue
AudioManager.Instance.PlayDialogue("npc_greeting_01", duckMusic: true);
```

---

**Copy this prompt to continue with Session 24: Audio & Sound Systems**
