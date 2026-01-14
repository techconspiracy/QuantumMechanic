# QUANTUM MECHANIC - SESSION 11 - CHUNK 3/3 KICKOFF
## UIManager.cs - Combat Feedback & Inventory UI (FINAL)

---

## ✅ WHAT'S ALREADY COMPLETE (Chunks 1 & 2)

You have a fully functional UI management system:
- ✅ All data structures, enums, and UIPanel class (Chunk 1)
- ✅ Singleton setup with field organization (Chunk 1)
- ✅ InitializeUI() and SetLocalPlayer() (Chunk 1)
- ✅ Panel management system with animations (Chunk 2)
- ✅ OpenPanel(), ClosePanel(), TogglePanel(), CloseAllPanels() (Chunk 2)
- ✅ AnimatePanel() coroutine with 7 animation types (Chunk 2)
- ✅ HUD updates for health, mana, level, abilities (Chunk 2)

**Line count so far:** ~250 lines

---

## 🎯 CHUNK 3 OBJECTIVE: Combat Feedback & Inventory UI

Implement combat feedback systems and inventory display **without modifying Chunk 1 or 2 code**:

### 1. Combat Feedback Methods (4 methods)
```csharp
/// <summary>
/// Shows a damage number at the specified world position.
/// </summary>
public void ShowDamageNumber(Vector3 worldPosition, float damage, DamageNumberType type)

/// <summary>
/// Shows a hit indicator effect.
/// </summary>
public void ShowHitIndicator(Vector3 worldPosition, Color color)

/// <summary>
/// Updates all active damage numbers (called from Update).
/// </summary>
private void UpdateDamageNumbers()

/// <summary>
/// Cleans up expired damage numbers.
/// </summary>
private void CleanupDamageNumbers()
```

### 2. Inventory UI Methods (4 methods)
```csharp
/// <summary>
/// Refreshes the inventory display with current items.
/// </summary>
public void RefreshInventoryUI(InventorySystem inventory)

/// <summary>
/// Creates inventory slot UI elements.
/// </summary>
private void CreateInventorySlots(int slotCount)

/// <summary>
/// Updates a specific inventory slot display.
/// </summary>
private void UpdateInventorySlot(GameObject slot, ItemData item, int quantity)

/// <summary>
/// Updates the gold display.
/// </summary>
public void UpdateGoldDisplay(int goldAmount)
```

### 3. Character Panel Methods (2 methods)
```csharp
/// <summary>
/// Refreshes the character panel with current stats.
/// </summary>
public void RefreshCharacterPanel(CharacterStats stats)

/// <summary>
/// Updates the buff/debuff display.
/// </summary>
public void UpdateBuffDisplay(List<BuffInstance> buffs)
```

### 4. Unity Lifecycle (2 methods)
```csharp
/// <summary>
/// Update loop for damage number animations.
/// </summary>
private void Update()

/// <summary>
/// Cleanup when destroyed.
/// </summary>
private void OnDestroy()
```

---

## 📋 IMPLEMENTATION REQUIREMENTS

### ShowDamageNumber() Requirements:
Creates floating damage text at world position with color based on type.

**Implementation:**
```csharp
/// <summary>
/// Shows a floating damage number at the specified world position.
/// </summary>
public void ShowDamageNumber(Vector3 worldPosition, float damage, DamageNumberType type)
{
    if (damageNumberPrefab == null || damageNumberContainer == null)
        return;

    GameObject damageObj = Instantiate(damageNumberPrefab, damageNumberContainer);
    activeDamageNumbers.Add(damageObj);

    // Position it at world position
    RectTransform rectTransform = damageObj.GetComponent<RectTransform>();
    if (rectTransform != null)
    {
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
        rectTransform.position = screenPos;
    }

    // Set damage text and color
    TextMeshProUGUI text = damageObj.GetComponent<TextMeshProUGUI>();
    if (text != null)
    {
        text.text = Mathf.RoundToInt(damage).ToString();

        switch (type)
        {
            case DamageNumberType.Normal:
                text.color = Color.white;
                break;
            case DamageNumberType.Critical:
                text.color = new Color(1f, 0.5f, 0f); // Orange
                text.fontSize *= 1.5f;
                break;
            case DamageNumberType.Heal:
                text.color = Color.green;
                text.text = "+" + text.text;
                break;
            case DamageNumberType.Miss:
                text.color = Color.gray;
                text.text = "MISS";
                break;
            case DamageNumberType.Immune:
                text.color = Color.cyan;
                text.text = "IMMUNE";
                break;
        }
    }

    // Store creation time
    damageObj.GetComponent<DamageNumberUI>()?.Initialize(Time.time, damageNumberLifetime);

    if (showDebugInfo)
        Debug.Log($"[UIManager] Showed damage number: {damage} ({type}) at {worldPosition}");
}
```

