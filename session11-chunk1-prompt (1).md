# QUANTUM MECHANIC - SESSION 11 - CHUNK 1/3 KICKOFF
## UIManager.cs - Foundation & Core UI Systems

---

## 🎯 SESSION 11 OVERVIEW

**Goal:** Create a comprehensive UI management system that handles all game interfaces including HUD, inventory, menus, and combat feedback.

**Total Estimated Lines:** ~350 lines across 3 chunks
**Namespace:** `QuantumMechanic.UI`

**Key Features:**
- Centralized UI panel management
- HUD with health/mana/ability displays
- Inventory UI with grid layout
- Character stats panel
- Combat feedback (damage numbers, indicators)
- Menu systems (pause, settings)
- UI animations and transitions

---

## 📦 CHUNK 1 OBJECTIVE: Foundation & Core UI Systems

Implement the foundational structure and basic UI management:

### 1. Data Structures & Enums (4 items)
```csharp
/// <summary>
/// Types of UI panels in the game.
/// </summary>
public enum UIPanelType
{
    None,
    HUD,
    Inventory,
    CharacterPanel,
    PauseMenu,
    SettingsMenu,
    QuestLog,
    LootWindow,
    DialogueBox,
    DeathScreen
}

/// <summary>
/// UI animation types for panel transitions.
/// </summary>
public enum UIAnimationType
{
    None,
    Fade,
    Scale,
    SlideFromTop,
    SlideFromBottom,
    SlideFromLeft,
    SlideFromRight
}

/// <summary>
/// Damage number type for combat feedback.
/// </summary>
public enum DamageNumberType
{
    Normal,
    Critical,
    Heal,
    Miss,
    Immune
}

/// <summary>
/// Represents a UI panel with its GameObject and metadata.
/// </summary>
[System.Serializable]
public class UIPanel
{
    public UIPanelType PanelType;
    public GameObject PanelObject;
    public UIAnimationType OpenAnimation;
    public UIAnimationType CloseAnimation;
    public float AnimationDuration = 0.3f;
    public bool IsOpen { get; set; }
    public CanvasGroup CanvasGroup { get; set; }
}
```

### 2. Singleton Setup & Fields (1 section)
```csharp
public class UIManager : MonoBehaviour
{
    #region Singleton
    
    public static UIManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        InitializeUI();
    }
    
    #endregion
    
    #region Serialized Fields
    
    [Header("UI Panels")]
    [SerializeField] private List<UIPanel> uiPanels = new List<UIPanel>();
    
    [Header("HUD References")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private Slider manaBar;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI manaText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Transform abilityBarContainer;
    [SerializeField] private GameObject abilitySlotPrefab;
    
    [Header("Combat Feedback")]
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Transform damageNumberContainer;
    [SerializeField] private GameObject hitIndicatorPrefab;
    [SerializeField] private Transform hitIndicatorContainer;
    
    [Header("Inventory UI")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private Transform inventoryGridContainer;
    [SerializeField] private GameObject inventorySlotPrefab;
    [SerializeField] private TextMeshProUGUI inventoryGoldText;
    
    [Header("Character Panel")]
    [SerializeField] private GameObject characterPanel;
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private TextMeshProUGUI characterLevelText;
    [SerializeField] private Transform statsContainer;
    [SerializeField] private GameObject statDisplayPrefab;
    
    [Header("Settings")]
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField] private float damageNumberLifetime = 2f;
    [SerializeField] private float damageNumberFloatSpeed = 50f;
    
    #endregion
    
    #region Private Fields
    
    private Dictionary<UIPanelType, UIPanel> panelDictionary = new Dictionary<UIPanelType, UIPanel>();
    private List<GameObject> activeAbilitySlots = new List<GameObject>();
    private List<GameObject> activeInventorySlots = new List<GameObject>();
    private List<GameObject> activeDamageNumbers = new List<GameObject>();
    private PlayerNetworking localPlayer;
    
    #endregion
}
```

### 3. Initialization Methods (2 methods)
```csharp
/// <summary>
/// Initializes all UI systems and caches panel references.
/// </summary>
private void InitializeUI()
{
    // Build panel dictionary
    foreach (UIPanel panel in uiPanels)
    {
        if (panel.PanelObject != null)
        {
            panelDictionary[panel.PanelType] = panel;
            
            // Cache or add CanvasGroup for animations
            panel.CanvasGroup = panel.PanelObject.GetComponent<CanvasGroup>();
            if (panel.CanvasGroup == null)
            {
                panel.CanvasGroup = panel.PanelObject.AddComponent<CanvasGroup>();
            }
            
            // Close all panels by default
            panel.PanelObject.SetActive(false);
            panel.IsOpen = false;
        }
    }
    
    // Open HUD by default
    if (panelDictionary.ContainsKey(UIPanelType.HUD))
    {
        OpenPanel(UIPanelType.HUD, false); // Open instantly without animation
    }
    
    if (showDebugInfo)
        Debug.Log($"[UIManager] Initialized with {panelDictionary.Count} panels");
}

/// <summary>
/// Sets the local player reference for UI updates.
/// </summary>
public void SetLocalPlayer(PlayerNetworking player)
{
    localPlayer = player;
    
    if (localPlayer != null)
    {
        // Subscribe to player events
        CharacterStats stats = localPlayer.GetComponent<CharacterStats>();
        if (stats != null)
        {
            stats.OnHealthChanged += UpdateHealthBar;
            stats.OnManaChanged += UpdateManaBar;
            stats.OnLevelChanged += UpdateLevel;
        }
        
        // Initialize HUD with current values
        UpdateHealthBar(stats.CurrentHealth, stats.MaxHealth);
        UpdateManaBar(stats.CurrentMana, stats.MaxMana);
        UpdateLevel(stats.Level);
        
        if (showDebugInfo)
            Debug.Log($"[UIManager] Local player set: {player.name}");
    }
}
```

