using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuantumMechanic.VFX
{
    /// <summary>
    /// Manages particle system pooling, spawning, and lifecycle
    /// </summary>
    public class ParticleManager : MonoBehaviour
    {
        public static ParticleManager Instance { get; private set; }

        [Header("Pool Settings")]
        [SerializeField] private int defaultPoolSize = 20;
        [SerializeField] private int maxPoolSize = 100;
        [SerializeField] private bool autoExpand = true;

        [Header("Performance")]
        [SerializeField] private int maxActiveParticles = 500;
        [SerializeField] private bool cullDistantParticles = true;
        [SerializeField] private float cullDistance = 50f;

        private Dictionary<string, ParticlePool> particlePools = new Dictionary<string, ParticlePool>();
        private List<ParticleEffect> activeEffects = new List<ParticleEffect>();
        private Transform poolContainer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            poolContainer = new GameObject("ParticlePool").transform;
            poolContainer.SetParent(transform);
        }

        /// <summary>
        /// Preload particle effect into pool
        /// </summary>
        public void PreloadEffect(string effectName, GameObject prefab, int count = 0)
        {
            if (particlePools.ContainsKey(effectName))
                return;

            int poolSize = count > 0 ? count : defaultPoolSize;
            ParticlePool pool = new ParticlePool(effectName, prefab, poolSize, poolContainer);
            particlePools[effectName] = pool;
        }

        /// <summary>
        /// Spawn particle effect at position with rotation
        /// </summary>
        public ParticleEffect SpawnEffect(string effectName, Vector3 position, Quaternion rotation = default)
        {
            if (!particlePools.ContainsKey(effectName))
            {
                Debug.LogWarning($"Particle effect '{effectName}' not preloaded");
                return null;
            }

            if (activeEffects.Count >= maxActiveParticles)
            {
                ReturnOldestEffect();
            }

            ParticleEffect effect = particlePools[effectName].Get();
            effect.transform.position = position;
            effect.transform.rotation = rotation == default ? Quaternion.identity : rotation;
            effect.Play();

            activeEffects.Add(effect);
            return effect;
        }

        /// <summary>
        /// Spawn effect aligned to surface normal
        /// </summary>
        public ParticleEffect SpawnEffect(string effectName, Vector3 position, Vector3 normal)
        {
            Quaternion rotation = Quaternion.LookRotation(normal);
            return SpawnEffect(effectName, position, rotation);
        }

        /// <summary>
        /// Return particle effect to pool
        /// </summary>
        public void ReturnEffect(ParticleEffect effect)
        {
            if (effect == null) return;

            activeEffects.Remove(effect);
            effect.Stop();
            effect.gameObject.SetActive(false);
        }

        private void ReturnOldestEffect()
        {
            if (activeEffects.Count > 0)
            {
                ReturnEffect(activeEffects[0]);
            }
        }

        private void Update()
        {
            // Clean up finished effects
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                if (activeEffects[i].IsFinished)
                {
                    ReturnEffect(activeEffects[i]);
                }
            }

            // Cull distant particles
            if (cullDistantParticles && Camera.main != null)
            {
                Vector3 camPos = Camera.main.transform.position;
                for (int i = activeEffects.Count - 1; i >= 0; i--)
                {
                    float dist = Vector3.Distance(camPos, activeEffects[i].transform.position);
                    if (dist > cullDistance)
                    {
                        ReturnEffect(activeEffects[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Get performance statistics
        /// </summary>
        public ParticleStats GetStats()
        {
            return new ParticleStats
            {
                ActiveCount = activeEffects.Count,
                PooledCount = GetTotalPooledCount(),
                MaxParticles = maxActiveParticles
            };
        }

        private int GetTotalPooledCount()
        {
            int total = 0;
            foreach (var pool in particlePools.Values)
            {
                total += pool.AvailableCount;
            }
            return total;
        }
    }

    /// <summary>
    /// Wrapper for Unity particle system with enhanced control
    /// </summary>
    public class ParticleEffect : MonoBehaviour
    {
        private ParticleSystem[] particleSystems;
        private float startTime;
        private float maxDuration;
        private Color originalColor;
        private float originalSize;

        public bool IsFinished => Time.time - startTime > maxDuration && !IsPlaying();

        private void Awake()
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>();
            CalculateMaxDuration();
        }

        private void CalculateMaxDuration()
        {
            maxDuration = 0f;
            foreach (var ps in particleSystems)
            {
                float duration = ps.main.duration + ps.main.startLifetime.constantMax;
                if (duration > maxDuration)
                    maxDuration = duration;
            }
        }

        public void Play()
        {
            startTime = Time.time;
            foreach (var ps in particleSystems)
            {
                ps.Play();
            }
        }

        public void Stop()
        {
            foreach (var ps in particleSystems)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void Pause()
        {
            foreach (var ps in particleSystems)
            {
                ps.Pause();
            }
        }

        public bool IsPlaying()
        {
            foreach (var ps in particleSystems)
            {
                if (ps.isPlaying) return true;
            }
            return false;
        }

        public void SetColor(Color color)
        {
            foreach (var ps in particleSystems)
            {
                var main = ps.main;
                main.startColor = color;
            }
        }

        public void SetSize(float sizeMultiplier)
        {
            foreach (var ps in particleSystems)
            {
                var main = ps.main;
                main.startSizeMultiplier = sizeMultiplier;
            }
        }
    }

    /// <summary>
    /// Object pool for particle systems
    /// </summary>
    internal class ParticlePool
    {
        private string effectName;
        private GameObject prefab;
        private Queue<ParticleEffect> available = new Queue<ParticleEffect>();
        private Transform container;

        public int AvailableCount => available.Count;

        public ParticlePool(string name, GameObject prefab, int initialSize, Transform parent)
        {
            this.effectName = name;
            this.prefab = prefab;
            this.container = new GameObject($"Pool_{name}").transform;
            this.container.SetParent(parent);

            for (int i = 0; i < initialSize; i++)
            {
                CreateNew();
            }
        }

        private ParticleEffect CreateNew()
        {
            GameObject obj = GameObject.Instantiate(prefab, container);
            obj.SetActive(false);
            ParticleEffect effect = obj.GetComponent<ParticleEffect>();
            if (effect == null)
                effect = obj.AddComponent<ParticleEffect>();
            available.Enqueue(effect);
            return effect;
        }

        public ParticleEffect Get()
        {
            if (available.Count == 0)
                CreateNew();

            ParticleEffect effect = available.Dequeue();
            effect.gameObject.SetActive(true);
            return effect;
        }

        public void Return(ParticleEffect effect)
        {
            effect.gameObject.SetActive(false);
            available.Enqueue(effect);
        }
    }

    /// <summary>
    /// Performance statistics for particle system
    /// </summary>
    public struct ParticleStats
    {
        public int ActiveCount;
        public int PooledCount;
        public int MaxParticles;
    }
}