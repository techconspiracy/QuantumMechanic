namespace QuantumMechanic.Audio
{
    // CONTINUATION OF AudioManager class

    public partial class AudioManager
    {
        [Header("Music System")]
        [SerializeField] private bool enableMusicPlaylist = true;
        [SerializeField] private bool shufflePlaylist = false;
        [SerializeField] private float musicCrossfadeDuration = 2f;

        [Header("Adaptive Music")]
        [SerializeField] private float musicIntensity = 0f;
        [SerializeField] private float intensityTransitionSpeed = 1f;

        [Header("Footstep System")]
        [SerializeField] private LayerMask footstepLayerMask;
        [SerializeField] private float footstepRaycastDistance = 1.5f;

        // Music system
        private AudioSource musicSourceA;
        private AudioSource musicSourceB;
        private bool currentMusicIsA = true;
        private List<AudioClip> musicPlaylist = new List<AudioClip>();
        private int currentPlaylistIndex = 0;

        // Adaptive music stems
        private Dictionary<string, AudioSource> musicStems = new Dictionary<string, AudioSource>();
        private float targetIntensity = 0f;

        // Footstep surfaces
        private Dictionary<string, AudioClip[]> footstepSounds = new Dictionary<string, AudioClip[]>();

        // Environmental ambience
        private Dictionary<string, AudioSource> ambientSources = new Dictionary<string, AudioSource>();
        private AudioSource weatherAmbience;

        // Audio occlusion
        private Dictionary<AudioSource, float> occludedSources = new Dictionary<AudioSource, float>();
        private LayerMask occlusionMask;

        /// <summary>
        /// Play music with crossfade support
        /// </summary>
        public void PlayMusic(string musicName, float fadeInDuration = 2f, bool loop = true)
        {
            AudioClip clip = LoadAudioClip(musicName);
            if (clip == null)
            {
                Debug.LogWarning($"Music clip not found: {musicName}");
                return;
            }

            AudioSource targetSource = currentMusicIsA ? musicSourceB : musicSourceA;
            AudioSource fadeOutSource = currentMusicIsA ? musicSourceA : musicSourceB;

            // Crossfade
            if (fadeOutSource.isPlaying)
            {
                FadeOut(fadeOutSource, musicCrossfadeDuration);
            }

            targetSource.clip = clip;
            targetSource.loop = loop;
            targetSource.volume = 0f;
            targetSource.Play();
            FadeIn(targetSource, fadeInDuration, channelVolumes[AudioChannel.Music]);

            currentMusicIsA = !currentMusicIsA;
        }

        /// <summary>
        /// Stop all music with fade out
        /// </summary>
        public void StopMusic(float fadeOutDuration = 2f)
        {
            if (musicSourceA.isPlaying) FadeOut(musicSourceA, fadeOutDuration);
            if (musicSourceB.isPlaying) FadeOut(musicSourceB, fadeOutDuration);
        }

        /// <summary>
        /// Set adaptive music intensity (0-1)
        /// </summary>
        public void SetMusicIntensity(float intensity)
        {
            targetIntensity = Mathf.Clamp01(intensity);
        }

        /// <summary>
        /// Update adaptive music stems based on intensity
        /// </summary>
        private void UpdateAdaptiveMusicStems()
        {
            musicIntensity = Mathf.MoveTowards(musicIntensity, targetIntensity, Time.deltaTime * intensityTransitionSpeed);

            // Adjust stem volumes based on intensity
            foreach (var stem in musicStems)
            {
                if (stem.Key.Contains("low"))
                {
                    stem.Value.volume = Mathf.Lerp(1f, 0.3f, musicIntensity);
                }
                else if (stem.Key.Contains("high"))
                {
                    stem.Value.volume = Mathf.Lerp(0f, 1f, musicIntensity);
                }
            }
        }

        /// <summary>
        /// Play footstep sound based on surface type
        /// </summary>
        public void PlayFootstep(Vector3 position, float volume = 0.6f)
        {
            RaycastHit hit;
            string surfaceType = "default";

            if (Physics.Raycast(position, Vector3.down, out hit, footstepRaycastDistance, footstepLayerMask))
            {
                // Detect surface material from texture or tag
                if (hit.collider.CompareTag("Metal"))
                    surfaceType = "metal";
                else if (hit.collider.CompareTag("Wood"))
                    surfaceType = "wood";
                else if (hit.collider.CompareTag("Stone"))
                    surfaceType = "stone";
                else if (hit.collider.CompareTag("Grass"))
                    surfaceType = "grass";
            }

            PlayFootstepForSurface(surfaceType, position, volume);
        }

        /// <summary>
        /// Play footstep for specific surface type
        /// </summary>
        private void PlayFootstepForSurface(string surfaceType, Vector3 position, float volume)
        {
            if (footstepSounds.ContainsKey(surfaceType) && footstepSounds[surfaceType].Length > 0)
            {
                AudioClip clip = footstepSounds[surfaceType][Random.Range(0, footstepSounds[surfaceType].Length)];
                PlaySFX(clip, position, volume, randomPitch: true);
            }
        }

        /// <summary>
        /// Play weapon firing sound
        /// </summary>
        public void PlayWeaponSound(string weaponType, Vector3 position, float volume = 0.8f)
        {
            string soundName = $"weapon_{weaponType}_fire";
            PlaySFX(soundName, position, volume, priority: 64);
        }

        /// <summary>
        /// Play weapon reload sound
        /// </summary>
        public void PlayReloadSound(string weaponType, float volume = 0.6f)
        {
            string soundName = $"weapon_{weaponType}_reload";
            PlaySFX(soundName, Vector3.zero, volume, spatial: false);
        }

        /// <summary>
        /// Play environmental ambience
        /// </summary>
        public void SetAmbience(string ambienceName, float volume = 0.5f, float fadeInDuration = 3f)
        {
            AudioClip clip = LoadAudioClip(ambienceName);
            if (clip == null) return;

            if (!ambientSources.ContainsKey(ambienceName))
            {
                AudioSource source = GetAudioSource(priority: 200);
                source.outputAudioMixerGroup = ambientMixer;
                source.loop = true;
                source.spatialBlend = 0f;
                ambientSources[ambienceName] = source;
            }

            AudioSource ambSource = ambientSources[ambienceName];
            ambSource.clip = clip;
            ambSource.volume = 0f;
            ambSource.Play();
            FadeIn(ambSource, fadeInDuration, volume);
        }

        /// <summary>
        /// Stop environmental ambience
        /// </summary>
        public void StopAmbience(string ambienceName, float fadeOutDuration = 3f)
        {
            if (ambientSources.ContainsKey(ambienceName))
            {
                FadeOut(ambientSources[ambienceName], fadeOutDuration);
                ambientSources.Remove(ambienceName);
            }
        }

        /// <summary>
        /// Update audio occlusion for 3D sounds
        /// </summary>
        private void UpdateAudioOcclusion()
        {
            Transform listenerTransform = Camera.main.transform;

            foreach (var source in activeAudioSources.Where(s => s.spatialBlend > 0.5f))
            {
                Vector3 direction = source.transform.position - listenerTransform.position;
                float distance = direction.magnitude;

                if (Physics.Raycast(listenerTransform.position, direction.normalized, distance, occlusionMask))
                {
                    // Sound is occluded
                    if (!occludedSources.ContainsKey(source))
                    {
                        occludedSources[source] = source.volume;
                    }
                    source.volume = occludedSources[source] * 0.3f; // Reduce volume
                }
                else
                {
                    // Sound is not occluded
                    if (occludedSources.ContainsKey(source))
                    {
                        source.volume = occludedSources[source];
                        occludedSources.Remove(source);
                    }
                }
            }
        }

        /// <summary>
        /// Play sound with random pitch/volume variation
        /// </summary>
        public void PlaySFX(string soundName, Vector3 position, float volume = 1f, bool randomPitch = false, bool spatial = true, int priority = 128)
        {
            AudioClip clip = LoadAudioClip(soundName);
            PlaySFX(clip, position, volume, randomPitch, spatial, priority);
        }

        public void PlaySFX(AudioClip clip, Vector3 position, float volume = 1f, bool randomPitch = false, bool spatial = true, int priority = 128)
        {
            if (clip == null) return;

            AudioSource source = GetAudioSource(priority);
            if (source == null) return;

            source.clip = clip;
            source.volume = volume * channelVolumes[AudioChannel.SFX] * masterVolume;
            source.pitch = randomPitch ? Random.Range(0.9f, 1.1f) : 1f;
            source.spatialBlend = spatial ? 1f : 0f;
            source.outputAudioMixerGroup = sfxMixer;

            if (spatial)
            {
                source.transform.position = position;
            }

            source.Play();
        }
    }
}