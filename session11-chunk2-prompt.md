# QUANTUM MECHANIC - SESSION 11 - CHUNK 2/3 KICKOFF
## UIManager.cs - Panel Management & HUD Updates

---

## ✅ WHAT'S ALREADY COMPLETE (Chunk 1)

You have a fully functional foundation:
- ✅ 4 enums (UIPanelType, UIAnimationType, DamageNumberType) + UIPanel class
- ✅ Singleton pattern with DontDestroyOnLoad
- ✅ All serialized fields organized by category
- ✅ Private fields (dictionaries, lists, references)
- ✅ InitializeUI() - builds panel dictionary and caches CanvasGroups
- ✅ SetLocalPlayer() - subscribes to stat events

**Line count so far:** ~120-140 lines

---

## 🎯 CHUNK 2 OBJECTIVE: Panel Management & HUD Updates

Implement panel control and live HUD updates **without modifying Chunk 1 code**:

### 1. Panel Management Methods (4 methods)
```csharp
/// <summary>
/// Opens a UI panel with optional animation.
/// </summary>
public void OpenPanel(UIPanelType panelType, bool animate = true)

/// <summary>
/// Closes a UI panel with optional animation.
/// </summary>
public void ClosePanel(UIPanelType panelType, bool animated = true)

/// <summary>
/// Toggles a panel open or closed.
/// </summary>
public void TogglePanel(UIPanelType panelType)

/// <summary>
/// Closes all open panels except HUD.
/// </summary>
public void CloseAllPanels(bool closeHUD = false)
```

### 2. Panel Animation Methods (2 methods)
```csharp
/// <summary>
/// Plays the panel animation (open or close).
/// </summary>
private IEnumerator AnimatePanel(UIPanel panel, bool opening)

/// <summary>
/// Gets the starting values for an animation type.
/// </summary>
private void GetAnimationStartValues(UIAnimationType animType, out Vector3 scale, out Vector3 position, out float alpha)
```

### 3. HUD Update Methods (5 methods)
```csharp
/// <summary>
/// Updates the health bar display.
/// </summary>
private void UpdateHealthBar(float current, float max)

/// <summary>
/// Updates the mana bar display.
/// </summary>
private void UpdateManaBar(float current, float max)

/// <summary>
/// Updates the player level display.
/// </summary>
private void UpdateLevel(int level)

/// <summary>
/// Updates ability cooldown displays.
/// </summary>
public void UpdateAbilityBar(List<AbilityData> abilities)

/// <summary>
/// Refreshes a specific ability slot cooldown display.
/// </summary>
private void UpdateAbilitySlot(GameObject slot, AbilityData ability, float cooldown)
```

---

## 📋 IMPLEMENTATION REQUIREMENTS

### UpdateHealthBar() Requirements:
```csharp
/// <summary>
/// Updates the health bar UI with current values.
/// </summary>
private void UpdateHealthBar(float currentHealth, float maxHealth)
{
    if (healthBar != null)
    {
        healthBar.value = currentHealth / maxHealth;
    }
    
    if (healthText != null)
    {
        healthText.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
    }
}
```

### UpdateManaBar() Requirements:
```csharp
private void UpdateManaBar(float currentMana, float maxMana)
{
    if (manaBar != null)
    {
        manaBar.value = maxMana > 0 ? currentMana / maxMana : 0f;
    }
    
    if (manaText != null)
    {
        manaText.text = $"{Mathf.RoundToInt(currentMana)} / {Mathf.RoundToInt(maxMana)}";
    }
}
```

### UpdateLevel() Requirements:
Updates the level display text.

**Implementation:**
```csharp
private void UpdateLevel(int newLevel)
{
    if (levelText != null)
    {
        levelText.text = $"Level {newLevel}";
    }
}
```

### UpdateAbilityBar() Requirements:
Creates/updates ability slot UI elements from player's ability list.

**Implementation:**
```csharp
public void UpdateAbilityBar(List<AbilityData> abilities)
{
    // Clear existing slots
    foreach (GameObject slot in activeAbilitySlots)
    {
        Destroy(slot);
    }
    activeAbilitySlots.Clear();
    
    // Create new slots for each ability
    for (int i = 0; i < abilities.Count && i < maxAbilitySlots; i++)
    {
        GameObject slotObj = Instantiate(abilitySlotPrefab, abilityBarContainer);
        activeAbilitySlots.Add(slotObject);
        
        // Setup ability slot UI (icon, cooldown, etc.)
        // This will be expanded in later chunks
    }
}
```

