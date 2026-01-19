// File: Assets/Scripts/RPG/Player/NetworkPlayerCamera.cs
using UnityEngine;

namespace RPG.Player
{
    /// <summary>
    /// Third-person camera with mouse orbit and collision detection.
    /// Only active for the local player.
    /// </summary>
    public class NetworkPlayerCamera : MonoBehaviour
    {
        [Header("Camera Target")]
        [SerializeField] private Transform _cameraTarget; // Point above player's head
        [SerializeField] private Vector3 _targetOffset = new Vector3(0, 1.5f, 0);

        [Header("Camera Settings")]
        [SerializeField] private float _distance = 5f;
        [SerializeField] private float _minDistance = 2f;
        [SerializeField] private float _maxDistance = 10f;
        [SerializeField] private float _zoomSpeed = 2f;

        [Header("Rotation Settings")]
        [SerializeField] private float _mouseSensitivity = 3f;
        [SerializeField] private float _minVerticalAngle = -30f;
        [SerializeField] private float _maxVerticalAngle = 70f;
        [SerializeField] private bool _invertY = false;

        [Header("Smoothing")]
        [SerializeField] private float _rotationSmoothTime = 0.1f;
        [SerializeField] private float _positionSmoothTime = 0.1f;

        [Header("Collision")]
        [SerializeField] private LayerMask _collisionMask;
        [SerializeField] private float _collisionRadius = 0.3f;

        private Camera _camera;
        private float _currentX = 0f;
        private float _currentY = 20f;
        private float _currentDistance;
        
        private Vector2 _rotationVelocity;
        private float _distanceVelocity;
        private Vector3 _positionVelocity;

        private void Awake()
        {
            // Create camera if it doesn't exist
            _camera = GetComponentInChildren<Camera>();
            if (_camera == null)
            {
                GameObject cameraObj = new GameObject("PlayerCamera");
                cameraObj.transform.SetParent(transform);
                _camera = cameraObj.AddComponent<Camera>();
                _camera.tag = "MainCamera";
            }

            _currentDistance = _distance;

            // Lock and hide cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Start()
        {
            // Auto-create camera target if not assigned
            if (_cameraTarget == null)
            {
                GameObject targetObj = new GameObject("CameraTarget");
                targetObj.transform.SetParent(transform);
                targetObj.transform.localPosition = _targetOffset;
                _cameraTarget = targetObj.transform;
            }
        }

        private void LateUpdate()
        {
            if (!enabled) return;

            HandleInput();
            UpdateCameraPosition();
        }

        private void HandleInput()
        {
            // Mouse input
            float mouseX = Input.GetAxis("Mouse X") * _mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * _mouseSensitivity;

            if (_invertY) mouseY = -mouseY;

            // Smooth rotation
            _currentX += mouseX;
            _currentY -= mouseY;
            _currentY = Mathf.Clamp(_currentY, _minVerticalAngle, _maxVerticalAngle);

            // Mouse scroll zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _distance = Mathf.Clamp(_distance - scroll * _zoomSpeed, _minDistance, _maxDistance);
            }

            // Toggle cursor lock (ESC key)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        private void UpdateCameraPosition()
        {
            if (_cameraTarget == null) return;

            // Calculate desired rotation
            Quaternion rotation = Quaternion.Euler(_currentY, _currentX, 0);

            // Calculate desired position
            Vector3 direction = rotation * Vector3.back;
            Vector3 desiredPosition = _cameraTarget.position + direction * _distance;

            // Check for collisions
            float collisionDistance = CheckCameraCollision(_cameraTarget.position, direction, _distance);
            _currentDistance = Mathf.SmoothDamp(_currentDistance, collisionDistance, ref _distanceVelocity, _positionSmoothTime);

            // Apply collision-adjusted position
            Vector3 finalPosition = _cameraTarget.position + direction * _currentDistance;

            // Smooth position
            _camera.transform.position = Vector3.SmoothDamp(
                _camera.transform.position,
                finalPosition,
                ref _positionVelocity,
                _positionSmoothTime
            );

            // Look at target
            _camera.transform.LookAt(_cameraTarget);
        }

        private float CheckCameraCollision(Vector3 targetPosition, Vector3 direction, float desiredDistance)
        {
            RaycastHit hit;
            
            if (Physics.SphereCast(
                targetPosition,
                _collisionRadius,
                direction,
                out hit,
                desiredDistance,
                _collisionMask
            ))
            {
                // Camera hit something, pull closer
                return Mathf.Clamp(hit.distance - _collisionRadius, _minDistance, desiredDistance);
            }

            return desiredDistance;
        }

        #region Public API

        /// <summary>
        /// Instantly snap camera to behind the player (useful after teleports)
        /// </summary>
        public void ResetCamera()
        {
            _currentX = transform.eulerAngles.y;
            _currentY = 20f;
            _currentDistance = _distance;
        }

        /// <summary>
        /// Shake the camera (damage feedback, explosions, etc.)
        /// </summary>
        public void Shake(float intensity = 0.3f, float duration = 0.2f)
        {
            StartCoroutine(ShakeCoroutine(intensity, duration));
        }

        private System.Collections.IEnumerator ShakeCoroutine(float intensity, float duration)
        {
            Vector3 originalPosition = _camera.transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * intensity;
                float y = Random.Range(-1f, 1f) * intensity;

                _camera.transform.localPosition += new Vector3(x, y, 0);

                elapsed += Time.deltaTime;
                yield return null;
            }

            _camera.transform.localPosition = originalPosition;
        }

        #endregion

        #region Debug Visualization

        private void OnDrawGizmosSelected()
        {
            if (_cameraTarget == null) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_cameraTarget.position, 0.2f);

            Gizmos.color = Color.yellow;
            Vector3 direction = Quaternion.Euler(_currentY, _currentX, 0) * Vector3.back;
            Gizmos.DrawLine(_cameraTarget.position, _cameraTarget.position + direction * _distance);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_cameraTarget.position + direction * _distance, _collisionRadius);
        }

        #endregion
    }
}