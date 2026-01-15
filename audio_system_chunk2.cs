using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;

namespace QuantumMechanic.Audio
{
    /// <summary>
    /// Music state types for dynamic transitions
    /// </summary>
    public enum MusicState
    {
        None,
        Menu,
        Exploration,
        Combat,
        Boss,
        Victory,
        Defeat,
        Cutscene
    }

    /// <summary>
    /// Music track data with metadata
    /// </summary>
    [Serializable]
    public class MusicTrack
    {
        public string trackName;
        public AudioClip clip;
        public MusicState state;
        public float intensity = 0.5f;
        public bool canLoop = true;
        public float crossfadeDuration = 2f;
    }

    /// <summary>
    /// Music layer for adaptive music system
    /// </summary>
    [Serializable]
    public class MusicLayer
    {
        public string layerName;
        public AudioClip clip;
        public float intensityThreshold; // Play when intensity >= this
        public float baseVolume = 1f;
    }

    /// <summary>
    /// Ambient sound zone configuration
    /// </summary>
    [Serializable]
    public class AmbientZone
    {
        public string zoneName;
        public AudioClip ambientClip;
        public float volume = 0.6f;
        public bool use3D = true;
        public float fadeInDuration = 1f;
        public float fadeOutDuration = 1f;
    }

    /// <summary>
    /// Weather sound configuration
    /// </summary>
    [Serializable]
    public class WeatherSound
    {
        public string weatherType;
        public AudioClip[] soundClips;
        public float volume = 0.7f;
        public float minInterval = 2f;
        public float maxInterval = 8f;
    }