### ShowHitIndicator() Requirements:
Creates a brief visual indicator at hit position.

**Implementation:**
```csharp
public void ShowHitIndicator(Vector3 worldPosition, Color color)
{
    if (hitIndicatorPrefab == null || hitIndicatorContainer == null)
        return;

    GameObject indicator = Instantiate(hitIndicatorPrefab, hitIndicatorContainer);

    RectTransform rectTransform = indicator.GetComponent<RectTransform>();
    if (rectTransform != null)
    {
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
        rectTransform.position = screenPos;
    }

    Image image = indicator.GetComponent<Image>();
    if (image != null)
    {
        image.color = color;
    }

    // Auto-destroy after brief delay
    Destroy(indicator, 0.5f);
}
```

### UpdateDamageNumbers() Requirements:
Called every frame to animate damage numbers upward.

**Implementation:**
```csharp
private void UpdateDamageNumbers()
{
    for (int i = activeDamageNumbers.Count - 1; i >= 0; i--)
    {
        GameObject dmgNum = activeDamageNumbers[i];
        if (dmgNum == null)
        {
            activeDamageNumbers.RemoveAt(i);
            continue;
        }

        // Float upward
        RectTransform rectTransform = dmgNum.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.position += Vector3.up * damageNumberFloatSpeed * Time.deltaTime;
        }

        // Fade out over lifetime
        CanvasGroup canvasGroup = dmgNum.GetComponent<CanvasGroup>();
        DamageNumberUI damageUI = dmgNum.GetComponent<DamageNumberUI>();

        if (damageUI != null && canvasGroup != null)
        {
            float elapsed = Time.time - damageUI.spawnTime;
            float t = elapsed / damageUI.lifetime;

            canvasGroup.alpha = 1f - t;

            if (t >= 1f)
            {
                Destroy(dmgNum);
                activeDamageNumbers.RemoveAt(i);
            }
        }
    }
}
```

### RefreshInventoryUI() Requirements:
Rebuilds entire inventory display from InventorySystem.

**Implementation:**
```csharp
public void RefreshInventoryUI(InventorySystem inventory)
{
    if (inventory == null || inventoryGridContainer == null)
        return;

    // Create slots if not already created
    if (activeInventorySlots.Count == 0)
    {
        CreateInventorySlots(inventory.MaxSlots);
    }

    // Update each slot with item data
    for (int i = 0; i < activeInventorySlots.Count; i++)
    {
        if (i < inventory.Items.Count)
        {
            InventoryItem item = inventory.Items[i];
            UpdateInventorySlot(activeInventorySlots[i], item.Data, item.Quantity);
        }
        else
        {
            UpdateInventorySlot(activeInventorySlots[i], null, 0);
        }
    }

    if (showDebugInfo)
        Debug.Log($"[UIManager] Refreshed inventory UI with {inventory.Items.Count} items");
}
```

### CreateInventorySlots() Requirements:
Instantiates grid of inventory slot UI elements.

