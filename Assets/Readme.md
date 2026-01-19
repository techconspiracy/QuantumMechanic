# Atomic Module Generator - Developer Guide
## Unity 6 NGO Modular RPG Framework

---

## 🎯 Overview

The **Atomic Module Generator** is an editor tool that creates production-ready, network-synchronized RPG components using Unity's Netcode for GameObjects (NGO). It enforces architectural best practices while allowing developers to extend functionality without breaking existing code.

### Core Philosophy
- **Separation of Concerns**: Each RPG system (Health, Mana, Inventory) is a standalone `NetworkBehaviour`
- **Partial Class Architecture**: Auto-generated code and user logic live in separate files
- **Contract-Driven Design**: Interfaces define capabilities, modules implement them
- **Data-Driven**: `ScriptableObjects` for configuration, `NetworkVariables` for state

---

## 📂 Project Structure

```
Assets/
├── Scripts/
│   ├── RPG/
│   │   ├── Contracts/
│   │   │   └── INetworkModule.cs          # Interface definitions
│   │   ├── Core/
│   │   │   └── BaseNetworkModule.cs       # Abstract base class
│   │   └── Modules/
│   │       ├── HealthModule.generated.cs  # Auto-generated (DO NOT EDIT)
│   │       └── HealthModule.cs            # User logic (EDIT HERE)
└── Editor/
    └── RPG/
        └── AtomicModuleGenerator.cs        # The generator tool
```

---

## 🚀 Quick Start

### 1. Generate a New Module
1. Open Unity menu: `RPG Tools → Atomic Module Generator`
2. Configure your module:
   - **Module Name**: `HealthModule`
   - **Implement IDamageable**: ✅
   - **Implement IResourcePool**: ✅
   - **Use ScriptableObject Config**: ✅
3. Click **Generate Module**

### 2. Extend the Module
Edit `HealthModule.cs` to add custom logic:

```csharp
public partial class HealthModule
{
    [Header("Custom Configuration")]
    [SerializeField] private float regenRate = 5f;
    
    private void Update()
    {
        if (!IsServer || IsDead) return;
        
        // Regenerate health over time
        ModifyResource(regenRate * Time.deltaTime);
    }
    
    public override void TakeDamage(float amount, ulong attackerId)
    {
        if (!IsServerCallValid()) return;
        
        ModifyResource(-amount);
        
        if (CurrentValue <= 0)
        {
            HandleDeathServerRpc(attackerId);
        }
    }
    
    [ServerRpc]
    private void HandleDeathServerRpc(ulong killerId)
    {
        LogServer($"Entity killed by client {killerId}");
        BroadcastDeathClientRpc(killerId);
    }
    
    [ClientRpc]
    private void BroadcastDeathClientRpc(ulong killerId)
    {
        // Play death animation, sound effects, etc.
    }
}
```

### 3. Attach to GameObject
1. Create a new GameObject in your scene
2. Add `NetworkObject` component
3. Add your `HealthModule` component
4. Configure in the Inspector

---

## 🧩 Available Interfaces

### `INetworkModule` (Always Implemented)
Core lifecycle management for all modules.

**Methods:**
- `OnModuleInitialized()` - Called after network spawn
- `OnModuleShutdown()` - Called before network despawn
- `ValidateOwnership()` - Checks if local client owns this object

### `IDamageable`
For entities that can take damage and die.

**Properties:**
- `bool IsDead` - Current death state

**Methods:**
- `TakeDamage(float amount, ulong attackerId)` - Apply damage from an attacker

### `IResourcePool`
For managing regenerating resources (Health, Mana, Stamina).

**Properties:**
- `float CurrentValue` - Current resource amount
- `float MaxValue` - Maximum resource capacity

**Methods:**
- `ModifyResource(float delta)` - Add or subtract resource
- `SetMaxValue(float newMax)` - Change maximum capacity

### `IConfigurable<T>`
For data-driven modules using ScriptableObjects.

**Properties:**
- `T Configuration` - Reference to config asset

**Methods:**
- `ApplyConfiguration()` - Apply settings from ScriptableObject

---

## 💡 Example Systems to Build Next

### 1. **ManaModule** (Spell Casting Resource)
```
Interfaces: IResourcePool, IConfigurable<ManaConfig>
Features:
- Regenerates over time
- Different regen rates (combat vs out-of-combat)
- ScriptableObject defines base mana and regen multipliers
```

### 2. **InventoryModule** (Item Management)
```
Interfaces: INetworkModule
Features:
- NetworkList<ItemData> for synchronized inventory
- Add/Remove/Use item methods
- Weight/slot capacity system
- Drop items as NetworkObjects in world
```

### 3. **StatsModule** (Character Attributes)
```
Interfaces: IConfigurable<StatsConfig>
Features:
- Strength, Agility, Intelligence, etc.
- Stat modifiers (buffs/debuffs)
- Level-up system with stat point allocation
- Syncs with other modules (high STR = more HP)
```

### 4. **EquipmentModule** (Gear System)
```
Interfaces: INetworkModule
Features:
- Head, Chest, Legs, Weapon, Shield slots
- NetworkVariable<EquipmentSlot[]> for sync
- Stat bonuses from equipped items
- Visual mesh swapping on equip/unequip
```

### 5. **BuffModule** (Status Effects)
```
Interfaces: INetworkModule
Features:
- NetworkList<ActiveBuff> (Duration, Type, Intensity)
- Stacking rules (replace, stack, extend duration)
- Periodic tick effects (DoT, HoT)
- VFX synchronization
```

