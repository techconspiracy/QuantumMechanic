// File: Assets/Scripts/RPG/Modules/HealthModule_WebSocket.cs
// REFACTORED VERSION - Works with WebSocket networking instead of NGO

using UnityEngine;
using RPG.Core;
using RPG.Contracts;
using RPG.Networking;
using System;

namespace RPG.Modules
{
    /// <summary>
    /// Health system refactored for WebSocket networking.
    /// Syncs health state via custom messages instead of NetworkVariables.
    /// </summary>
    public class HealthModule : BaseNetworkModule, IDamageable, IResourcePool
    {
        [Header("Health Configuration")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _startingHealth = 100f;
        [SerializeField] private bool _canRegenerate = false;
        [SerializeField] private float _regenerationRate = 5f;
        [SerializeField] private float _regenerationDelay = 3f;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject _deathVFX;
        [SerializeField] private AudioClip _deathSound;
        [SerializeField] private AudioClip _damageSound;

        // State (replaces NetworkVariables)
        private float _currentHealth;
        private float _currentMaxHealth;
        private bool _isDead;
        private float _timeSinceLastDamage;

        private AudioSource _audioSource;
        private MeshRenderer _meshRenderer;

        // Events for UI binding
        public event Action<float, float> OnHealthChanged; // (current, max)
        public event Action OnDeath;

        #region IResourcePool Implementation

        public float CurrentValue => _currentHealth;
        public float MaxValue => _currentMaxHealth;

        public void ModifyResource(float delta)
        {
            if (!IsOwner) return; // Only owner modifies

            float newHealth = Mathf.Clamp(_currentHealth + delta, 0, _currentMaxHealth);
            
            if (Mathf.Abs(newHealth - _currentHealth) > 0.01f)
            {
                _currentHealth = newHealth;
                OnHealthChanged?.Invoke(_currentHealth, _currentMaxHealth);
                SyncHealthToServer();

                if (_currentHealth <= 0 && !_isDead)
                {
                    Die();
                }
            }
        }

        public void SetMaxValue(float newMax)
        {
            if (!IsOwner) return;

            _currentMaxHealth = Mathf.Max(0, newMax);
            _currentHealth = Mathf.Min(_currentHealth, _currentMaxHealth);
            OnHealthChanged?.Invoke(_currentHealth, _currentMaxHealth);
            SyncHealthToServer();
        }

        #endregion

        #region IDamageable Implementation

        public bool IsDead => _isDead;

        public void TakeDamage(float amount, ulong attackerId)
        {
            if (_isDead) return;

            _timeSinceLastDamage = 0f;
            ModifyResource(-amount);

            // Play damage feedback
            if (_damageSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_damageSound);
            }

            LogInfo($"Took {amount} damage from {attackerId}. Health: {_currentHealth}/{_currentMaxHealth}");
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1f;
            }

            _meshRenderer = GetComponent<MeshRenderer>();
        }

        protected override void Start()
        {
            base.Start();

            // Initialize health
            _currentHealth = _startingHealth;
            _currentMaxHealth = _maxHealth;
            _isDead = false;

            OnHealthChanged?.Invoke(_currentHealth, _currentMaxHealth);
        }

        private void Update()
        {
            if (!IsOwner || _isDead) return;

            // Handle regeneration
            if (_canRegenerate && _currentHealth < _currentMaxHealth)
            {
                _timeSinceLastDamage += Time.deltaTime;

                if (_timeSinceLastDamage >= _regenerationDelay)
                {
                    ModifyResource(_regenerationRate * Time.deltaTime);
                }
            }
        }

        #endregion

        #region Network Synchronization

        private void SyncHealthToServer()
        {
            if (!IsOwner) return;

            var healthData = new HealthSyncData
            {
                currentHealth = _currentHealth,
                maxHealth = _currentMaxHealth,
                isDead = _isDead
            };

            SendNetworkMessage(
                MessageType.ResourceUpdate,
                JsonUtility.ToJson(healthData)
            );
        }

        protected override void HandleNetworkMessage(NetworkMessage message)
        {
            base.HandleNetworkMessage(message);

            // Remote players receive health updates
            if (message.messageType == MessageType.ResourceUpdate && !IsOwner)
            {
                if (!string.IsNullOrEmpty(message.payload))
                {
                    HealthSyncData data = JsonUtility.FromJson<HealthSyncData>(message.payload);
                    
                    _currentHealth = data.currentHealth;
                    _currentMaxHealth = data.maxHealth;
                    
                    if (data.isDead && !_isDead)
                    {
                        _isDead = true;
                        PlayDeathEffects();
                    }

                    OnHealthChanged?.Invoke(_currentHealth, _currentMaxHealth);
                }
            }
        }

        #endregion

        #region Death Handling

        private void Die()
        {
            _isDead = true;
            OnDeath?.Invoke();
            
            LogInfo("Entity died!");
            
            // Sync death to server
            SyncHealthToServer();
            
            // Play death effects
            PlayDeathEffects();
        }

        private void PlayDeathEffects()
        {
            // VFX
            if (_deathVFX != null)
            {
                Instantiate(_deathVFX, transform.position, transform.rotation);
            }

            // SFX
            if (_deathSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_deathSound);
            }

            // Hide mesh
            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = false;
            }

            // Destroy after delay (or handle respawn)
            if (IsOwner)
            {
                Destroy(gameObject, 5f);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Heal the entity by a specific amount
        /// </summary>
        public void Heal(float amount)
        {
            ModifyResource(amount);
        }

        /// <summary>
        /// Instantly kill the entity (for admin/debug)
        /// </summary>
        public void Kill()
        {
            if (!_isDead)
            {
                _currentHealth = 0;
                Die();
            }
        }

        /// <summary>
        /// Fully restore health
        /// </summary>
        public void FullHeal()
        {
            _currentHealth = _currentMaxHealth;
            OnHealthChanged?.Invoke(_currentHealth, _currentMaxHealth);
            SyncHealthToServer();
        }

        #endregion
    }

    [Serializable]
    public class HealthSyncData
    {
        public float currentHealth;
        public float maxHealth;
        public bool isDead;
    }
}