---

## 📋 IMPLEMENTATION REQUIREMENTS

### Data Structures:
- 4 enums: UIPanelType (10 values), UIAnimationType (7 values), DamageNumberType (5 values)
- 1 class: UIPanel with metadata
- All properly serializable with [System.Serializable] where needed

### Singleton Pattern:
- Standard Unity singleton with DontDestroyOnLoad
- Proper null checks and destruction of duplicates
- Call InitializeUI() in Awake()

### Fields Organization:
- Use #region blocks: "Singleton", "Serialized Fields", "Private Fields"
- Group serialized fields by category with [Header] attributes
- Include all necessary prefab references and containers
- Use descriptive names following C# conventions

### Initialization:
- Build dictionary from uiPanels list
- Add CanvasGroup components if missing
- Close all panels by default except HUD
- Subscribe to player stat events when player is set

### Required Using Statements:
```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using QuantumMechanic.Core;
using QuantumMechanic.Combat;
```

---

## 🎨 CODE STYLE REQUIREMENTS

1. **XML Documentation:** Every public method, enum, and class
2. **Regions:** Organize code into logical sections
3. **Null Checks:** Always validate references before use
4. **Debug Logging:** Use showDebugInfo flag for optional logs
5. **Naming:** 
   - Private fields: camelCase with underscore prefix for serialized
   - Public properties: PascalCase
   - Methods: PascalCase with clear verb names

---

## 📊 EXPECTED OUTPUT

**Lines:** ~120-140 lines
**Sections:**
- Using statements (8 lines)
- Namespace declaration (1 line)
- 4 enums with XML docs (~40 lines)
- 1 UIPanel class (~10 lines)
- Singleton + fields (~50 lines)
- 2 initialization methods (~30 lines)
- Closing braces (2 lines)

**Structure:**
```
using statements
namespace QuantumMechanic.UI
{
    enums (UIPanelType, UIAnimationType, DamageNumberType)
    UIPanel class
    
    public class UIManager : MonoBehaviour
    {
        #region Singleton
        #region Serialized Fields
        #region Private Fields
        
        InitializeUI()
        SetLocalPlayer()
    }
}
```

---

## ⚠️ CRITICAL NOTES

- **DO NOT implement panel opening/closing yet** - that's Chunk 2
- **DO NOT implement HUD updates yet** - that's Chunk 2
- **DO NOT implement combat feedback yet** - that's Chunk 3
- This chunk is FOUNDATION ONLY - structures, fields, and basic init
- Keep the class structure open for adding more regions in Chunks 2 & 3

---

## 🚀 READY TO IMPLEMENT CHUNK 1?

Copy this into chat:

```
I'm starting Quantum Mechanic Session 11 - UIManager.cs (Chunk 1/3).

Implement the FOUNDATION & CORE UI SYSTEMS:
- 4 enums: UIPanelType, UIAnimationType, DamageNumberType, and UIPanel class
- Singleton pattern with DontDestroyOnLoad
- All serialized fields organized by category (Panels, HUD, Combat, Inventory, Character, Settings)
- Private fields for dictionaries and references
- InitializeUI() method to cache panels and setup dictionary
- SetLocalPlayer() method to subscribe to stat events

Output: ~120-140 lines with XML docs and #regions.
Namespace: QuantumMechanic.UI

Ready to generate Chunk 1?
```

---

## 📈 PROGRESS TRACKING

- 🔄 **Chunk 1:** Foundation & Core (120-140 lines) - NEXT
- ⏳ **Chunk 2:** Panel Management & HUD Updates (~120 lines) - PENDING
- ⏳ **Chunk 3:** Combat Feedback & Inventory UI (~110 lines) - PENDING

**Session 11 Total:** ~350 lines

---

## ✅ COMPLETION CHECKLIST

After Chunk 1, verify:
- [ ] All 4 enums defined with XML documentation
- [ ] UIPanel class is serializable
- [ ] Singleton pattern implemented correctly
- [ ] All necessary serialized fields present with [Header] attributes
- [ ] Dictionary and list fields declared
- [ ] InitializeUI() builds panel dictionary
- [ ] InitializeUI() adds CanvasGroup components
- [ ] SetLocalPlayer() subscribes to stat events
- [ ] Code compiles without errors
- [ ] Proper using statements included

**🎯 Ready to build the UI foundation!**