**Implementation:**
```csharp
private void CreateInventorySlots(int slotCount)
{
    if (inventorySlotPrefab == null || inventoryGridContainer == null)
        return;

    // Clear existing
    foreach (GameObject slot in activeInventorySlots)
    {
        if (slot != null)
            Destroy(slot);
    }
    activeInventorySlots.Clear();

    // Create new slots
    for (int i = 0; i < slotCount; i++)
    {
        GameObject slot = Instantiate(inventorySlotPrefab, inventoryGridContainer);
        activeInventorySlots.Add(slot);
    }
}
```

### UpdateInventorySlot() Requirements:
Updates a single slot with item icon and quantity.

**Implementation:**
```csharp
private void UpdateInventorySlot(GameObject slot, ItemData item, int quantity)
{
    if (slot == null)
        return;

    // Assumes slot has Image for icon and TextMeshProUGUI for quantity
    Image icon = slot.transform.Find("Icon")?.GetComponent<Image>();
    TextMeshProUGUI quantityText = slot.transform.Find("Quantity")?.GetComponent<TextMeshProUGUI>();

    if (icon != null)
    {
        if (item != null && item.Icon != null)
        {
            icon.sprite = item.Icon;
            icon.enabled = true;
        }
        else
        {
            icon.enabled = false;
        }
    }

    if (quantityText != null)
    {
        if (item != null && quantity > 1)
        {
            quantityText.text = quantity.ToString();
            quantityText.enabled = true;
        }
        else
        {
            quantityText.enabled = false;
        }
    }
}
```

### RefreshCharacterPanel() Requirements:
Updates character stats display.

**Implementation:**
```csharp
public void RefreshCharacterPanel(CharacterStats stats)
{
    if (stats == null || characterPanel == null)
        return;

    if (characterNameText != null)
    {
        characterNameText.text = stats.CharacterName;
    }

    if (characterLevelText != null)
    {
        characterLevelText.text = $"Level {stats.Level}";
    }

    // Update stat displays
    if (statsContainer != null && statDisplayPrefab != null)
    {
        // Clear existing
        foreach (Transform child in statsContainer)
        {
            Destroy(child.gameObject);
        }

        // Create stat displays
        CreateStatDisplay("Strength", stats.GetStat(StatType.Strength).ToString());
        CreateStatDisplay("Dexterity", stats.GetStat(StatType.Dexterity).ToString());
        CreateStatDisplay("Intelligence", stats.GetStat(StatType.Intelligence).ToString());
        CreateStatDisplay("Vitality", stats.GetStat(StatType.Vitality).ToString());
        CreateStatDisplay("Luck", stats.GetStat(StatType.Luck).ToString());
    }
}

private void CreateStatDisplay(string statName, string statValue)
{
    GameObject statObj = Instantiate(statDisplayPrefab, statsContainer);
    
    TextMeshProUGUI nameText = statObj.transform.Find("StatName")?.GetComponent<TextMeshProUGUI>();
    TextMeshProUGUI valueText = statObj.transform.Find("StatValue")?.GetComponent<TextMeshProUGUI>();

    if (nameText != null)
        nameText.text = statName;
    if (valueText != null)
        valueText.text = statValue;
}
```

### Update() & OnDestroy() Requirements:
**Implementation:**
```csharp
private void Update()
{
    UpdateDamageNumbers();
}

private void OnDestroy()
{
    // Unsubscribe from events
    if (localPlayer != null)
    {
        CharacterStats stats = localPlayer.GetComponent<CharacterStats>();
        if (stats != null)
        {
            stats.OnHealthChanged -= UpdateHealthBar;
            stats.OnManaChanged -= UpdateManaBar;
            stats.OnLevelChanged -= UpdateLevel;
        }
    }
}
```

---

## 🔧 INTEGRATION WITH PREVIOUS CHUNKS

Uses from Chunk 1:
- `activeDamageNumbers` list
- `activeInventorySlots` list
- `damageNumberPrefab`, `hitIndicatorPrefab`
- `inventorySlotPrefab`, `statDisplayPrefab`
- All container references
- `showDebugInfo` flag

Uses from Chunk 2:
- Panel system is already functional
- HUD updates already working

