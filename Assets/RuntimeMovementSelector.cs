// File: Assets/Scripts/RPG/Player/RuntimeMovementSelector.cs
using UnityEngine;
using RPG.Networking;

namespace RPG.Player
{
    /// <summary>
    /// Allows players to switch between movement styles during runtime.
    /// Supports: Third-Person, First-Person, Top-Down (Isometric)
    /// Dynamically switches camera and controls based on selection.
    /// </summary>
    public class RuntimeMovementSelector : MonoBehaviour
    {
        [Header("Movement Prefabs")]
        [SerializeField] private GameObject _thirdPersonPrefab;
        [SerializeField] private GameObject _firstPersonPrefab;
        [SerializeField] private GameObject _topDownPrefab;

        [Header("Initial Mode")]
        [SerializeField] private MovementMode _startingMode = MovementMode.ThirdPerson;

        [Header("Voice/Text Chat")]
        [SerializeField] private bool _enableVoiceChat = false;
        [SerializeField] private bool _enableTextChat = true;

        public event System.Action<MovementMode> OnMovementModeChanged;

        private MovementMode _currentMode;
        private GameObject _activeController;
        private IPlayerController _activeInterface;
        private bool _isLocalPlayer;
        private string _playerId;

        // Chat components
        private VoiceChatManager _voiceChat;
        private TextChatManager _textChat;

        public MovementMode CurrentMode => _currentMode;

        public void Initialize(string playerId, bool isLocal)
        {
            _playerId = playerId;
            _isLocalPlayer = isLocal;

            // Initialize chat systems
            if (_isLocalPlayer)
            {
                if (_enableVoiceChat)
                {
                    _voiceChat = gameObject.AddComponent<VoiceChatManager>();
                    _voiceChat.Initialize(playerId);
                }

                if (_enableTextChat)
                {
                    _textChat = gameObject.AddComponent<TextChatManager>();
                    _textChat.Initialize(playerId);
                }
            }

            // Start with initial mode
            SwitchMovementMode(_startingMode);
        }

        private void Update()
        {
            if (!_isLocalPlayer) return;

            // Hotkey switching (F1-F3)
            if (Input.GetKeyDown(KeyCode.F1))
            {
                SwitchMovementMode(MovementMode.ThirdPerson);
            }
            else if (Input.GetKeyDown(KeyCode.F2))
            {
                SwitchMovementMode(MovementMode.FirstPerson);
            }
            else if (Input.GetKeyDown(KeyCode.F3))
            {
                SwitchMovementMode(MovementMode.TopDown);
            }

            // Toggle voice chat (V key)
            if (_enableVoiceChat && Input.GetKeyDown(KeyCode.V))
            {
                _voiceChat?.ToggleMicrophone();
            }

            // Open text chat (Enter key)
            if (_enableTextChat && Input.GetKeyDown(KeyCode.Return))
            {
                _textChat?.OpenChatInput();
            }
        }

        public void SwitchMovementMode(MovementMode newMode)
        {
            if (_currentMode == newMode && _activeController != null) return;

            Debug.Log($"[Movement] Switching to {newMode} mode");

            // Store current state
            Vector3 currentPosition = transform.position;
            Quaternion currentRotation = transform.rotation;

            // Destroy old controller
            if (_activeController != null)
            {
                Destroy(_activeController);
            }

            // Instantiate new controller
            GameObject prefab = GetPrefabForMode(newMode);
            if (prefab == null)
            {
                Debug.LogError($"[Movement] No prefab assigned for {newMode}!");
                return;
            }

            _activeController = Instantiate(prefab, transform);
            _activeController.transform.localPosition = Vector3.zero;
            _activeController.transform.localRotation = Quaternion.identity;

            // Get controller interface
            _activeInterface = _activeController.GetComponent<IPlayerController>();
            if (_activeInterface == null)
            {
                Debug.LogError($"[Movement] Controller doesn't implement IPlayerController!");
            }
            else
            {
                _activeInterface.Initialize(_playerId, _isLocalPlayer);
            }

            _currentMode = newMode;
            OnMovementModeChanged?.Invoke(newMode);

            // Restore position/rotation
            transform.position = currentPosition;
            transform.rotation = currentRotation;
        }

        private GameObject GetPrefabForMode(MovementMode mode)
        {
            switch (mode)
            {
                case MovementMode.ThirdPerson:
                    return _thirdPersonPrefab;
                case MovementMode.FirstPerson:
                    return _firstPersonPrefab;
                case MovementMode.TopDown:
                    return _topDownPrefab;
                default:
                    return null;
            }
        }

        #region Public API

