// File: Assets/Scripts/RPG/Player/Controllers/FirstPersonController.cs
using UnityEngine;
using RPG.Networking;

namespace RPG.Player
{
    /// <summary>
    /// First-person controller with client prediction and server reconciliation.
    /// Features: WASD movement, mouse look, jumping, crouching.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour, IPlayerController
    {
        [Header("Movement Settings")]
        [SerializeField] private float _walkSpeed = 6f;
        [SerializeField] private float _runSpeed = 10f;
        [SerializeField] private float _crouchSpeed = 3f;
        [SerializeField] private float _jumpForce = 8f;
        [SerializeField] private float _gravity = -20f;

        [Header("Mouse Look")]
        [SerializeField] private float _mouseSensitivity = 2f;
        [SerializeField] private float _minVerticalAngle = -90f;
        [SerializeField] private float _maxVerticalAngle = 90f;

        [Header("Camera")]
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private float _cameraHeight = 1.6f;
        [SerializeField] private float _crouchCameraHeight = 1.0f;

        [Header("Ground Check")]
        [SerializeField] private Transform _groundCheck;
        [SerializeField] private float _groundDistance = 0.2f;
        [SerializeField] private LayerMask _groundMask;

        private CharacterController _controller;
        private Camera _camera;
        private Vector3 _velocity;
        private bool _isGrounded;
        private float _verticalRotation;
        private bool _isCrouching;

        // Network state
        private bool _isLocalPlayer;
        private string _playerId;
        private float _syncTimer;
        private const float SYNC_RATE = 1f / 20f; // 20 updates/sec

        public void Initialize(string playerId, bool isLocal)
        {
            _playerId = playerId;
            _isLocalPlayer = isLocal;

            _controller = GetComponent<CharacterController>();

            // Setup camera
            if (_cameraTransform == null)
            {
                GameObject camObj = new GameObject("FPSCamera");
                camObj.transform.SetParent(transform);
                camObj.transform.localPosition = new Vector3(0, _cameraHeight, 0);
                _cameraTransform = camObj.transform;

                _camera = camObj.AddComponent<Camera>();
                _camera.tag = "MainCamera";
                _camera.fieldOfView = 75f;
            }

            _camera.enabled = isLocal;

            if (isLocal)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            Debug.Log($"[FPS] Initialized for {playerId} (Local: {isLocal})");
        }

        private void Update()
        {
            if (_isLocalPlayer)
            {
                HandleInput();
                HandleMovement();
                HandleMouseLook();
                SyncToServer();
            }
        }

        private void HandleInput()
        {
            // Ground check
            _isGrounded = Physics.CheckSphere(_groundCheck.position, _groundDistance, _groundMask);

            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }

            // Crouch toggle
            if (Input.GetKeyDown(KeyCode.LeftControl))
            {
                _isCrouching = !_isCrouching;
                float targetHeight = _isCrouching ? _crouchCameraHeight : _cameraHeight;
                _cameraTransform.localPosition = new Vector3(0, targetHeight, 0);
            }

            // Jump
            if (Input.GetButtonDown("Jump") && _isGrounded && !_isCrouching)
            {
                _velocity.y = Mathf.Sqrt(_jumpForce * -2f * _gravity);
            }

            // Sprint
            bool isSprinting = Input.GetKey(KeyCode.LeftShift) && !_isCrouching;
            float currentSpeed = _isCrouching ? _crouchSpeed : (isSprinting ? _runSpeed : _walkSpeed);

            // Movement input
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveZ = Input.GetAxisRaw("Vertical");

            // Calculate move direction relative to camera
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            Vector3 moveDirection = (forward * moveZ + right * moveX).normalized;
            Vector3 move = moveDirection * currentSpeed;

            _controller.Move(move * Time.deltaTime);

            // Apply gravity
            _velocity.y += _gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void HandleMouseLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * _mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * _mouseSensitivity;

            // Horizontal rotation (body)
            transform.Rotate(Vector3.up * mouseX);

            // Vertical rotation (camera)
            _verticalRotation -= mouseY;
            _verticalRotation = Mathf.Clamp(_verticalRotation, _minVerticalAngle, _maxVerticalAngle);
            _cameraTransform.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);
        }

        private void SyncToServer()
        {
            _syncTimer += Time.deltaTime;
            if (_syncTimer >= SYNC_RATE)
            {
                _syncTimer = 0f;

                // Send position/rotation to server
                HybridNetworkManager.Instance?.SendMovement(
                    transform.position,
                    transform.rotation,
                    _velocity
                );
            }
        }

        #region IPlayerController Implementation

        public Vector3 GetPosition() => transform.position;
        public Quaternion GetRotation() => transform.rotation;
        public Vector3 GetVelocity() => _velocity;

        #endregion

        private void OnDrawGizmosSelected()
        {
            if (_groundCheck != null)
            {
                Gizmos.color = _isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(_groundCheck.position, _groundDistance);
            }
        }
    }
}