### 6. **ExperienceModule** (Progression System)
```
Interfaces: INetworkModule
Features:
- NetworkVariable<int> currentXP, currentLevel
- XP curve definitions (ScriptableObject)
- Level-up rewards (stat points, skill unlocks)
- Syncs with QuestModule for XP rewards
```

### 7. **CombatModule** (Attack System)
```
Interfaces: INetworkModule
Features:
- Cooldown management (NetworkVariable<float>)
- Hit detection (raycast or trigger-based)
- Damage calculation (stats + weapon + buffs)
- Critical hits, dodge, block mechanics
```

### 8. **QuestModule** (Mission System)
```
Interfaces: IConfigurable<QuestDatabase>
Features:
- NetworkList<ActiveQuest> for player progress
- Objective tracking (kill X enemies, collect Y items)
- Reward distribution on completion
- Quest chaining and prerequisites
```

### 9. **DialogueModule** (NPC Conversations)
```
Interfaces: INetworkModule
Features:
- ServerRpc to request dialogue from NPC
- ClientRpc to display dialogue UI
- Choice-based branching (affects reputation/quests)
- Localization support
```

### 10. **CraftingModule** (Item Creation)
```
Interfaces: INetworkModule
Features:
- Recipe validation (check materials in InventoryModule)
- Crafting time with progress bar
- Success/failure chances
- Quality tiers (normal, rare, legendary)
```

---

## 🏗️ Architectural Patterns

### Pattern 1: Module Communication
Modules should communicate through **events** or **direct references**, never through `GameObject.Find`.

```csharp
public partial class CombatModule
{
    [SerializeField] private HealthModule _targetHealth;
    
    private void Attack()
    {
        if (_targetHealth != null)
        {
            _targetHealth.TakeDamage(CalculateDamage(), OwnerClientId);
        }
    }
}
```

### Pattern 2: Server Authority
Always validate on the server, trust nothing from clients.

```csharp
[ServerRpc]
private void UseItemServerRpc(int itemId, ServerRpcParams rpcParams = default)
{
    if (!IsServerCallValid()) return;
    
    var senderId = rpcParams.Receive.SenderClientId;
    
    // Validate: Does this client own this item?
    if (!_inventory.HasItem(itemId, senderId))
    {
        LogWarning($"Client {senderId} tried to use item {itemId} they don't own!");
        return;
    }
    
    // Valid - process item use
    _inventory.RemoveItem(itemId);
    ApplyItemEffectClientRpc(itemId);
}
```

### Pattern 3: Prediction with Reconciliation
For responsive gameplay, predict on the client, then reconcile with server state.

```csharp
public void JumpLocal()
{
    if (!ValidateOwnership()) return;
    
    // Immediate client-side prediction
    ApplyJumpForce();
    
    // Request server validation
    JumpServerRpc();
}

[ServerRpc]
private void JumpServerRpc()
{
    if (!IsServerCallValid()) return;
    
    // Server validates and broadcasts to other clients
    if (CanJump())
    {
        ApplyJumpForce();
        JumpClientRpc(); // Tell other clients
    }
}
```

---

## ⚠️ Common Pitfalls

### 1. **Accessing NetworkVariables Before Spawn**
```csharp
❌ WRONG:
private void Awake()
{
    _health.Value = 100; // NetworkVariable not initialized yet!
}

✅ CORRECT:
public override void OnModuleInitialized()
{
    base.OnModuleInitialized();
    if (IsServer) _health.Value = 100;
}
```

### 2. **Modifying State from Clients**
```csharp
❌ WRONG:
private void Update()
{
    _health.Value -= Time.deltaTime; // Clients can't modify NetworkVariables!
}

✅ CORRECT:
private void Update()
{
    if (IsServer) _health.Value -= Time.deltaTime;
}
```

### 3. **Using GameObject.Find**
```csharp
❌ WRONG:
private void Start()
{
    var player = GameObject.Find("Player"); // Fragile and slow!
}

✅ CORRECT:
[SerializeField] private PlayerModule _player; // Assign in Inspector
// OR use dependency injection via a manager
```

### 4. **Forgetting Ownership Checks**
```csharp
❌ WRONG:
private void Update()
{
    if (Input.GetKeyDown(KeyCode.Space))
    {
        Jump(); // All clients will call this!
    }
}

✅ CORRECT:
private void Update()
{
    if (!ValidateOwnership()) return;
    if (Input.GetKeyDown(KeyCode.Space)) Jump();
}
```

---

## 🔧 Extending the Generator

Want to add new templates or interfaces? Modify `AtomicModuleGenerator.cs`:

```csharp
// Add new interface option
private bool _implementIInteractable;

// In OnGUI():
_implementIInteractable = EditorGUILayout.Toggle("IInteractable", _implementIInteractable);

// In GeneratePartialClass():
if (_implementIInteractable) interfaces.Add("IInteractable");

// Add stub methods
if (_implementIInteractable)
{
    sb.AppendLine("        public virtual void Interact(ulong interactorId)");
    sb.AppendLine("        {");
    sb.AppendLine("            // Implement in user partial class");
    sb.AppendLine("        }");
}
```

---

## 📚 Additional Resources

- [Unity Netcode for GameObjects Docs](https://docs-multiplayer.unity3d.com/)
- [C# Partial Classes](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/partial-classes-and-methods)
- [ScriptableObjects Best Practices](https://unity.com/how-to/architect-game-code-scriptable-objects)

---

## 🤝 Contributing

Found a bug? Have an idea for a new interface? Open an issue or submit a pull request!

**Happy Coding! 🎮**