        public void SetVoiceChatEnabled(bool enabled)
        {
            _enableVoiceChat = enabled;
            if (_voiceChat != null)
            {
                _voiceChat.enabled = enabled;
            }
        }

        public void SetTextChatEnabled(bool enabled)
        {
            _enableTextChat = enabled;
            if (_textChat != null)
            {
                _textChat.enabled = enabled;
            }
        }

        public IPlayerController GetActiveController()
        {
            return _activeInterface;
        }

        #endregion
    }

    #region Movement Modes

    public enum MovementMode
    {
        ThirdPerson,
        FirstPerson,
        TopDown
    }

    #endregion

    #region Controller Interface

    /// <summary>
    /// Common interface for all movement controllers
    /// </summary>
    public interface IPlayerController
    {
        void Initialize(string playerId, bool isLocal);
        Vector3 GetPosition();
        Quaternion GetRotation();
        Vector3 GetVelocity();
    }

    #endregion

    #region Chat Systems

    /// <summary>
    /// Voice chat manager using Unity Microphone API
    /// TODO: Implement full voice encoding/streaming
    /// </summary>
    public class VoiceChatManager : MonoBehaviour
    {
        private string _playerId;
        private AudioSource _audioSource;
        private bool _isMicrophoneActive;
        private string _microphoneName;

        public void Initialize(string playerId)
        {
            _playerId = playerId;
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f; // 3D sound
            _audioSource.minDistance = 5f;
            _audioSource.maxDistance = 20f;

            // Get default microphone
            if (Microphone.devices.Length > 0)
            {
                _microphoneName = Microphone.devices[0];
                Debug.Log($"[VoiceChat] Using microphone: {_microphoneName}");
            }
            else
            {
                Debug.LogWarning("[VoiceChat] No microphone detected!");
            }
        }

        public void ToggleMicrophone()
        {
            if (_isMicrophoneActive)
            {
                StopMicrophone();
            }
            else
            {
                StartMicrophone();
            }
        }

        private void StartMicrophone()
        {
            if (string.IsNullOrEmpty(_microphoneName)) return;

            // TODO: Advanced Implementation
            // 1. Start recording: Microphone.Start()
            // 2. Encode audio data (Opus codec recommended)
            // 3. Send chunks via WebSocket to server
            // 4. Server broadcasts to nearby players
            // 5. Decode and play on receivers

            Debug.Log("[VoiceChat] Microphone started (TODO: Implement encoding/streaming)");
            _isMicrophoneActive = true;

            // BASIC EXAMPLE (local only):
            // _audioSource.clip = Microphone.Start(_microphoneName, true, 10, 44100);
            // _audioSource.loop = true;
            // while (!(Microphone.GetPosition(_microphoneName) > 0)) { }
            // _audioSource.Play();
        }

        private void StopMicrophone()
        {
            if (string.IsNullOrEmpty(_microphoneName)) return;

            Microphone.End(_microphoneName);
            _isMicrophoneActive = false;
            Debug.Log("[VoiceChat] Microphone stopped");
        }

        private void OnDestroy()
        {
            if (_isMicrophoneActive)
            {
                StopMicrophone();
            }
        }
    }

    /// <summary>
    /// Text chat manager with network synchronization
    /// </summary>
    public class TextChatManager : MonoBehaviour
    {
        private string _playerId;
        private bool _isChatOpen;

        public event System.Action<string, string> OnMessageReceived; // (senderId, message)

        public void Initialize(string playerId)
        {
            _playerId = playerId;

            // Subscribe to chat messages
            if (HybridNetworkManager.Instance != null)
            {
                HybridNetworkManager.Instance.OnMessageReceived += HandleNetworkMessage;
            }
        }

        private void OnDestroy()
        {
            if (HybridNetworkManager.Instance != null)
            {
                HybridNetworkManager.Instance.OnMessageReceived -= HandleNetworkMessage;
            }
        }

        public void OpenChatInput()
        {
            // TODO: Show chat UI input field
            // This would typically open a UI panel for text input
            Debug.Log("[TextChat] Opening chat input...");
            _isChatOpen = true;
        }

        public void SendMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            Debug.Log($"[TextChat] Sending: {message}");
            
            HybridNetworkManager.Instance?.SendChatMessage(message);
            _isChatOpen = false;
        }

        private void HandleNetworkMessage(NetworkMessage message)
        {
            if (message.messageType == MessageType.ChatMessage)
            {
                string text = message.payload;
                Debug.Log($"[TextChat] {message.senderId}: {text}");
                OnMessageReceived?.Invoke(message.senderId, text);
            }
        }
    }

    #endregion
}