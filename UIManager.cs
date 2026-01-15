using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using QuantumMechanic.Core;
using QuantumMechanic.Combat;

namespace QuantumMechanic.UI
{
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

    /// <summary>
    /// Manages all UI systems including HUD, menus, inventory, and combat feedback.
    /// </summary>
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

        #region Initialization

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

        #endregion
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

}

}