### OpenPanel() & ClosePanel() Requirements:
Core panel management with animations.

**OpenPanel Implementation:**
```csharp
public void OpenPanel(UIPanelType panelType, bool animate = true)
{
    if (!panelDictionary.ContainsKey(panelType))
    {
        Debug.LogWarning($"[UIManager] Panel {panelType} not found in dictionary");
        return;
    }
    
    UIPanel panel = panelDictionary[panelType];
    
    if (panel.IsOpen)
        return; // Already open
    
    panel.PanelObject.SetActive(true);
    panel.IsOpen = true;
    
    if (animate && panel.OpenAnimation != UIAnimationType.None)
    {
        StartCoroutine(AnimatePanel(panel, true));
    }
    else
    {
        // Instant open
        panel.CanvasGroup.alpha = 1f;
        panel.CanvasGroup.interactable = true;
        panel.CanvasGroup.blocksRaycasts = true;
    }
}

/// <summary>
/// Closes a UI panel with optional animation.
/// </summary>
public void ClosePanel(UIPanelType panelType, bool animated = true)
{
    if (!panelDictionary.ContainsKey(panelType))
    {
        Debug.LogWarning($"[UIManager] Panel {panelType} not found in dictionary!");
        return;
    }
    
    UIPanel panel = panelDictionary[panelType];
    
    if (!panel.IsOpen)
        return; // Already closed
    
    if (animated && panel.CloseAnimation != UIAnimationType.None)
    {
        StartCoroutine(AnimatePanel(panel, false));
    }
    else
    {
        panel.PanelObject.SetActive(false);
        panel.IsOpen = false;
    }
    
    if (showDebugInfo)
        Debug.Log($"[UIManager] Closed panel: {panelType}");
}

/// <summary>
/// Toggles a UI panel open or closed.
/// </summary>
public void TogglePanel(UIPanelType panelType, bool animate = true)
{
    if (panelDictionary.TryGetValue(panelType, out UIPanel panel))
    {
        if (panel.IsOpen)
        {
            ClosePanel(panelType, animate);
        }
        else
        {
            OpenPanel(panelType, animate);
        }
    }
}

/// <summary>
/// Closes all open panels except HUD.
/// </summary>
public void CloseAllPanels()
{
    foreach (var kvp in panelDictionary)
    {
        if (kvp.Key != UIPanelType.HUD && kvp.Value.IsOpen)
        {
            ClosePanel(kvp.Key);
        }
    }
}

/// <summary>
/// Checks if a specific panel is currently open.
/// </summary>
public bool IsPanelOpen(UIPanelType panelType)
{
    if (panelDictionary.TryGetValue(panelType, out UIPanel panel))
    {
        return panel.IsOpen;
    }
    return false;
}

/// <summary>
/// Animates a panel opening or closing.
/// </summary>
private IEnumerator AnimatePanel(UIPanel panel, bool opening)
{
    if (panel.CanvasGroup == null)
        yield break;

    UIAnimationType animType = opening ? panel.OpenAnimation : panel.CloseAnimation;
    float duration = panel.AnimationDuration;
    float elapsed = 0f;

    Vector3 originalScale = panel.PanelObject.transform.localScale;
    Vector3 originalPosition = panel.PanelObject.transform.localPosition;

    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        switch (animType)
        {
            case UIAnimationType.Fade:
                panel.CanvasGroup.alpha = opening ? smoothT : 1f - smoothT;
                break;

            case UIAnimationType.Scale:
                float scale = opening ? smoothT : 1f - smoothT;
                panel.PanelObject.transform.localScale = originalScale * scale;
                panel.CanvasGroup.alpha = scale;
                break;

            case UIAnimationType.SlideFromTop:
                float yOffset = opening ? Screen.height * (1f - smoothT) : Screen.height * smoothT;
                panel.PanelObject.transform.localPosition = originalPosition + new Vector3(0f, yOffset, 0f);
                panel.CanvasGroup.alpha = opening ? smoothT : 1f - smoothT;
                break;

            case UIAnimationType.SlideFromBottom:
                yOffset = opening ? -Screen.height * (1f - smoothT) : -Screen.height * smoothT;
                panel.PanelObject.transform.localPosition = originalPosition + new Vector3(0f, yOffset, 0f);
                panel.CanvasGroup.alpha = opening ? smoothT : 1f - smoothT;
                break;

            case UIAnimationType.SlideFromLeft:
                float xOffset = opening ? -Screen.width * (1f - smoothT) : -Screen.width * smoothT;
                panel.PanelObject.transform.localPosition = originalPosition + new Vector3(xOffset, 0f, 0f);
                panel.CanvasGroup.alpha = opening ? smoothT : 1f - smoothT;
                break;

            case UIAnimationType.SlideFromRight:
                xOffset = opening ? Screen.width * (1f - smoothT) : Screen.width * smoothT;
                panel.PanelObject.transform.localPosition = originalPosition + new Vector3(xOffset, 0f, 0f);
                panel.CanvasGroup.alpha = opening ? smoothT : 1f - smoothT;
                break;
        }

        yield return null;
    }

    // Ensure final state
    if (opening)
    {
        panel.CanvasGroup.alpha = 1f;
        panel.PanelObject.transform.localScale = originalScale;
        panel.PanelObject.transform.localPosition = originalPosition;
    }
    else
    {
        panel.PanelObject.SetActive(false);
    }
}

#endregion

#region HUD Updates (Private)

/// <summary>
/// Updates the health bar UI.
/// </summary>
private void UpdateHealthBar(float current, float max)
{
    if (healthBar != null)
    {
        healthBar.maxValue = max;
        healthBar.value = current;
    }

    if (healthText != null)
    {
        healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }
}

/// <summary>
/// Updates the mana bar UI.
/// </summary>
private void UpdateManaBar(float current, float max)
{
    if (manaBar != null)
    {
        manaBar.maxValue = max;
        manaBar.value = current;
    }

    if (manaText != null)
    {
        manaText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }
}

/// <summary>
/// Updates the level display.
/// </summary>
private void UpdateLevel(int level)
{
    if (levelText != null)
    {
        levelText.text = $"Level {level}";
    }

    if (characterLevelText != null)
    {
        characterLevelText.text = $"Level {level}";
    }
}

/// <summary>
/// Updates the ability bar with player's abilities.
/// </summary>
public void UpdateAbilityBar(List<AbilityData> abilities)
{
    // Clear existing slots
    foreach (GameObject slot in activeAbilitySlots)
    {
        if (slot != null)
            Destroy(slot);
    }
    activeAbilitySlots.Clear();

    if (abilityBarContainer == null || abilitySlotPrefab == null)
        return;

    // Create new slots
    for (int i = 0; i < abilities.Count; i++)
    {
        GameObject slotObj = Instantiate(abilitySlotPrefab, abilityBarContainer);
        activeAbilitySlots.Add(slotObj);

        // Set ability data (assumes slot has AbilitySlotUI component)
        // This would be implemented in a separate AbilitySlotUI component
        // For now, just create the visual slot
    }

    if (showDebugInfo)
        Debug.Log($"[UIManager] Updated ability bar with {abilities.Count} abilities");
}

/// <summary>
/// Updates a specific ability slot's cooldown display.
/// </summary>
public void UpdateAbilityCooldown(int slotIndex, float currentCooldown, float maxCooldown)
{
    if (slotIndex < 0 || slotIndex >= activeAbilitySlots.Count)
        return;

    GameObject slot = activeAbilitySlots[slotIndex];
    if (slot == null)
        return;

    // Update cooldown display (assumes slot has Image component for fill)
    Image cooldownFill = slot.GetComponentInChildren<Image>();
    if (cooldownFill != null)
    {
        cooldownFill.fillAmount = maxCooldown > 0f ? currentCooldown / maxCooldown : 0f;
    }
}

#endregion
```

