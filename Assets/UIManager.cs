using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Netcode;
using RPG.Modules; // Access to our new modules
using RPG.Core;

namespace RPG.UI
{
    public enum UIPanelType { HUD, Inventory, Character, Pause, Dialogue, Death }

    [System.Serializable]
    public class UIPanel
    {
        public UIPanelType PanelType;
        public GameObject PanelObject;
        public CanvasGroup CanvasGroup;
        public bool IsOpen;
    }

    /// <summary>
    /// Central UI Controller for Unity 6 NGO.
    /// Listens for Local Player spawn and binds module events to HUD elements.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("HUD Elements")]
        [SerializeField] private Slider _healthBar;
        [SerializeField] private TextMeshProUGUI _healthText;
        [SerializeField] private TextMeshProUGUI _goldText;
        
        [Header("Panels")]
        [SerializeField] private List<UIPanel> _panels = new List<UIPanel>();
        [SerializeField] private UIPanelType _startingPanel = UIPanelType.HUD;

        private HealthModule _localHealth;
        /// private EconomyModule _localEconomy;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            InitializePanels();
        }

        private void Start()
        {
            ShowPanel(_startingPanel);
            
            // In NGO, the LocalPlayer might not be ready at Start.
            // We subscribe to the OnClientConnectedCallback to catch when we are ready.
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            
            UnbindPlayerEvents();
        }

        private void HandleClientConnected(ulong clientId)
        {
            // Only proceed if this is the local client
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                StopAllCoroutines();
                StartCoroutine(WaitForLocalPlayer());
            }
        }

        private System.Collections.IEnumerator WaitForLocalPlayer()
        {
            // Wait until the local player object is assigned and spawned
            while (NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject() == null)
            {
                yield return null;
            }

            BindToLocalPlayer(NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject());
        }

        /// <summary>
        /// Finds the modules on the local player and hooks into their events.
        /// </summary>
        public void BindToLocalPlayer(NetworkObject player)
        {
            UnbindPlayerEvents(); // Clean up previous bindings

            // 1. Bind Health
            if (player.TryGetComponent(out _localHealth))
            {
                // Note: We access the NetworkVariables via the generated properties
                UpdateHealthUI(_localHealth.CurrentValue, _localHealth.MaxValue);
                // The generated code in HealthModule.generated.cs doesn't expose the private Var, 
                // so we use the Public Events we added in the User Partial class.
            }

            // 2. Bind Economy
  //          if (player.TryGetComponent(out _localEconomy))
  //          {
  //              UpdateGoldUI(_localEconomy.CurrentValue);
  //              _localEconomy.OnCurrencyChanged += UpdateGoldUI;
  //          }

            Debug.Log("[UI Manager] Successfully bound to Local Player Modules.");
        }

        private void UnbindPlayerEvents()
        {
//            if (_localEconomy != null) _localEconomy.OnCurrencyChanged -= UpdateGoldUI;
        }

        #region HUD Updates

        public void UpdateHealthUI(float current, float max)
        {
            if (_healthBar) _healthBar.value = current / max;
            if (_healthText) _healthText.text = $"{Mathf.CeilToInt(current)} / {max}";
        }

//        public void UpdateGoldUI(float amount)
//        {
//            if (_goldText) _goldText.text = amount.ToString("N0");
//        }

        #endregion

        #region Panel Management

        public void ShowPanel(UIPanelType type)
        {
            foreach (var panel in _panels)
            {
                if (panel.PanelType == type)
                {
                    panel.PanelObject.SetActive(true);
                    panel.IsOpen = true;
                    if (panel.CanvasGroup) panel.CanvasGroup.alpha = 1;
                }
                else if (panel.PanelType != UIPanelType.HUD) // Keep HUD visible unless specified
                {
                    panel.PanelObject.SetActive(false);
                    panel.IsOpen = false;
                }
            }
        }

        private void InitializePanels()
        {
            foreach (var panel in _panels)
            {
                if (panel.PanelObject != null && panel.CanvasGroup == null)
                {
                    panel.CanvasGroup = panel.PanelObject.GetComponent<CanvasGroup>();
                }
            }
        }

        #endregion
    }
}