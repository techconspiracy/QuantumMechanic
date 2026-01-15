using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
using System.Linq;

namespace QuantumMechanic.Audio
{
    /// <summary>
    /// Core audio management system with channel-based mixing and spatial audio
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;
        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<AudioManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("AudioManager");
                        _instance = go.AddComponent<AudioManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixerGroup masterMixer;
        [SerializeField] private AudioMixerGroup musicMixer;
        [SerializeField] private AudioMixerGroup sfxMixer;
        [SerializeField] private AudioMixerGroup ambientMixer;
        [SerializeField] private AudioMixerGroup voiceMixer;
        [SerializeField] private AudioMixerGroup uiMixer;

        [Header("Audio Pool Settings")]
        [SerializeField] private int initialPoolSize = 20;
        [SerializeField] private int maxPoolSize = 50;

        [Header("3D Audio Settings")]
        [SerializeField] private float maxAudioDistance = 100f;
        [SerializeField] private AnimationCurve distanceAttenuation = AnimationCurve.EaseInOut(0, 1, 1, 0);
        [SerializeField] private float dopplerLevel = 1f;

        // Audio channels
        private Dictionary<AudioChannel, float> channelVolumes = new Dictionary<AudioChannel, float>();
        private Dictionary<AudioChannel, AudioMixerGroup> channelMixers = new Dictionary<AudioChannel, AudioMixerGroup>();

        // Audio source pool
        private Queue<AudioSource> audioSourcePool = new Queue<AudioSource>();
        private List<AudioSource> activeAudioSources = new List<AudioSource>();

        // Dynamic audio loading
        private Dictionary<string, AudioClip> loadedClips = new Dictionary<string, AudioClip>();
        private Dictionary<AudioSource, float> fadingAudioSources = new Dictionary<AudioSource, float>();

        // Audio settings
        private float masterVolume = 1f;
        private bool audioMuted = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAudioSystem();
            LoadAudioSettings();
        }

        /// <summary>
        /// Initialize audio channel system and pooling
        /// </summary>
        private void InitializeAudioSystem()
        {
            // Setup channel mixers
            channelMixers[AudioChannel.Music] = musicMixer;
            channelMixers[AudioChannel.SFX] = sfxMixer;
            channelMixers[AudioChannel.Ambient] = ambientMixer;
            channelMixers[AudioChannel.Voice] = voiceMixer;
            channelMixers[AudioChannel.UI] = uiMixer;

            // Initialize channel volumes
            foreach (AudioChannel channel in System.Enum.GetValues(typeof(AudioChannel)))
            {
                channelVolumes[channel] = 1f;
            }

            // Create initial audio source pool
            for (int i = 0; i < initialPoolSize; i++)
            {
                CreatePooledAudioSource();
            }
        }

        /// <summary>
        /// Create a new pooled audio source
        /// </summary>
        private AudioSource CreatePooledAudioSource()
        {
            GameObject audioObj = new GameObject($"PooledAudio_{audioSourcePool.Count}");
            audioObj.transform.SetParent(transform);
            AudioSource source = audioObj.AddComponent<AudioSource>();
            
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = dopplerLevel;
            source.maxDistance = maxAudioDistance;
            
            audioSourcePool.Enqueue(source);
            return source;
        }

        /// <summary>
        /// Get audio source from pool with priority system
        /// </summary>
        private AudioSource GetAudioSource(int priority = 128)
        {
            AudioSource source = null;

            // Try to get from pool
            if (audioSourcePool.Count > 0)
            {
                source = audioSourcePool.Dequeue();
            }
            // Create new if under max pool size
            else if (activeAudioSources.Count < maxPoolSize)
            {
                source = CreatePooledAudioSource();
                audioSourcePool.Dequeue(); // Remove it since we just added it
            }
            // Steal lowest priority active source
            else
            {
                var lowestPriority = activeAudioSources.OrderBy(s => s.priority).FirstOrDefault();
                if (lowestPriority != null && lowestPriority.priority < priority)
                {
                    lowestPriority.Stop();
                    source = lowestPriority;
                    activeAudioSources.Remove(source);
                }
            }

            if (source != null)
            {
                source.priority = priority;
                activeAudioSources.Add(source);
            }

            return source;
        }

        /// <summary>
        /// Return audio source to pool
        /// </summary>
        private void ReturnAudioSource(AudioSource source)
        {
            if (source == null) return;

            activeAudioSources.Remove(source);
            source.clip = null;
            source.Stop();
            source.gameObject.SetActive(false);
            audioSourcePool.Enqueue(source);
        }

        private void Update()
        {
            UpdateFadingAudio();
            CleanupFinishedAudio();
        }

        /// <summary>
        /// Update audio sources that are fading
        /// </summary>
        private void UpdateFadingAudio()
        {
            var fadingKeys = fadingAudioSources.Keys.ToList();
            foreach (var source in fadingKeys)
            {
                if (source == null || !source.isPlaying)
                {
                    fadingAudioSources.Remove(source);
                    continue;
                }

                float targetVolume = fadingAudioSources[source];
                source.volume = Mathf.MoveTowards(source.volume, targetVolume, Time.deltaTime * 2f);

                if (Mathf.Approximately(source.volume, targetVolume))
                {
                    fadingAudioSources.Remove(source);
                    if (targetVolume <= 0.01f)
                    {
                        source.Stop();
                        ReturnAudioSource(source);
                    }
                }
            }
        }

        /// <summary>
        /// Cleanup audio sources that finished playing
        /// </summary>
        private void CleanupFinishedAudio()
        {
            for (int i = activeAudioSources.Count - 1; i >= 0; i--)
            {
                if (activeAudioSources[i] != null && !activeAudioSources[i].isPlaying && !fadingAudioSources.ContainsKey(activeAudioSources[i]))
                {
                    ReturnAudioSource(activeAudioSources[i]);
                }
            }
        }
    }

    /// <summary>
    /// Audio channel types for mixing
    /// </summary>
    public enum AudioChannel
    {
        Music,
        SFX,
        Ambient,
        Voice,
        UI
    }
}