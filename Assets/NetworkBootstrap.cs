using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using RPG.UI; // Keep: Needed for UI transitions

namespace RPG.Core
{
    public class NetworkBootstrap : MonoBehaviour
    {
        [Header("Scene Config")]
        [SerializeField] private string _gameplaySceneName = "World_Main";
        
        [Header("Development")]
        [SerializeField] private bool _autoStartHostInEditor = true;

        private void Start()
        {
            // Keep: Great for fast iteration in Unity Editor
            if (Application.isEditor && _autoStartHostInEditor)
            {
                StartHost();
            }
        }

        public void StartHost()
        {
            // Keep: Essential for syncing scenes across the network
            NetworkManager.Singleton.OnServerStarted += HandleServerStarted;

            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("[Bootstrap] Host Started Successfully.");
                OnConnectionStarted();
            }
            else
            {
                Debug.LogError("[Bootstrap] Failed to start Host.");
            }
        }

        public void StartClient()
        {
            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("[Bootstrap] Client Connecting...");
                OnConnectionStarted();
            }
        }

        private void HandleServerStarted()
        {
            // Keep: This is the "Horse" that pulls the clients into the game scene
            if (NetworkManager.Singleton.IsServer)
            {
                // Unsubscribe to prevent double-calls if server restarts
                NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;

                Debug.Log($"[Bootstrap] Server started. Loading: {_gameplaySceneName}");
                
                NetworkManager.Singleton.SceneManager.LoadScene(
                    _gameplaySceneName, 
                    LoadSceneMode.Single
                );
            }
        }

        private void OnConnectionStarted()
        {
            // Keep: Hides the menu and prepares the HUD
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowPanel(UIPanelType.HUD);
            }
        }

        public void Shutdown()
        {
            // Keep: Clean exit logic
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
            
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowPanel(UIPanelType.Pause);
            }
        }
    }
}