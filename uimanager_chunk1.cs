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
    }
}