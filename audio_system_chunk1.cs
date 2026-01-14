using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
using System;

namespace QuantumMechanic.Audio
{
    /// <summary>
    /// Audio channel types for categorized volume control
    /// </summary>
    public enum AudioChannel
    {
        Master,
        Music,
        SFX,
        Voice,
        Ambient,
        UI
    }

    /// <summary>
    /// Audio priority levels for interrupt logic
    /// </summary>
    public enum AudioPriority
    {
        Low = 0,
        Normal = 128,
        High = 200,
        Critical = 255
    }

    /// <summary>
    /// Cached audio clip data for pooling
    /// </summary>
    public class AudioClipData
    {
        public AudioClip clip;
        public AudioChannel channel;
        public float baseVolume = 1f;
        public float basePitch = 1f;
        public bool is3D = false;
        public int useCount = 0;
        public float lastUsedTime;
    }

    /// <summary>
    /// Pooled audio source wrapper
    /// </summary>
    public class PooledAudioSource
    {
        public AudioSource source;
        public bool isActive;
        public float startTime;
        public AudioChannel channel;
        public Action onComplete;
    }

    /// <summary>
    /// Audio fade operation data
    /// </summary>
    public class AudioFade
    {
        public AudioSource source;
        public float startVolume;
        public float targetVolume;
        public float duration;
        public float elapsed;
        public Action onComplete;
    }

    /// <summary>
    /// Master audio manager singleton - handles all audio playback, mixing, and effects
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;
        public static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("AudioManager");
                    instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixerGroup masterMixer;
        [SerializeField] private AudioMixerGroup musicMixer;
        [SerializeField] private AudioMixerGroup sfxMixer;
        [SerializeField] private AudioMixerGroup voiceMixer;
        [SerializeField] private AudioMixerGroup ambientMixer;
        [SerializeField] private AudioMixerGroup uiMixer;

        [Header("Audio Source Pool")]
        [SerializeField] private int initialPoolSize = 20;
        [SerializeField] private int maxPoolSize = 50;

        private Dictionary<AudioChannel, float> channelVolumes = new Dictionary<AudioChannel, float>();
        private Dictionary<string, AudioClipData> clipCache = new Dictionary<string, AudioClipData>();
        private List<PooledAudioSource> audioSourcePool = new List<PooledAudioSource>();
        private List<AudioFade> activeFades = new List<AudioFade>();

