// AUTO-GENERATED FILE - DO NOT EDIT
// Regenerate using RPG Tools > Atomic Module Generator

using Unity.Netcode;
using UnityEngine;
using RPG.Core;
using RPG.Contracts;

namespace RPG.Modules
{
    [RequireComponent(typeof(NetworkObject))]
    public partial class HealthModule : BaseNetworkModule, IDamageable, IResourcePool
    {
        private NetworkVariable<float> _currentValue = new NetworkVariable<float>(100f);
        private NetworkVariable<float> _maxValue = new NetworkVariable<float>(100f);

        private NetworkVariable<bool> _isDead = new NetworkVariable<bool>(false);

        public float CurrentValue => _currentValue.Value;
        public float MaxValue => _maxValue.Value;

        public bool IsDead => _isDead.Value;

        public virtual void TakeDamage(float amount, ulong attackerId)
        {
            // Implement in user partial class
        }

        public virtual void ModifyResource(float delta)
        {
            if (!IsServer) return;
            _currentValue.Value = Mathf.Clamp(_currentValue.Value + delta, 0, _maxValue.Value);
        }

        public virtual void SetMaxValue(float newMax)
        {
            if (!IsServer) return;
            _maxValue.Value = Mathf.Max(0, newMax);
        }

    }
}
