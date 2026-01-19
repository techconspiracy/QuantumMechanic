// File: Assets/Scripts/RPG/Player/Controllers/TopDownController.cs
using UnityEngine;
using RPG.Networking;

namespace RPG.Player
{
    /// <summary>
    /// Top-down/isometric controller with click-to-move or WASD controls.
    /// Features: Mouse cursor, pathfinding-ready, camera follow.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class TopDownController : MonoBehaviour, IPlayerController
    {
        [Header("Movement Settings")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _rotationSpeed = 10f;

        [Header("Camera Settings")]
        [SerializeField] private Vector3 _cameraOffset = new Vector3(0, 15, -10);
        [SerializeField] private float _cameraAngle = 45f;
        [SerializeField] private float _cameraDistance = 20f;
        [SerializeField] private bool _lockCameraRotation = true;

        [Header("Input Mode")]
        [SerializeField] private TopDownInputMode _inputMode = TopDownInputMode.WASD;

        [Header("Click-to-Move")]
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private GameObject _moveTargetIndicator;

        private CharacterController _controller;
        private Camera _camera;
        private Transform _cameraTransform;
        private Vector3 _targetPosition;
        private bool _hasTargetPosition;
        private Vector3 _velocity;

        // Network state
        private bool _isLocalPlayer;
        private string _playerId;
        private float _syncTimer;
        private const float SYNC_RATE = 1f / 20f;

        public void Initialize(string playerId, bool isLocal)
        {
            _playerId = playerId;
            _isLocalPlayer = isLocal;

            _controller = GetComponent<CharacterController>();

            // Setup camera
            SetupCamera();

            if (isLocal)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (_moveTargetIndicator != null)
                {
                    _moveTargetIndicator.SetActive(false);
                }
            }

            Debug.Log($"[TopDown] Initialized for {playerId} (Local: {isLocal})");
        }

        private void SetupCamera()
        {
            GameObject camObj = new GameObject("TopDownCamera");
            camObj.transform.SetParent(transform);
            _cameraTransform = camObj.transform;

            _camera = camObj.AddComponent<Camera>();
            _camera.tag = "MainCamera";
            _camera.enabled = _isLocalPlayer;

            // Position camera above and behind
            _cameraTransform.localPosition = _cameraOffset;
            _cameraTransform.localRotation = Quaternion.Euler(_cameraAngle, 0, 0);
        }

        private void Update()
        {
            if (_isLocalPlayer)
            {
                HandleInput();
                HandleMovement();
                UpdateCamera();
                SyncToServer();
            }
        }

        private void HandleInput()
        {
            switch (_inputMode)
            {
                case TopDownInputMode.WASD:
                    HandleWASDInput();
                    break;

                case TopDownInputMode.ClickToMove:
                    HandleClickToMoveInput();
                    break;

                case TopDownInputMode.Both:
                    HandleWASDInput();
                    HandleClickToMoveInput();
                    break;
            }
        }

        private void HandleWASDInput()
        {
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveZ = Input.GetAxisRaw("Vertical");

            if (moveX != 0 || moveZ != 0)
            {
                _hasTargetPosition = false; // Cancel click-to-move
                
                // Move relative to camera
                Vector3 cameraForward = _camera.transform.forward;
                Vector3 cameraRight = _camera.transform.right;

                cameraForward.y = 0;
                cameraRight.y = 0;
                cameraForward.Normalize();
                cameraRight.Normalize();

                Vector3 moveDirection = (cameraForward * moveZ + cameraRight * moveX).normalized;
                
                if (moveDirection.magnitude > 0.1f)
                {
                    // Rotate character to face movement direction
                    Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        _rotationSpeed * Time.deltaTime
                    );

                    // Move character
                    _controller.Move(moveDirection * _moveSpeed * Time.deltaTime);
                }
            }
        }

        private void HandleClickToMoveInput()
        {
            if (Input.GetMouseButtonDown(0)) // Left click
            {
                Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 1000f, _groundLayer))
                {
                    _targetPosition = hit.point;
                    _hasTargetPosition = true;

                    // Show move indicator
                    if (_moveTargetIndicator != null)
                    {
                        _moveTargetIndicator.transform.position = _targetPosition;
                        _moveTargetIndicator.SetActive(true);
                    }

                    Debug.Log($"[TopDown] Moving to: {_targetPosition}");
                }
            }
        }

        private void HandleMovement()
        {
            if (_hasTargetPosition)
            {
                Vector3 direction = (_targetPosition - transform.position);
                direction.y = 0;

                float distance = direction.magnitude;

                if (distance > 0.5f) // Threshold to stop
                {
                    direction.Normalize();

                    // Rotate towards target
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        _rotationSpeed * Time.deltaTime
                    );

                    // Move towards target
                    _controller.Move(direction * _moveSpeed * Time.deltaTime);
                }
                else
                {
                    // Reached target
                    _hasTargetPosition = false;
                    if (_moveTargetIndicator != null)
                    {
                        _moveTargetIndicator.SetActive(false);
                    }
                }
            }

            // Apply gravity
            if (!_controller.isGrounded)
            {
                _velocity.y += -20f * Time.deltaTime;
                _controller.Move(_velocity * Time.deltaTime);
            }
            else
            {
                _velocity.y = 0;
            }
        }

        private void UpdateCamera()
        {
            if (_lockCameraRotation)
            {
                _cameraTransform.localPosition = _cameraOffset;
                _cameraTransform.localRotation = Quaternion.Euler(_cameraAngle, 0, 0);
            }
            else
            {
                // TODO: Allow camera rotation with middle mouse drag
            }
        }

        private void SyncToServer()
        {
            _syncTimer += Time.deltaTime;
            if (_syncTimer >= SYNC_RATE)
            {
                _syncTimer = 0f;

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
    }

    public enum TopDownInputMode
    {
        WASD,
        ClickToMove,
        Both
    }
}