        private bool isMuted = false;
        private float masterVolume = 1f;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeChannelVolumes();
            InitializeAudioSourcePool();
            LoadAudioSettings();
        }

        /// <summary>
        /// Initialize default channel volumes
        /// </summary>
        private void InitializeChannelVolumes()
        {
            channelVolumes[AudioChannel.Master] = 1f;
            channelVolumes[AudioChannel.Music] = 0.8f;
            channelVolumes[AudioChannel.SFX] = 1f;
            channelVolumes[AudioChannel.Voice] = 1f;
            channelVolumes[AudioChannel.Ambient] = 0.6f;
            channelVolumes[AudioChannel.UI] = 0.9f;
        }

        /// <summary>
        /// Pre-create audio source pool for performance
        /// </summary>
        private void InitializeAudioSourcePool()
        {
            for (int i = 0; i < initialPoolSize; i++)
            {
                CreatePooledAudioSource();
            }
        }

        /// <summary>
        /// Create new pooled audio source
        /// </summary>
        private PooledAudioSource CreatePooledAudioSource()
        {
            GameObject go = new GameObject($"PooledAudioSource_{audioSourcePool.Count}");
            go.transform.SetParent(transform);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;

            PooledAudioSource pooled = new PooledAudioSource
            {
                source = source,
                isActive = false
            };

            audioSourcePool.Add(pooled);
            return pooled;
        }

        /// <summary>
        /// Get available audio source from pool
        /// </summary>
        private PooledAudioSource GetPooledAudioSource()
        {
            foreach (var pooled in audioSourcePool)
            {
                if (!pooled.isActive)
                {
                    pooled.isActive = true;
                    return pooled;
                }
            }

            if (audioSourcePool.Count < maxPoolSize)
            {
                return CreatePooledAudioSource();
            }

            // Steal oldest low-priority source
            PooledAudioSource oldest = audioSourcePool[0];
            foreach (var pooled in audioSourcePool)
            {
                if (pooled.isActive && pooled.startTime < oldest.startTime)
                {
                    oldest = pooled;
                }
            }

            oldest.source.Stop();
            oldest.onComplete?.Invoke();
            return oldest;
        }

        /// <summary>
        /// Return audio source to pool
        /// </summary>
        private void ReturnToPool(PooledAudioSource pooled)
        {
            pooled.isActive = false;
            pooled.source.Stop();
            pooled.source.clip = null;
            pooled.onComplete = null;
        }

        private void Update()
        {
            UpdateActiveFades();
            UpdateActiveAudioSources();
        }

        /// <summary>
        /// Update all active audio fades
        /// </summary>
        private void UpdateActiveFades()
        {
            for (int i = activeFades.Count - 1; i >= 0; i--)
            {
                AudioFade fade = activeFades[i];
                fade.elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(fade.elapsed / fade.duration);
                fade.source.volume = Mathf.Lerp(fade.startVolume, fade.targetVolume, t);

                if (t >= 1f)
                {
                    fade.onComplete?.Invoke();
                    activeFades.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Check and return completed audio sources to pool
        /// </summary>
        private void UpdateActiveAudioSources()
        {
            foreach (var pooled in audioSourcePool)
            {
                if (pooled.isActive && !pooled.source.isPlaying)
                {
                    ReturnToPool(pooled);
                }
            }
        }

        /// <summary>
        /// Play audio clip with full control
        /// </summary>
        public AudioSource PlaySound(AudioClip clip, AudioChannel channel = AudioChannel.SFX, 
            float volume = 1f, float pitch = 1f, bool loop = false, 
            Vector3? position = null, AudioPriority priority = AudioPriority.Normal,
            Action onComplete = null)
        {
            if (clip == null || isMuted) return null;

            PooledAudioSource pooled = GetPooledAudioSource();
            AudioSource source = pooled.source;

            // Configure audio source
            source.clip = clip;
            source.volume = volume * GetChannelVolume(channel) * masterVolume;
            source.pitch = pitch;
            source.loop = loop;
            source.priority = (int)priority;
            source.outputAudioMixerGroup = GetMixerForChannel(channel);

            // 3D spatial audio
            if (position.HasValue)
            {
                source.spatialBlend = 1f;
                source.transform.position = position.Value;
            }
            else
            {
                source.spatialBlend = 0f;
            }

            pooled.channel = channel;
            pooled.startTime = Time.time;
            pooled.onComplete = onComplete;

            source.Play();

            // Cache clip data
            CacheClipData(clip, channel, volume, pitch, position.HasValue);

            return source;
        }

        /// <summary>
        /// Cache audio clip metadata
        /// </summary>
        private void CacheClipData(AudioClip clip, AudioChannel channel, float volume, float pitch, bool is3D)
        {
            string key = clip.name;
            if (!clipCache.ContainsKey(key))
            {
                clipCache[key] = new AudioClipData
                {
                    clip = clip,
                    channel = channel,
                    baseVolume = volume,
                    basePitch = pitch,
                    is3D = is3D
                };
            }

            clipCache[key].useCount++;
            clipCache[key].lastUsedTime = Time.time;
        }

        /// <summary>
        /// Get mixer group for channel
        /// </summary>
        private AudioMixerGroup GetMixerForChannel(AudioChannel channel)
        {
            return channel switch
            {
                AudioChannel.Music => musicMixer,
                AudioChannel.SFX => sfxMixer,
                AudioChannel.Voice => voiceMixer,
                AudioChannel.Ambient => ambientMixer,
                AudioChannel.UI => uiMixer,
                _ => masterMixer
            };
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
        /// Get channel volume
        /// </summary>
        public float GetChannelVolume(AudioChannel channel)
        {
            return channelVolumes.TryGetValue(channel, out float vol) ? vol : 1f;
        }

        /// <summary>
        /// Fade audio source volume
        /// </summary>
        public void FadeVolume(AudioSource source, float targetVolume, float duration, Action onComplete = null)
        {
            if (source == null) return;

            AudioFade fade = new AudioFade
            {
                source = source,
                startVolume = source.volume,
                targetVolume = targetVolume,
                duration = duration,
                elapsed = 0f,
                onComplete = onComplete
            };

            activeFades.Add(fade);
        }

        /// <summary>
        /// Toggle global mute
        /// </summary>
        public void SetMuted(bool muted)
        {
            isMuted = muted;
            AudioListener.volume = muted ? 0f : 1f;
            SaveAudioSettings();
        }

        /// <summary>
        /// Set master volume
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            SaveAudioSettings();
        }

        /// <summary>
        /// Load audio settings from save system
        /// </summary>
        private void LoadAudioSettings()
        {
            masterVolume = PlayerPrefs.GetFloat("Audio_MasterVolume", 1f);
            isMuted = PlayerPrefs.GetInt("Audio_Muted", 0) == 1;

            foreach (AudioChannel channel in Enum.GetValues(typeof(AudioChannel)))
            {
                string key = $"Audio_{channel}Volume";
                if (PlayerPrefs.HasKey(key))
                {
                    channelVolumes[channel] = PlayerPrefs.GetFloat(key);
                }
            }
        }

        /// <summary>
        /// Save audio settings
        /// </summary>
        private void SaveAudioSettings()
        {
            PlayerPrefs.SetFloat("Audio_MasterVolume", masterVolume);
            PlayerPrefs.SetInt("Audio_Muted", isMuted ? 1 : 0);

            foreach (var kvp in channelVolumes)
            {
                PlayerPrefs.SetString($"Audio_{kvp.Key}Volume", kvp.Value.ToString());
            }

            PlayerPrefs.Save();
        }
    }
}