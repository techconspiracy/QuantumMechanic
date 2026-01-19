// USER EDITABLE FILE - Economy System Implementation
using Unity.Netcode;
using UnityEngine;
using System;
using RPG.Core;

namespace RPG.Modules
{
    // Porting relevant data structures from the original EconomySystem
    [Serializable]
    public enum ItemType { Consumable, Equipment, Material, Quest, Currency }

    [Serializable]
    public class Item
    {
        public string itemId;
        public string itemName;
        public int value;
        public ItemType type;
    }

    /// <summary>
    /// Manages player currency and transactions.
    /// Uses IResourcePool: CurrentValue = Gold, MaxValue = Wallet Cap.
    /// </summary>
    public partial class EconomyModule
    {
        [Header("Starting Config")]
        [SerializeField] private float _startingCurrency = 100f;
        [SerializeField] private float _walletCapacity = 999999f;

        public event Action<float> OnCurrencyChanged;
        public event Action<string, int, int> OnTransactionCompleted;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                // Initialize the generated NetworkVariables
                _currentValue.Value = _startingCurrency;
                _maxValue.Value = _walletCapacity;
            }

            // Hook into the NetworkVariable's change event for local UI/SFX
            _currentValue.OnValueChanged += (oldVal, newVal) => 
            {
                OnCurrencyChanged?.Invoke(newVal);
            };
        }

        public override void OnNetworkDespawn()
        {
            _currentValue.OnValueChanged -= (oldVal, newVal) => OnCurrencyChanged?.Invoke(newVal);
            base.OnNetworkDespawn();
        }

        #region Economy Logic (Ported from EconomySystem.cs)

        /// <summary>
        /// Logic-friendly check for affordability.
        /// </summary>
        public bool CanAfford(float amount) => CurrentValue >= amount;

        /// <summary>
        /// Server-authoritative method to add currency.
        /// </summary>
        public void AddCurrency(float amount)
        {
            if (!IsServer) return;
            // Access the generated NetworkVariable directly
            _currentValue.Value = Mathf.Clamp(_currentValue.Value + amount, 0, MaxValue);
        }

        /// <summary>
        /// Server-authoritative method to spend currency.
        /// </summary>
        public bool TrySpendCurrency(float amount)
        {
            if (!IsServer) return false;

            if (CanAfford(amount))
            {
                _currentValue.Value -= amount;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Refactored Buy logic. 
        /// Note: This expects an InventoryModule to exist on the same object.
        /// </summary>
        public bool BuyItem(Item item, int quantity)
        {
            if (!IsServer) return false;

            float totalCost = item.value * quantity;
            if (TrySpendCurrency(totalCost))
            {
                // Logic for adding to inventory would go here:
                // GetComponent<InventoryModule>().AddItem(item, quantity);
                
                OnTransactionCompleted?.Invoke(item.itemId, (int)totalCost, quantity);
                LogServer($"Purchased {quantity}x {item.itemName} for {totalCost}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Refactored Sell logic.
        /// </summary>
        public void SellItem(Item item, int quantity)
        {
            if (!IsServer) return;

            float sellValue = (item.value / 2f) * quantity;
            AddCurrency(sellValue);
            
            OnTransactionCompleted?.Invoke(item.itemId, (int)-sellValue, quantity);
            LogServer($"Sold {quantity}x {item.itemName} for {sellValue}");
        }

        #endregion
    }
}