---

## 📋 IMPLEMENTATION REQUIREMENTS

### Panel Management Methods:
1. **OpenPanel()** - Opens a panel with optional animation
   - Set active, mark as open, start animation coroutine
   - Use UIAnimationType from panel data
   - Handle instant opening (animate = false)

2. **ClosePanel()** - Closes a panel with optional animation
   - Mark as closed, start animation coroutine
   - Deactivate after animation completes
   - Handle instant closing

3. **TogglePanel()** - Toggle panel open/closed state
   - Check current state and call appropriate method

4. **CloseAllPanels()** - Close all except HUD
   - Loop through dictionary, skip HUD

5. **IsPanelOpen()** - Query method for panel state

6. **AnimatePanel()** - Coroutine handling all animation types
   - Support 7 animation types from enum
   - Use SmoothStep for easing
   - Manipulate CanvasGroup alpha, transform scale/position
   - Ensure final state is correct

### HUD Update Methods:
1. **UpdateHealthBar()** - Update health slider and text
2. **UpdateManaBar()** - Update mana slider and text
3. **UpdateLevel()** - Update level text in HUD and character panel
4. **UpdateAbilityBar()** - Rebuild ability slots from list
5. **UpdateAbilityCooldown()** - Update specific slot cooldown fill

### Animation Details:
- **Fade:** Alpha 0→1 or 1→0
- **Scale:** Scale 0→1 or 1→0 with alpha
- **Slide animations:** Move from screen edges with alpha fade
- All use SmoothStep for smooth easing
- Duration from panel.AnimationDuration

