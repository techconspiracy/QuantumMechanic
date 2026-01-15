using UnityEngine;
using System.Collections.Generic;
using System;

namespace QuantumMechanic.Audio
{
    /// <summary>
    /// Surface type for footstep sounds
    /// </summary>
    public enum SurfaceType
    {
        Grass,
        Stone,
        Wood,
        Metal,
        Water,
        Sand,
        Snow,
        Mud
    }

    /// <summary>
    /// Footstep sound configuration
    /// </summary>
    [Serializable]
    public class FootstepSound
    {
        public SurfaceType surface;
        public AudioClip[] footstepClips;
        public float volume = 0.5f;
        public float pitchVariation = 0.1f;
    }

    /// <summary>
    /// Voice line data for dialogue
    /// </summary>
    [Serializable]
    public class VoiceLine
    {
        public string lineId;
        public AudioClip clip;
        public string characterName;
        public float volume = 1f;
    }

    /// <summary>
    /// Audio snapshot for environmental effects
    /// </summary>
    [Serializable]
    public class AudioSnapshot
    {
        public string snapshotName;
        public float lowpassCutoff = 22000f;
        public float reverbAmount = 0f;
        public float volumeReduction = 0f;
    }

    /// <summary>
    /// Audio event trigger data
    /// </summary>
    public class AudioEvent
    {
        public string eventName;
        public AudioClip clip;
        public AudioChannel channel;
        public float volume;
        public Action callback;
    }

