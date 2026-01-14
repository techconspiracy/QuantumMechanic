namespace QuantumMechanic.Audio
{
    // CONTINUATION OF AudioManager class

    public partial class AudioManager
    {
        [Header("Dialogue System")]
        [SerializeField] private bool enableAudioDucking = true;
        [SerializeField] private float duckingLevel = 0.3f;
        [SerializeField] private float duckingTransitionSpeed = 2f;

        [Header("Combat Audio")]
        [SerializeField] private float criticalHitVolume = 1.2f;

        [Header("UI Audio")]
        [SerializeField] private float uiVolumeMultiplier = 0.8f;

        [Header("Accessibility")]
        [SerializeField] private bool enableMonoAudio = false;
        [SerializeField] private bool enableVisualIndicators = false;

        // Dialogue system
        private AudioSource dialogueSource;
        private bool isDucking = false;
        private float preDuckMusicVolume = 1f;

        // Performance monitoring
        private int audioMemoryUsage = 0;
        private float audioCPUUsage = 0f;

        /// <summary>
        /// Play dialogue with optional music ducking
        /// </summary>
        public void PlayDialogue(string dialogueName, bool duckMusic = true, System.Action onComplete = null)
        {
            AudioClip clip = LoadAudioClip(dialogueName);
            if (clip == null) return;

            if (dialogueSource == null)
            {
                dialogueSource = GetAudioSource(priority: 0); // Highest priority
                dialogueSource.outputAudioMixerGroup = voiceMixer;
                dialogueSource.spatialBlend = 0f;
            }

            dialogueSource.clip = clip;
            dialogueSource.volume = channelVolumes[AudioChannel.Voice] * masterVolume;
            dialogueSource.Play();

            if (duckMusic && enableAudioDucking)
            {
                StartAudioDucking();
            }

            if (onComplete != null)
            {
                StartCoroutine(WaitForAudioComplete(dialogueSource, onComplete));
            }
        }

        /// <summary>
        /// Start audio ducking (reduce music/ambient volume)
        /// </summary>
        private void StartAudioDucking()
        {
            isDucking = true;
            preDuckMusicVolume = channelVolumes[AudioChannel.Music];
        }

        /// <summary>
        /// Stop audio ducking
        /// </summary>
        private void StopAudioDucking()
        {
            isDucking = false;
        }

        /// <summary>
        /// Update audio ducking
        /// </summary>
        private void UpdateAudioDucking()
        {
            if (dialogueSource != null && dialogueSource.isPlaying)
            {
                float targetVolume = isDucking ? preDuckMusicVolume * duckingLevel : preDuckMusicVolume;
                channelVolumes[AudioChannel.Music] = Mathf.MoveTowards(
                    channelVolumes[AudioChannel.Music],
                    targetVolume,
                    Time.deltaTime * duckingTransitionSpeed
                );
            }
            else if (isDucking)
            {
                StopAudioDucking();
            }
        }

        /// <summary>
        /// Play combat audio trigger
        /// </summary>
        public void PlayCombatAudio(CombatAudioType type, Vector3 position, float volume = 1f)
        {
            string soundName = type switch
            {
                CombatAudioType.Hit => "combat_hit",
                CombatAudioType.CriticalHit => "combat_critical",
                CombatAudioType.Death => "combat_death",
                CombatAudioType.Block => "combat_block",
                CombatAudioType.Dodge => "combat_dodge",
                _ => "combat_hit"
            };

            float finalVolume = type == CombatAudioType.CriticalHit ? volume * criticalHitVolume : volume;
            PlaySFX(soundName, position, finalVolume, randomPitch: true);
        }

        /// <summary>
        /// Play UI sound feedback
        /// </summary>
        public void PlayUI(string uiSoundName, float volume = 1f)
        {
            AudioClip clip = LoadAudioClip(uiSoundName);
            if (clip == null) return;

            AudioSource source = GetAudioSource(priority: 64);
            source.clip = clip;
            source.volume = volume * uiVolumeMultiplier * channelVolumes[AudioChannel.UI] * masterVolume;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = uiMixer;
            source.Play();
        }

        /// <summary>
        /// Play dynamic soundscape
        /// </summary>
        public void PlaySoundscape(string soundscapeName, float intensity = 0.5f)
        {
            // Play layered ambient sounds based on intensity
            SetAmbience($"{soundscapeName}_base", intensity * 0.8f);
            
            if (intensity > 0.3f)
            {
                SetAmbience($"{soundscapeName}_mid", (intensity - 0.3f) * 0.6f);
            }
            
            if (intensity > 0.6f)
            {
                SetAmbience($"{soundscapeName}_high", (intensity - 0.6f) * 0.5f);
            }
        }

        /// <summary>
        /// Play audio cue for gameplay event
        /// </summary>
        public void PlayGameplayCue(GameplayCueType cue, float volume = 0.8f)
        {
            string soundName = cue switch
            {
                GameplayCueType.LowHealth => "cue_low_health",
                GameplayCueType.DangerNear => "cue_danger",
                GameplayCueType.ObjectiveComplete => "cue_objective",
                GameplayCueType.ItemPickup => "cue_pickup",
                GameplayCueType.LevelUp => "cue_levelup",
                _ => "cue_default"
            };

            PlayUI(soundName, volume);
        }

        /// <summary>
        /// Set master volume (0-1)
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            if (masterMixer != null)
            {
                masterMixer.audioMixer.SetFloat("MasterVolume", Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20f);
            }
            SaveAudioSettings();
        }

        /// <summary>
        /// Set channel volume
        /// </summary>
        public void SetChannelVolume(AudioChannel channel, float volume)
        {
            channelVolumes[channel] = Mathf.Clamp01(volume);
            SaveAudioSettings();
        }

        /// <summary>
        /// Fade audio source in
        /// </summary>
        private void FadeIn(AudioSource source, float duration, float targetVolume)
        {
            source.volume = 0f;
            fadingAudioSources[source] = targetVolume;
        }

        /// <summary>
        /// Fade audio source out
        /// </summary>
        private void FadeOut(AudioSource source, float duration)
        {
            fadingAudioSources[source] = 0f;
        }

        /// <summary>
        /// Load audio clip from resources
        /// </summary>
        private AudioClip LoadAudioClip(string clipName)
        {
            if (loadedClips.ContainsKey(clipName))
            {
                return loadedClips[clipName];
            }

            AudioClip clip = Resources.Load<AudioClip>($"Audio/{clipName}");
            if (clip != null)
            {
                loadedClips[clipName] = clip;
            }

            return clip;
        }

        /// <summary>
        /// Save audio settings to PlayerPrefs
        /// </summary>
        private void SaveAudioSettings()
        {
            PlayerPrefs.SetFloat("Audio_MasterVolume", masterVolume);
            foreach (var channel in channelVolumes)
            {
                PlayerPrefs.SetFloat($"Audio_{channel.Key}Volume", channel.Value);
            }
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Load audio settings from PlayerPrefs
        /// </summary>
        private void LoadAudioSettings()
        {
            masterVolume = PlayerPrefs.GetFloat("Audio_MasterVolume", 1f);
            foreach (AudioChannel channel in System.Enum.GetValues(typeof(AudioChannel)))
            {
                float volume = PlayerPrefs.GetFloat($"Audio_{channel}Volume", 1f);
                channelVolumes[channel] = volume;
            }
        }

        /// <summary>
        /// Coroutine to wait for audio completion
        /// </summary>
        private System.Collections.IEnumerator WaitForAudioComplete(AudioSource source, System.Action callback)
        {
            yield return new WaitWhile(() => source.isPlaying);
            callback?.Invoke();
        }
    }

    /// <summary>
    /// Combat audio types
    /// </summary>
    public enum CombatAudioType
    {
        Hit,
        CriticalHit,
        Death,
        Block,
        Dodge
    }

    /// <summary>
    /// Gameplay audio cue types
    /// </summary>
    public enum GameplayCueType
    {
        LowHealth,
        DangerNear,
        ObjectiveComplete,
        ItemPickup,
        LevelUp
    }
}