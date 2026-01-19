// File: Assets/Scripts/RPG/Core/WebSocketBootstrap.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using RPG.Networking;
using RPG.UI;

namespace RPG.Core
{
    /// <summary>
    /// Handles connection flow and scene transitions for WebSocket multiplayer.
    /// Replaces the NGO NetworkBootstrap with WebSocket-specific logic.
    /// </summary>
    public class WebSocketBootstrap : MonoBehaviour
    {
        [Header("Scene Configuration")]
        [SerializeField] private string _gameplaySceneName = "World_Main";
        [SerializeField] private string _menuSceneName = "MainMenu";

        [Header("Connection UI")]
        [SerializeField] private GameObject _connectingPanel;
        [SerializeField] private GameObject _connectionFailedPanel;
        [SerializeField] private TMPro.TextMeshProUGUI _statusText;

        [Header("Development")]
        [SerializeField] private bool _autoConnectInEditor = true;
        [SerializeField] private string _devServerAddress = "ws://localhost";

        private bool _isConnecting;
        private bool _isConnected;

        private void Start()
        {
            // Setup WebSocket event listeners
            if (WebSocketNetworkManager.Instance != null)
            {
                WebSocketNetworkManager.Instance.OnConnected += HandleConnected;
                WebSocketNetworkManager.Instance.OnDisconnected += HandleDisconnected;
            }

            // Auto-connect in editor for faster testing
            if (Application.isEditor && _autoConnectInEditor)
            {
                ConnectToServer();
            }
        }

        private void OnDestroy()
        {
            if (WebSocketNetworkManager.Instance != null)
            {
                WebSocketNetworkManager.Instance.OnConnected -= HandleConnected;
                WebSocketNetworkManager.Instance.OnDisconnected -= HandleDisconnected;
            }
        }

        #region Connection Management

        public void ConnectToServer()
        {
            if (_isConnecting || _isConnected)
            {
                Debug.LogWarning("[Bootstrap] Already connecting or connected!");
                return;
            }

            _isConnecting = true;
            UpdateStatusUI("Connecting to server...");
            ShowPanel(_connectingPanel);

            // Override server address in editor
            if (Application.isEditor && !string.IsNullOrEmpty(_devServerAddress))
            {
                Debug.Log($"[Bootstrap] Using dev server: {_devServerAddress}");
                // You would set this on the NetworkManager here
            }

            // Initiate connection
            if (WebSocketNetworkManager.Instance != null)
            {
                WebSocketNetworkManager.Instance.ConnectAsync();
            }
            else
            {
                Debug.LogError("[Bootstrap] WebSocketNetworkManager not found!");
                HandleConnectionFailed("Network Manager not initialized");
            }
        }

        public void Disconnect()
        {
            _isConnected = false;
            _isConnecting = false;

            if (WebSocketNetworkManager.Instance != null)
            {
                WebSocketNetworkManager.Instance.DisconnectAsync();
            }

            // Return to menu
            LoadScene(_menuSceneName);
        }

        #endregion

        #region Event Handlers

        private void HandleConnected()
        {
            _isConnecting = false;
            _isConnected = true;

            Debug.Log("[Bootstrap] Successfully connected to server!");
            UpdateStatusUI("Connected! Loading game world...");

            // Load gameplay scene
            LoadScene(_gameplaySceneName);
        }

        private void HandleDisconnected(string reason)
        {
            _isConnected = false;
            _isConnecting = false;

            Debug.Log($"[Bootstrap] Disconnected from server: {reason}");
            HandleConnectionFailed(reason);
        }

        private void HandleConnectionFailed(string reason)
        {
            UpdateStatusUI($"Connection failed: {reason}");
            ShowPanel(_connectionFailedPanel);
        }

        #endregion

        #region Scene Management

        private void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[Bootstrap] Scene name is empty!");
                return;
            }

            Debug.Log($"[Bootstrap] Loading scene: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }

        #endregion

        #region UI Management

        private void ShowPanel(GameObject panel)
        {
            // Hide all panels first
            if (_connectingPanel != null) _connectingPanel.SetActive(false);
            if (_connectionFailedPanel != null) _connectionFailedPanel.SetActive(false);

            // Show requested panel
            if (panel != null)
            {
                panel.SetActive(true);
            }
        }

        private void UpdateStatusUI(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }
            Debug.Log($"[Bootstrap] {message}");
        }

        #endregion

        #region Button Callbacks (Called from UI)

        public void OnConnectButtonClicked()
        {
            ConnectToServer();
        }

        public void OnDisconnectButtonClicked()
        {
            Disconnect();
        }

        public void OnRetryButtonClicked()
        {
            ShowPanel(_connectingPanel);
            ConnectToServer();
        }

        public void OnBackToMenuButtonClicked()
        {
            Disconnect();
        }

        #endregion
    }
}