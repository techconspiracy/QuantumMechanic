#region Panel Management (Public & Private)

    /// <summary>
    /// Opens a UI panel with optional animation.
    /// </summary>
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
            if (panel.CanvasGroup != null)
            {
                panel.CanvasGroup.alpha = 1f;
                panel.CanvasGroup.interactable = true;
                panel.CanvasGroup.blocksRaycasts = true;
            }
        }

        if (showDebugInfo)
            Debug.Log($"[UIManager] Opened panel: {panelType}");
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

        panel.IsOpen = false;

        if (animated && panel.CloseAnimation != UIAnimationType.None)
        {
            StartCoroutine(AnimatePanel(panel, false));
        }
        else
        {
            panel.PanelObject.SetActive(false);
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
            panel.CanvasGroup.interactable = true;
            panel.CanvasGroup.blocksRaycasts = true;
            panel.PanelObject.transform.localScale = originalScale;
            panel.PanelObject.transform.localPosition = originalPosition;
        }
        else
        {
            panel.CanvasGroup.alpha = 0f;
            panel.CanvasGroup.interactable = false;
            panel.CanvasGroup.blocksRaycasts = false;
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
        for (int i = 0; i < abilities.Count && i < maxAbilitySlots; i++)
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