    /// <summary>
    /// Advanced audio features - footsteps, voice, occlusion, events, debugging
    /// </summary>
    public class AdvancedAudioSystem : MonoBehaviour
    {
        private static AdvancedAudioSystem instance;
        public static AdvancedAudioSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("AdvancedAudioSystem");
                    instance = go.AddComponent<AdvancedAudioSystem>();
                }
                return instance;
            }
        }

        [Header("Footstep System")]
        [SerializeField] private List<FootstepSound> footstepSounds = new List<FootstepSound>();
        [SerializeField] private float footstepInterval = 0.5f;

        [Header("Voice Lines")]
        [SerializeField] private List<VoiceLine> voiceLines = new List<VoiceLine>();

        [Header("Audio Snapshots")]
        [SerializeField] private List<AudioSnapshot> audioSnapshots = new List<AudioSnapshot>();

        [Header("Audio Occlusion")]
        [SerializeField] private bool enableOcclusion = true;
        [SerializeField] private LayerMask occlusionLayers;
        [SerializeField] private float occlusionCheckInterval = 0.1f;

        [Header("Debug")]
        [SerializeField] private bool showDebugOverlay = false;
        [SerializeField] private int maxDebugSounds = 10;

        private Dictionary<string, AudioEvent> registeredEvents = new Dictionary<string, AudioEvent>();
        private List<AudioSource> occludedSources = new List<AudioSource>();
        private List<string> recentSounds = new List<string>();

        private float lastFootstepTime = 0f;
        private AudioSource currentVoiceSource;
        private AudioSnapshot currentSnapshot;

        private float lastOcclusionCheckTime = 0f;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            InitializeVoiceSource();
        }

        /// <summary>
        /// Initialize voice line audio source
        /// </summary>
        private void InitializeVoiceSource()
        {
            GameObject go = new GameObject("VoiceSource");
            go.transform.SetParent(transform);
            currentVoiceSource = go.AddComponent<AudioSource>();
            currentVoiceSource.playOnAwake = false;
        }

        private void Update()
        {
            if (enableOcclusion && Time.time - lastOcclusionCheckTime > occlusionCheckInterval)
            {
                UpdateAudioOcclusion();
                lastOcclusionCheckTime = Time.time;
            }
        }

        /// <summary>
        /// Play footstep sound based on surface type
        /// </summary>
        public void PlayFootstep(SurfaceType surface, Vector3 position)
        {
            if (Time.time - lastFootstepTime < footstepInterval) return;

            FootstepSound footstep = footstepSounds.Find(f => f.surface == surface);
            if (footstep == null || footstep.footstepClips.Length == 0) return;

            AudioClip clip = footstep.footstepClips[UnityEngine.Random.Range(0, footstep.footstepClips.Length)];
            float pitch = 1f + UnityEngine.Random.Range(-footstep.pitchVariation, footstep.pitchVariation);

            AudioManager.Instance.PlaySound(clip, AudioChannel.SFX, footstep.volume, pitch, false, position);

            lastFootstepTime = Time.time;
            LogSoundPlayed($"Footstep ({surface})");
        }

        /// <summary>
        /// Detect surface type at position (raycast downward)
        /// </summary>
        public SurfaceType DetectSurfaceType(Vector3 position)
        {
            RaycastHit hit;
            if (Physics.Raycast(position + Vector3.up * 0.1f, Vector3.down, out hit, 0.5f))
            {
                // Check terrain or mesh for surface type
                // This would integrate with terrain system or material tags
                if (hit.collider.CompareTag("Grass")) return SurfaceType.Grass;
                if (hit.collider.CompareTag("Stone")) return SurfaceType.Stone;
                if (hit.collider.CompareTag("Wood")) return SurfaceType.Wood;
                if (hit.collider.CompareTag("Metal")) return SurfaceType.Metal;
            }

            return SurfaceType.Stone; // Default
        }

        /// <summary>
        /// Play voice line by ID
        /// </summary>
        public void PlayVoiceLine(string lineId, Action onComplete = null)
        {
            VoiceLine line = voiceLines.Find(v => v.lineId == lineId);
            if (line == null)
            {
                Debug.LogWarning($"Voice line not found: {lineId}");
                return;
            }

            if (currentVoiceSource.isPlaying)
            {
                currentVoiceSource.Stop();
            }

            currentVoiceSource.clip = line.clip;
            currentVoiceSource.volume = line.volume * AudioManager.Instance.GetChannelVolume(AudioChannel.Voice);
            currentVoiceSource.Play();

            LogSoundPlayed($"Voice: {line.characterName} - {lineId}");

            if (onComplete != null)
            {
                StartCoroutine(WaitForVoiceComplete(onComplete));
            }
        }

        /// <summary>
        /// Wait for voice line to complete
        /// </summary>
        private System.Collections.IEnumerator WaitForVoiceComplete(Action callback)
        {
            yield return new WaitWhile(() => currentVoiceSource.isPlaying);
            callback?.Invoke();
        }

        /// <summary>
        /// Stop current voice line
        /// </summary>
        public void StopVoiceLine()
        {
            if (currentVoiceSource.isPlaying)
            {
                currentVoiceSource.Stop();
            }
        }

        /// <summary>
        /// Apply audio snapshot (environmental audio effects)
        /// </summary>
        public void ApplyAudioSnapshot(string snapshotName, float transitionTime = 1f)
        {
            AudioSnapshot snapshot = audioSnapshots.Find(s => s.snapshotName == snapshotName);
            if (snapshot == null)
            {
                Debug.LogWarning($"Audio snapshot not found: {snapshotName}");
                return;
            }

            currentSnapshot = snapshot;
            // Apply lowpass filter, reverb, etc.
            // This would integrate with Unity's Audio Mixer snapshots
            LogSoundPlayed($"Snapshot: {snapshotName}");
        }

        /// <summary>
        /// Update audio occlusion for 3D sounds
        /// </summary>
        private void UpdateAudioOcclusion()
        {
            Transform listenerTransform = Camera.main?.transform;
            if (listenerTransform == null) return;

            foreach (var source in occludedSources)
            {
                if (source == null || !source.isPlaying) continue;

                Vector3 direction = source.transform.position - listenerTransform.position;
                float distance = direction.magnitude;

                if (Physics.Raycast(listenerTransform.position, direction.normalized, distance, occlusionLayers))
                {
                    // Sound is occluded - apply lowpass filter
                    source.volume *= 0.5f;
                }
            }
        }

        /// <summary>
        /// Register audio event for triggering
        /// </summary>
        public void RegisterAudioEvent(string eventName, AudioClip clip, AudioChannel channel = AudioChannel.SFX, float volume = 1f)
        {
            registeredEvents[eventName] = new AudioEvent
            {
                eventName = eventName,
                clip = clip,
                channel = channel,
                volume = volume
            };
        }

        /// <summary>
        /// Trigger registered audio event
        /// </summary>
        public void TriggerAudioEvent(string eventName, Vector3? position = null)
        {
            if (registeredEvents.TryGetValue(eventName, out AudioEvent audioEvent))
            {
                AudioManager.Instance.PlaySound(audioEvent.clip, audioEvent.channel, 
                    audioEvent.volume, 1f, false, position);

                audioEvent.callback?.Invoke();
                LogSoundPlayed($"Event: {eventName}");
            }
            else
            {
                Debug.LogWarning($"Audio event not registered: {eventName}");
            }
        }

        /// <summary>
        /// Log sound for debug overlay
        /// </summary>
        private void LogSoundPlayed(string soundInfo)
        {
            if (!showDebugOverlay) return;

            recentSounds.Insert(0, $"{Time.time:F2}s - {soundInfo}");
            if (recentSounds.Count > maxDebugSounds)
            {
                recentSounds.RemoveAt(recentSounds.Count - 1);
            }
        }

        /// <summary>
        /// Get audio performance stats
        /// </summary>
        public AudioStats GetAudioStats()
        {
            return new AudioStats
            {
                activeSources = FindObjectsOfType<AudioSource>().Length,
                playingSources = GetPlayingSourceCount(),
                cachedClips = AudioManager.Instance != null ? 0 : 0, // Would integrate with clip cache
                memoryUsageMB = 0f // Would calculate from loaded clips
            };
        }

        /// <summary>
        /// Get count of playing audio sources
        /// </summary>
        private int GetPlayingSourceCount()
        {
            int count = 0;
            foreach (var source in FindObjectsOfType<AudioSource>())
            {
                if (source.isPlaying) count++;
            }
            return count;
        }

        private void OnGUI()
        {
            if (!showDebugOverlay) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            GUILayout.Box("=== AUDIO DEBUG ===");

            AudioStats stats = GetAudioStats();
            GUILayout.Label($"Active Sources: {stats.activeSources}");
            GUILayout.Label($"Playing: {stats.playingSources}");
            GUILayout.Label($"Memory: {stats.memoryUsageMB:F2} MB");

            GUILayout.Space(10);
            GUILayout.Label("Recent Sounds:");

            foreach (var sound in recentSounds)
            {
                GUILayout.Label(sound);
            }

            GUILayout.EndArea();
        }
    }

    /// <summary>
    /// Audio performance statistics
    /// </summary>
    public struct AudioStats
    {
        public int activeSources;
        public int playingSources;
        public int cachedClips;
        public float memoryUsageMB;
    }
}