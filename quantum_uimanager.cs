    #endregion

    #region Combat Feedback (Public)

    /// <summary>
    /// Shows a floating damage number at the specified world position.
    /// </summary>
    /// <param name="worldPosition">World position where damage occurred</param>
    /// <param name="damage">Amount of damage to display</param>
    /// <param name="type">Type of damage for color coding</param>
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

    /// <summary>
    /// Shows a hit indicator effect at the specified world position.
    /// </summary>
    /// <param name="worldPosition">World position where hit occurred</param>
    /// <param name="color">Color of the hit indicator</param>
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

    #endregion

    #region Combat Feedback (Private)

    /// <summary>
    /// Updates all active damage numbers (called from Update).
    /// </summary>
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

    /// <summary>
    /// Cleans up expired damage numbers.
    /// </summary>
    private void CleanupDamageNumbers()
    {
        for (int i = activeDamageNumbers.Count - 1; i >= 0; i--)
        {
            if (activeDamageNumbers[i] == null)
            {
                activeDamageNumbers.RemoveAt(i);
            }
        }
    }

    #endregion

    #region Inventory UI (Public)

    /// <summary>
    /// Refreshes the inventory display with current items.
    /// </summary>
    /// <param name="inventory">The inventory system to display</param>
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

    /// <summary>
    /// Updates the gold display.
    /// </summary>
    /// <param name="goldAmount">Current gold amount</param>
    public void UpdateGoldDisplay(int goldAmount)
    {
        if (goldText != null)
        {
            goldText.text = $"Gold: {goldAmount:N0}";
        }
    }

    #endregion

    #region Inventory UI (Private)

    /// <summary>
    /// Creates inventory slot UI elements.
    /// </summary>
    /// <param name="slotCount">Number of slots to create</param>
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

    /// <summary>
    /// Updates a specific inventory slot display.
    /// </summary>
    /// <param name="slot">The slot GameObject to update</param>
    /// <param name="item">Item data to display (null for empty)</param>
    /// <param name="quantity">Quantity of the item</param>
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

    #endregion

    #region Character Panel (Public)

    /// <summary>
    /// Refreshes the character panel with current stats.
    /// </summary>
    /// <param name="stats">Character stats to display</param>
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

    /// <summary>
    /// Updates the buff/debuff display.
    /// </summary>
    /// <param name="buffs">List of active buff instances</param>
    public void UpdateBuffDisplay(List<BuffInstance> buffs)
    {
        if (buffs == null || buffContainer == null)
            return;

        // Clear existing buff icons
        foreach (Transform child in buffContainer)
        {
            Destroy(child.gameObject);
        }

        // Create new buff icons
        foreach (BuffInstance buff in buffs)
        {
            if (buff.Data != null && buffIconPrefab != null)
            {
                GameObject buffIcon = Instantiate(buffIconPrefab, buffContainer);
                
                Image iconImage = buffIcon.GetComponent<Image>();
                if (iconImage != null && buff.Data.Icon != null)
                {
                    iconImage.sprite = buff.Data.Icon;
                }

                // Add tooltip or duration text if prefab supports it
                TextMeshProUGUI durationText = buffIcon.transform.Find("Duration")?.GetComponent<TextMeshProUGUI>();
                if (durationText != null)
                {
                    float remaining = buff.RemainingDuration;
                    durationText.text = remaining > 0 ? $"{remaining:F1}s" : "∞";
                }
            }
        }

        if (showDebugInfo)
            Debug.Log($"[UIManager] Updated buff display with {buffs.Count} buffs");
    }

    #endregion

    #region Character Panel (Private)

    /// <summary>
    /// Creates a stat display element.
    /// </summary>
    /// <param name="statName">Name of the stat</param>
    /// <param name="statValue">Value of the stat</param>
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

    #endregion

    #region Unity Lifecycle (Private)

    /// <summary>
    /// Update loop for damage number animations.
    /// </summary>
    private void Update()
    {
        UpdateDamageNumbers();
    }

    /// <summary>
    /// Cleanup when destroyed.
    /// </summary>
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

    #endregion
}