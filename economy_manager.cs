using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuantumMechanic.Economy
{
    /// <summary>
    /// Item definition for the inventory system.
    /// </summary>
    [Serializable]
    public class Item
    {
        public string itemId;
        public string itemName;
        public string description;
        public int value;
        public int stackSize;
        public ItemType type;
        
        public Item(string id, string name, int val, ItemType itemType)
        {
            itemId = id;
            itemName = name;
            value = val;
            type = itemType;
            stackSize = 1;
            description = "";
        }
    }
    
    /// <summary>
    /// Item type enumeration.
    /// </summary>
    public enum ItemType
    {
        Consumable,
        Equipment,
        Material,
        Quest,
        Currency
    }
    
    /// <summary>
    /// Inventory slot with item and quantity.
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        public Item item;
        public int quantity;
        
        public InventorySlot(Item itm, int qty)
        {
            item = itm;
            quantity = qty;
        }
        
        public bool CanStack(Item otherItem)
        {
            return item != null && item.itemId == otherItem.itemId && quantity < item.stackSize;
        }
    }
    
    /// <summary>
    /// Event-driven economy and inventory management system.
    /// Handles currency transactions, item management, and shop interactions.
    /// Fully decoupled using C# events for integration with UI and networking.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        [Header("Economy Configuration")]
        [SerializeField] private int _startingCurrency = 100;
        [SerializeField] private int _inventorySize = 20;
        
        private static EconomyManager _instance;
        private int _currentCurrency;
        private List<InventorySlot> _inventory;
        private Dictionary<string, Item> _itemDatabase;
        
        public static EconomyManager Instance => _instance;
        public int CurrentCurrency => _currentCurrency;
        
        // Event system for economy actions
        public event Action<int> OnCurrencyChanged;
        public event Action<Item, int> OnItemAdded;
        public event Action<Item, int> OnItemRemoved;
        public event Action<string, int, int> OnTransactionCompleted; // itemId, cost, quantity
        public event Action<string> OnTransactionFailed; // reason
        
        /// <summary>
        /// Singleton initialization.
        /// </summary>
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            InitializeEconomy();
        }
        
        /// <summary>
        /// Initializes economy systems and item database.
        /// </summary>
        private void InitializeEconomy()
        {
            _currentCurrency = _startingCurrency;
            _inventory = new List<InventorySlot>(_inventorySize);
            _itemDatabase = new Dictionary<string, Item>();
            
            // Register default items
            RegisterDefaultItems();
            
            Debug.Log($"[EconomyManager] Initialized with {_currentCurrency} currency");
        }
        
        /// <summary>
        /// Registers the default item catalog.
        /// </summary>
        private void RegisterDefaultItems()
        {
            RegisterItem(new Item("health_potion", "Health Potion", 10, ItemType.Consumable) { stackSize = 99 });
            RegisterItem(new Item("mana_potion", "Mana Potion", 15, ItemType.Consumable) { stackSize = 99 });
            RegisterItem(new Item("iron_sword", "Iron Sword", 50, ItemType.Equipment));
            RegisterItem(new Item("leather_armor", "Leather Armor", 75, ItemType.Equipment));
            RegisterItem(new Item("wood", "Wood", 5, ItemType.Material) { stackSize = 999 });
            RegisterItem(new Item("iron_ore", "Iron Ore", 20, ItemType.Material) { stackSize = 999 });
        }
        
        /// <summary>
        /// Registers an item in the database.
        /// </summary>
        public void RegisterItem(Item item)
        {
            if (!_itemDatabase.ContainsKey(item.itemId))
            {
                _itemDatabase[item.itemId] = item;
                Debug.Log($"[EconomyManager] Registered item: {item.itemName}");
            }
        }
        
        /// <summary>
        /// Retrieves an item from the database.
        /// </summary>
        public Item GetItem(string itemId)
        {
            return _itemDatabase.TryGetValue(itemId, out Item item) ? item : null;
        }
        
        /// <summary>
        /// Adds currency to the player's wallet.
        /// </summary>
        public void AddCurrency(int amount)
        {
            if (amount < 0)
            {
                Debug.LogWarning("[EconomyManager] Cannot add negative currency");
                return;
            }
            
            _currentCurrency += amount;
            OnCurrencyChanged?.Invoke(_currentCurrency);
            Debug.Log($"[EconomyManager] Added {amount} currency. Total: {_currentCurrency}");
        }
        
        /// <summary>
        /// Attempts to spend currency. Returns true if successful.
        /// </summary>
        public bool SpendCurrency(int amount)
        {
            if (amount < 0)
            {
                Debug.LogWarning("[EconomyManager] Cannot spend negative currency");
                return false;
            }
            
            if (_currentCurrency < amount)
            {
                OnTransactionFailed?.Invoke($"Insufficient funds. Need {amount}, have {_currentCurrency}");
                return false;
            }
            
            _currentCurrency -= amount;
            OnCurrencyChanged?.Invoke(_currentCurrency);
            Debug.Log($"[EconomyManager] Spent {amount} currency. Remaining: {_currentCurrency}");
            return true;
        }
        
        /// <summary>
        /// Adds an item to the inventory with stacking logic.
        /// </summary>
        public bool AddItem(string itemId, int quantity = 1)
        {
            Item item = GetItem(itemId);
            if (item == null)
            {
                Debug.LogError($"[EconomyManager] Item not found: {itemId}");
                return false;
            }
            
            // Try to stack with existing items
            foreach (var slot in _inventory)
            {
                if (slot.CanStack(item))
                {
                    int spaceLeft = item.stackSize - slot.quantity;
                    int amountToAdd = Mathf.Min(spaceLeft, quantity);
                    slot.quantity += amountToAdd;
                    quantity -= amountToAdd;
                    
                    OnItemAdded?.Invoke(item, amountToAdd);
                    
                    if (quantity == 0)
                    {
                        Debug.Log($"[EconomyManager] Stacked {item.itemName}");
                        return true;
                    }
                }
            }
            
            // Create new slots for remaining quantity
            while (quantity > 0)
            {
                if (_inventory.Count >= _inventorySize)
                {
                    OnTransactionFailed?.Invoke("Inventory full");
                    return false;
                }
                
                int amountForSlot = Mathf.Min(quantity, item.stackSize);
                _inventory.Add(new InventorySlot(item, amountForSlot));
                quantity -= amountForSlot;
                OnItemAdded?.Invoke(item, amountForSlot);
            }
            
            Debug.Log($"[EconomyManager] Added {item.itemName} to inventory");
            return true;
        }
        
        /// <summary>
        /// Removes an item from inventory.
        /// </summary>
        public bool RemoveItem(string itemId, int quantity = 1)
        {
            int remaining = quantity;
            
            for (int i = _inventory.Count - 1; i >= 0; i--)
            {
                if (_inventory[i].item.itemId == itemId)
                {
                    if (_inventory[i].quantity >= remaining)
                    {
                        _inventory[i].quantity -= remaining;
                        OnItemRemoved?.Invoke(_inventory[i].item, remaining);
                        
                        if (_inventory[i].quantity == 0)
                        {
                            _inventory.RemoveAt(i);
                        }
                        
                        Debug.Log($"[EconomyManager] Removed {quantity}x {itemId}");
                        return true;
                    }
                    else
                    {
                        remaining -= _inventory[i].quantity;
                        OnItemRemoved?.Invoke(_inventory[i].item, _inventory[i].quantity);
                        _inventory.RemoveAt(i);
                    }
                }
            }
            
            OnTransactionFailed?.Invoke($"Not enough {itemId}");
            return false;
        }
        
        /// <summary>
        /// Checks if player has enough of an item.
        /// </summary>
        public bool HasItem(string itemId, int quantity = 1)
        {
            int total = 0;
            foreach (var slot in _inventory)
            {
                if (slot.item.itemId == itemId)
                {
                    total += slot.quantity;
                }
            }
            return total >= quantity;
        }
        
        /// <summary>
        /// Purchases an item from the shop.
        /// </summary>
        public bool PurchaseItem(string itemId, int quantity = 1)
        {
            Item item = GetItem(itemId);
            if (item == null)
            {
                OnTransactionFailed?.Invoke("Item not found");
                return false;
            }
            
            int totalCost = item.value * quantity;
            
            if (!SpendCurrency(totalCost))
            {
                return false;
            }
            
            if (AddItem(itemId, quantity))
            {
                OnTransactionCompleted?.Invoke(itemId, totalCost, quantity);
                Debug.Log($"[EconomyManager] Purchased {quantity}x {item.itemName} for {totalCost}");
                return true;
            }
            else
            {
                // Refund if inventory full
                AddCurrency(totalCost);
                return false;
            }
        }
        
        /// <summary>
        /// Sells an item to the shop.
        /// </summary>
        public bool SellItem(string itemId, int quantity = 1)
        {
            Item item = GetItem(itemId);
            if (item == null)
            {
                OnTransactionFailed?.Invoke("Item not found");
                return false;
            }
            
            if (!HasItem(itemId, quantity))
            {
                OnTransactionFailed?.Invoke("Not enough items to sell");
                return false;
            }
            
            int sellValue = (item.value / 2) * quantity; // Sell for 50% of buy price
            
            if (RemoveItem(itemId, quantity))
            {
                AddCurrency(sellValue);
                OnTransactionCompleted?.Invoke(itemId, -sellValue, quantity);
                Debug.Log($"[EconomyManager] Sold {quantity}x {item.itemName} for {sellValue}");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets a copy of the current inventory.
        /// </summary>
        public List<InventorySlot> GetInventory()
        {
            return new List<InventorySlot>(_inventory);
        }
        
        /// <summary>
        /// Clears the entire inventory.
        /// </summary>
        public void ClearInventory()
        {
            _inventory.Clear();
            Debug.Log("[EconomyManager] Inventory cleared");
        }
        
        /// <summary>
        /// Resets economy to starting state.
        /// </summary>
        public void ResetEconomy()
        {
            _currentCurrency = _startingCurrency;
            ClearInventory();
            OnCurrencyChanged?.Invoke(_currentCurrency);
            Debug.Log("[EconomyManager] Economy reset");
        }
        
        /// <summary>
        /// Sets currency directly (for save system integration).
        /// </summary>
        public void SetCurrency(int amount)
        {
            _currentCurrency = amount;
            OnCurrencyChanged?.Invoke(_currentCurrency);
        }
    }
}