---

## 🎨 INTEGRATION WITH CHUNK 1

These methods use:
- `panelDictionary` from Chunk 1
- `UIPanel` class with CanvasGroup
- `activeAbilitySlots` list
- Serialized field references (healthBar, manaBar, etc.)
- `showDebugInfo` flag

**No modifications needed to Chunk 1 code!**

---

## 📊 EXPECTED OUTPUT

**Lines:** ~115-125 lines
**Regions:**
- `#region Panel Management (Public & Private)` - 6 methods
- `#region HUD Updates (Private)` - 5 methods

**Methods with XML Docs:**
- OpenPanel()
- ClosePanel()
- TogglePanel()
- CloseAllPanels()
- IsPanelOpen()
- AnimatePanel() - Coroutine
- UpdateHealthBar()
- UpdateManaBar()
- UpdateLevel()
- UpdateAbilityBar()
- UpdateAbilityCooldown()

---

## ⚠️ CRITICAL NOTES

- **DO NOT close the UIManager class** - Chunk 3 will add more methods
- Use `yield return null` in coroutine for frame-by-frame animation
- Store original transform values before animating
- Reset transform values when animation completes
- Handle null checks for all UI element references
- Deactivate panels only AFTER close animation completes

---

## 🚀 READY TO IMPLEMENT CHUNK 2?

Copy this into chat:

```
I'm continuing Quantum Mechanic Session 11 - UIManager.cs (Chunk 2/3).

Chunk 1 is complete with all foundation, enums, fields, and initialization.

Now implement PANEL MANAGEMENT & HUD UPDATES:
- OpenPanel() / ClosePanel() / TogglePanel()
- CloseAllPanels() / IsPanelOpen()
- AnimatePanel() coroutine with 7 animation types (Fade, Scale, Slides)
- UpdateHealthBar() / UpdateManaBar() / UpdateLevel()
- UpdateAbilityBar() / UpdateAbilityCooldown()

Output: ~115-125 lines with XML docs and #regions.
Namespace: QuantumMechanic.UI

Ready to generate Chunk 2?
```

---

## 📈 PROGRESS TRACKER

- ✅ **Chunk 1:** Foundation & Core (130 lines) - COMPLETE
- 🔄 **Chunk 2:** Panel Management & HUD (120 lines) - NEXT
- ⏳ **Chunk 3:** Combat Feedback & Inventory (~110 lines) - PENDING

**Total So Far:** ~130 lines
**Estimated Final:** ~360 lines

---

## ✅ COMPLETION CHECKLIST

After Chunk 2, verify:
- [ ] OpenPanel() activates and animates panels
- [ ] ClosePanel() deactivates after animation
- [ ] TogglePanel() switches state correctly
- [ ] CloseAllPanels() preserves HUD
- [ ] AnimatePanel() supports all 7 animation types
- [ ] Fade animation works smoothly
- [ ] Scale animation scales from 0 to 1
- [ ] Slide animations start from screen edges
- [ ] Health/mana bars update correctly
- [ ] Ability slots populate from list
- [ ] Cooldown fills update properly
- [ ] All methods have XML documentation

**🎯 Almost there - one more chunk to go!**