    /// <summary>
    /// Music and ambience manager - handles dynamic music, ambient zones, and weather
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        private static MusicManager instance;
        public static MusicManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("MusicManager");
                    instance = go.AddComponent<MusicManager>();
                }
                return instance;
            }
        }

        [Header("Music Tracks")]
        [SerializeField] private List<MusicTrack> musicTracks = new List<MusicTrack>();

        [Header("Adaptive Music Layers")]
        [SerializeField] private List<MusicLayer> musicLayers = new List<MusicLayer>();

        [Header("Ambient Zones")]
        [SerializeField] private List<AmbientZone> ambientZones = new List<AmbientZone>();

        [Header("Weather Sounds")]
        [SerializeField] private List<WeatherSound> weatherSounds = new List<WeatherSound>();

        private AudioSource currentMusicSource;
        private AudioSource crossfadeMusicSource;
        private List<AudioSource> activeLayerSources = new List<AudioSource>();
        private AudioSource currentAmbientSource;
        private List<AudioSource> weatherSources = new List<AudioSource>();

        private MusicState currentMusicState = MusicState.None;
        private float currentMusicIntensity = 0f;
        private string currentAmbientZone = "";
        private string currentWeather = "";

        private bool isDayTime = true;
        private float timeOfDayTransitionProgress = 0f;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            InitializeMusicSources();
        }

        /// <summary>
        /// Initialize audio sources for music system
        /// </summary>
        private void InitializeMusicSources()
        {
            currentMusicSource = CreateMusicSource("MainMusic");
            crossfadeMusicSource = CreateMusicSource("CrossfadeMusic");
            currentAmbientSource = CreateMusicSource("Ambient");

            // Create layer sources
            for (int i = 0; i < musicLayers.Count; i++)
            {
                AudioSource layerSource = CreateMusicSource($"MusicLayer_{i}");
                activeLayerSources.Add(layerSource);
            }

            // Create weather sources
            for (int i = 0; i < 3; i++)
            {
                AudioSource weatherSource = CreateMusicSource($"Weather_{i}");
                weatherSources.Add(weatherSource);
            }
        }

        /// <summary>
        /// Create configured music audio source
        /// </summary>
        private AudioSource CreateMusicSource(string sourceName)
        {
            GameObject go = new GameObject(sourceName);
            go.transform.SetParent(transform);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.volume = 0f;
            return source;
        }

        /// <summary>
        /// Play music for specific state with crossfade
        /// </summary>
        public void PlayMusicForState(MusicState state, float crossfadeDuration = 2f)
        {
            if (state == currentMusicState) return;

            MusicTrack track = GetTrackForState(state);
            if (track == null)
            {
                Debug.LogWarning($"No music track found for state: {state}");
                return;
            }

            currentMusicState = state;
            PlayMusicWithCrossfade(track, crossfadeDuration);
        }

        /// <summary>
        /// Get music track for state
        /// </summary>
        private MusicTrack GetTrackForState(MusicState state)
        {
            return musicTracks.Find(t => t.state == state);
        }

        /// <summary>
        /// Play music with crossfade transition
        /// </summary>
        private void PlayMusicWithCrossfade(MusicTrack track, float duration)
        {
            if (currentMusicSource.isPlaying)
            {
                // Swap sources for crossfade
                AudioSource temp = currentMusicSource;
                currentMusicSource = crossfadeMusicSource;
                crossfadeMusicSource = temp;

                // Fade out old music
                AudioManager.Instance.FadeVolume(crossfadeMusicSource, 0f, duration, () =>
                {
                    crossfadeMusicSource.Stop();
                });
            }

            // Setup new music
            currentMusicSource.clip = track.clip;
            currentMusicSource.loop = track.canLoop;
            currentMusicSource.volume = 0f;
            currentMusicSource.Play();

            // Fade in new music
            float targetVolume = AudioManager.Instance.GetChannelVolume(AudioChannel.Music);
            AudioManager.Instance.FadeVolume(currentMusicSource, targetVolume, duration);
        }

        /// <summary>
        /// Set music intensity for adaptive layers (0-1)
        /// </summary>
        public void SetMusicIntensity(float intensity)
        {
            currentMusicIntensity = Mathf.Clamp01(intensity);
            UpdateMusicLayers();
        }

        /// <summary>
        /// Update adaptive music layers based on intensity
        /// </summary>
        private void UpdateMusicLayers()
        {
            for (int i = 0; i < musicLayers.Count && i < activeLayerSources.Count; i++)
            {
                MusicLayer layer = musicLayers[i];
                AudioSource source = activeLayerSources[i];

                bool shouldPlay = currentMusicIntensity >= layer.intensityThreshold;

                if (shouldPlay && !source.isPlaying)
                {
                    source.clip = layer.clip;
                    source.volume = 0f;
                    source.Play();
                    AudioManager.Instance.FadeVolume(source, layer.baseVolume, 1f);
                }
                else if (!shouldPlay && source.isPlaying)
                {
                    AudioManager.Instance.FadeVolume(source, 0f, 1f, () => source.Stop());
                }
            }
        }

        /// <summary>
        /// Enter ambient sound zone
        /// </summary>
        public void EnterAmbientZone(string zoneName)
        {
            if (zoneName == currentAmbientZone) return;

            AmbientZone zone = ambientZones.Find(z => z.zoneName == zoneName);
            if (zone == null)
            {
                Debug.LogWarning($"Ambient zone not found: {zoneName}");
                return;
            }

            currentAmbientZone = zoneName;

            if (currentAmbientSource.isPlaying)
            {
                AudioManager.Instance.FadeVolume(currentAmbientSource, 0f, zone.fadeOutDuration, () =>
                {
                    PlayAmbientZone(zone);
                });
            }
            else
            {
                PlayAmbientZone(zone);
            }
        }

        /// <summary>
        /// Play ambient zone audio
        /// </summary>
        private void PlayAmbientZone(AmbientZone zone)
        {
            currentAmbientSource.clip = zone.ambientClip;
            currentAmbientSource.volume = 0f;
            currentAmbientSource.loop = true;
            currentAmbientSource.spatialBlend = zone.use3D ? 1f : 0f;
            currentAmbientSource.Play();

            float targetVolume = zone.volume * AudioManager.Instance.GetChannelVolume(AudioChannel.Ambient);
            AudioManager.Instance.FadeVolume(currentAmbientSource, targetVolume, zone.fadeInDuration);
        }

        /// <summary>
        /// Exit current ambient zone
        /// </summary>
        public void ExitAmbientZone()
        {
            if (string.IsNullOrEmpty(currentAmbientZone)) return;

            AudioManager.Instance.FadeVolume(currentAmbientSource, 0f, 1f, () =>
            {
                currentAmbientSource.Stop();
            });

            currentAmbientZone = "";
        }

        /// <summary>
        /// Set weather type and play weather sounds
        /// </summary>
        public void SetWeather(string weatherType)
        {
            if (weatherType == currentWeather) return;

            StopAllWeatherSounds();
            currentWeather = weatherType;

            if (!string.IsNullOrEmpty(weatherType))
            {
                WeatherSound weather = weatherSounds.Find(w => w.weatherType == weatherType);
                if (weather != null)
                {
                    StartCoroutine(PlayWeatherSoundsRoutine(weather));
                }
            }
        }

        /// <summary>
        /// Play weather sounds with random intervals
        /// </summary>
        private IEnumerator PlayWeatherSoundsRoutine(WeatherSound weather)
        {
            while (currentWeather == weather.weatherType)
            {
                if (weather.soundClips.Length > 0)
                {
                    AudioClip clip = weather.soundClips[UnityEngine.Random.Range(0, weather.soundClips.Length)];
                    AudioSource source = GetAvailableWeatherSource();

                    if (source != null)
                    {
                        AudioManager.Instance.PlaySound(clip, AudioChannel.Ambient, 
                            weather.volume, 1f, false, null, AudioPriority.Low);
                    }
                }

                float waitTime = UnityEngine.Random.Range(weather.minInterval, weather.maxInterval);
                yield return new WaitForSeconds(waitTime);
            }
        }

        /// <summary>
        /// Get available weather audio source
        /// </summary>
        private AudioSource GetAvailableWeatherSource()
        {
            foreach (var source in weatherSources)
            {
                if (!source.isPlaying) return source;
            }
            return weatherSources[0];
        }

        /// <summary>
        /// Stop all weather sounds
        /// </summary>
        private void StopAllWeatherSounds()
        {
            foreach (var source in weatherSources)
            {
                if (source.isPlaying)
                {
                    AudioManager.Instance.FadeVolume(source, 0f, 0.5f, () => source.Stop());
                }
            }
        }

        /// <summary>
        /// Set time of day for ambient transitions
        /// </summary>
        public void SetTimeOfDay(bool isDay, float transitionDuration = 3f)
        {
            if (isDayTime == isDay) return;

            isDayTime = isDay;
            StartCoroutine(TransitionTimeOfDay(transitionDuration));
        }

        /// <summary>
        /// Smoothly transition day/night ambience
        /// </summary>
        private IEnumerator TransitionTimeOfDay(float duration)
        {
            float elapsed = 0f;
            float startVolume = currentAmbientSource.volume;
            float targetVolume = isDayTime ? 0.6f : 0.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                timeOfDayTransitionProgress = elapsed / duration;

                currentAmbientSource.volume = Mathf.Lerp(startVolume, targetVolume, timeOfDayTransitionProgress);
                yield return null;
            }

            currentAmbientSource.volume = targetVolume;
        }

        /// <summary>
        /// Stop all music immediately
        /// </summary>
        public void StopMusic(float fadeOutDuration = 1f)
        {
            if (currentMusicSource.isPlaying)
            {
                AudioManager.Instance.FadeVolume(currentMusicSource, 0f, fadeOutDuration, () =>
                {
                    currentMusicSource.Stop();
                });
            }

            foreach (var layerSource in activeLayerSources)
            {
                if (layerSource.isPlaying)
                {
                    AudioManager.Instance.FadeVolume(layerSource, 0f, fadeOutDuration, () =>
                    {
                        layerSource.Stop();
                    });
                }
            }

            currentMusicState = MusicState.None;
        }

        /// <summary>
        /// Play one-shot audio with random pitch variation
        /// </summary>
        public void PlayOneShotWithVariation(AudioClip clip, float volumeVariation = 0.1f, float pitchVariation = 0.1f)
        {
            float volume = 1f + UnityEngine.Random.Range(-volumeVariation, volumeVariation);
            float pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);

            AudioManager.Instance.PlaySound(clip, AudioChannel.SFX, volume, pitch, false);
        }

        /// <summary>
        /// Get current music state
        /// </summary>
        public MusicState GetCurrentMusicState()
        {
            return currentMusicState;
        }

        /// <summary>
        /// Get current music intensity
        /// </summary>
        public float GetCurrentIntensity()
        {
            return currentMusicIntensity;
        }
    }
}