**Note:** You'll need a simple `DamageNumberUI` component:
```csharp
public class DamageNumberUI : MonoBehaviour
{
    public float spawnTime;
    public float lifetime;

    public void Initialize(float spawn, float life)
    {
        spawnTime = spawn;
        lifetime = life;
    }
}
```

---

## 📊 EXPECTED OUTPUT

**Lines:** ~110-120 lines of new code
**Total:** ~360-370 lines complete

**New Regions:**
- `#region Combat Feedback (Public & Private)`
- `#region Inventory UI (Public & Private)`
- `#region Character Panel (Public)`
- `#region Unity Lifecycle (Private)`

**Methods with XML Docs:**
- ShowDamageNumber()
- ShowHitIndicator()
- UpdateDamageNumbers()
- CleanupDamageNumbers()
- RefreshInventoryUI()
- CreateInventorySlots()
- UpdateInventorySlot()
- UpdateGoldDisplay()
- RefreshCharacterPanel()
- UpdateBuffDisplay()
- CreateStatDisplay() - helper
- Update()
- OnDestroy()

**Close the UIManager class with final braces!**

---

## ⚠️ CRITICAL REMINDERS

- **DO NOT modify Chunk 1 or 2 code**
- Include Update() and OnDestroy() lifecycle methods
- Damage numbers float upward and fade out
- Inventory slots show icon and quantity
- Character panel displays all main stats
- Always check null references before using UI elements
- Clean up event subscriptions in OnDestroy()
- Close all class and namespace braces properly

---

## 🚀 READY TO IMPLEMENT CHUNK 3 (FINAL)?

Copy this into chat:

```
I'm completing Quantum Mechanic Session 11 - UIManager.cs (Chunk 3/3 - FINAL).

Chunks 1 & 2 are complete with foundation, panel management, HUD updates, and animations.

Now implement COMBAT FEEDBACK & INVENTORY UI:
- ShowDamageNumber() with color-coded damage types
- ShowHitIndicator() for hit effects
- UpdateDamageNumbers() for floating animation
- RefreshInventoryUI() to display items
- CreateInventorySlots() and UpdateInventorySlot()
- RefreshCharacterPanel() for stats display
- UpdateBuffDisplay() for active buffs
- Update() and OnDestroy() lifecycle methods

This completes the entire UIManager system.

Output: ~110-120 lines with XML docs and #regions.
Namespace: QuantumMechanic.UI

Ready to generate Chunk 3 (FINAL)?
```

---

## 📈 PROGRESS TRACKER

- ✅ **Chunk 1:** Foundation & Core (130 lines) - COMPLETE
- ✅ **Chunk 2:** Panel Management & HUD (120 lines) - COMPLETE
- 🔄 **Chunk 3:** Combat Feedback & Inventory (115 lines) - NEXT (FINAL)

**Estimated Total:** ~365 lines

---

## 🎉 COMPLETION CHECKLIST

After Chunk 3, verify:
- [ ] Damage numbers spawn at world positions
- [ ] Damage numbers float upward and fade
- [ ] Critical hits show larger orange text
- [ ] Heals show green "+X" text
- [ ] Miss/Immune show appropriate text
- [ ] Hit indicators appear briefly
- [ ] Inventory grid displays items
- [ ] Item icons and quantities show correctly
- [ ] Gold display updates
- [ ] Character panel shows all stats
- [ ] Buff display shows active buffs
- [ ] Update() animates damage numbers
- [ ] OnDestroy() unsubscribes events
- [ ] All methods have XML documentation
- [ ] Class and namespace properly closed

**🎊 SESSION 11 WILL BE COMPLETE! 🎊**

---

## 🎮 WHAT'S NEXT?

After completing Session 11, you'll have:
- ✅ Complete UI management system
- ✅ Animated panel system
- ✅ Live HUD with health/mana/abilities
- ✅ Combat feedback with damage numbers
- ✅ Inventory display system
- ✅ Character stats panel

**Next Session Preview:**
**Session 12: QuestSystem.cs** - Quest tracking, objectives, rewards, and quest chains to give players goals and progression!

🚀 Ready to finish the UI system!