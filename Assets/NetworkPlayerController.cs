// File: Assets/Scripts/RPG/Player/NetworkPlayerController.cs
using UnityEngine;
using RPG.Networking;

namespace RPG.Player
{
    /// <summary>
    /// Third-person character controller with client-side prediction.
    /// Syncs state via WebSocket, uses CharacterController for movement.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class NetworkPlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float _walkSpeed = 5f;
        [SerializeField] private float _runSpeed = 8f;
        [SerializeField] private float _jumpForce = 7f;
        [SerializeField] private float _gravity = -20f;
        [SerializeField] private float _rotationSpeed = 10f;

        [Header("Ground Check")]
        [SerializeField] private Transform _groundCheck;
        [SerializeField] private float _groundDistance = 0.2f;
        [SerializeField] private LayerMask _groundMask;

        [Header("Network Sync")]
        [SerializeField] private float _syncRate = 20f; // Updates per second
        [SerializeField] private float _interpolationSpeed = 15f;

        private CharacterController _controller;
        private NetworkPlayerCamera _camera;
        private Vector3 _velocity;
        private bool _isGrounded;
        private float _syncTimer;

        // Network state
        private bool _isLocalPlayer;
        private string _playerId;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;

        // Input
        private Vector2 _moveInput;
        private bool _jumpInput;
        private bool _runInput;

        public bool IsLocalPlayer => _isLocalPlayer;
        public string PlayerId => _playerId;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _camera = GetComponent<NetworkPlayerCamera>();
        }

        private void Start()
        {
            // Subscribe to network events
            if (WebSocketNetworkManager.Instance != null)
            {
                WebSocketNetworkManager.Instance.OnMessageReceived += HandleNetworkMessage;
            }
        }

        private void OnDestroy()
        {
            if (WebSocketNetworkManager.Instance != null)
            {
                WebSocketNetworkManager.Instance.OnMessageReceived -= HandleNetworkMessage;
            }
        }

        public void Initialize(string playerId, bool isLocal)
        {
            _playerId = playerId;
            _isLocalPlayer = isLocal;

            // Enable camera and input only for local player
            if (_camera != null)
            {
                _camera.enabled = isLocal;
            }

            if (isLocal)
            {
                // Setup input system
                gameObject.tag = "Player";
                Debug.Log($"[Player] Initialized local player: {playerId}");
            }
            else
            {
                // Remote players use interpolation
                _targetPosition = transform.position;
                _targetRotation = transform.rotation;
                Debug.Log($"[Player] Initialized remote player: {playerId}");
            }
        }

        private void Update()
        {
            if (_isLocalPlayer)
            {
                HandleInput();
                HandleMovement();
                SyncPositionToServer();
            }
            else
            {
                InterpolateRemotePlayer();
            }
        }

        #region Local Player Logic

        private void HandleInput()
        {
            // WASD movement
            _moveInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            );

            // Jump
            _jumpInput = Input.GetButtonDown("Jump");

            // Run (Shift)
            _runInput = Input.GetKey(KeyCode.LeftShift);
        }

        private void HandleMovement()
        {
            // Ground check
            _isGrounded = Physics.CheckSphere(_groundCheck.position, _groundDistance, _groundMask);

            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f; // Small downward force to keep grounded
            }

            // Movement direction relative to camera
            Vector3 cameraForward = _camera != null ? _camera.transform.forward : transform.forward;
            Vector3 cameraRight = _camera != null ? _camera.transform.right : transform.right;

            // Flatten camera directions (ignore Y)
            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();

            // Calculate movement direction
            Vector3 moveDirection = (cameraForward * _moveInput.y + cameraRight * _moveInput.x).normalized;

            // Apply movement speed
            float speed = _runInput ? _runSpeed : _walkSpeed;
            Vector3 move = moveDirection * speed;

            _controller.Move(move * Time.deltaTime);

            // Rotate player to face movement direction
            if (moveDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    _rotationSpeed * Time.deltaTime
                );
            }

            // Jump
            if (_jumpInput && _isGrounded)
            {
                _velocity.y = Mathf.Sqrt(_jumpForce * -2f * _gravity);
            }

            // Apply gravity
            _velocity.y += _gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void SyncPositionToServer()
        {
            _syncTimer += Time.deltaTime;

            if (_syncTimer >= 1f / _syncRate)
            {
                _syncTimer = 0f;

                var message = new PlayerMovementMessage
                {
                    messageType = MessageType.PlayerMovement,
                    position = transform.position,
                    rotation = transform.rotation,
                    velocity = _velocity,
                    isGrounded = _isGrounded
                };

                message.payload = JsonUtility.ToJson(new MovementData
                {
                    position = transform.position,
                    rotation = transform.rotation,
                    velocity = _velocity,
                    isGrounded = _isGrounded
                });

                WebSocketNetworkManager.Instance?.SendMessage(message);
            }
        }

        #endregion

        #region Remote Player Logic

        private void InterpolateRemotePlayer()
        {
            // Smoothly interpolate to target position and rotation
            transform.position = Vector3.Lerp(
                transform.position,
                _targetPosition,
                _interpolationSpeed * Time.deltaTime
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                _targetRotation,
                _interpolationSpeed * Time.deltaTime
            );
        }

        private void HandleNetworkMessage(NetworkMessage message)
        {
            if (message.messageType != MessageType.PlayerMovement) return;
            if (message.senderId == _playerId) return; // Ignore own messages

            // Update remote player position
            if (!string.IsNullOrEmpty(message.payload))
            {
                MovementData data = JsonUtility.FromJson<MovementData>(message.payload);
                _targetPosition = data.position;
                _targetRotation = data.rotation;
            }
        }

        #endregion

        #region Debugging

        private void OnDrawGizmosSelected()
        {
            if (_groundCheck != null)
            {
                Gizmos.color = _isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(_groundCheck.position, _groundDistance);
            }
        }

        #endregion
    }

    [System.Serializable]
    public class MovementData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public bool isGrounded;
    }
}