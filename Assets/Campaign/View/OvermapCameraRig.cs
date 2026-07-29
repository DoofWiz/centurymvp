using UnityEngine;

namespace Century.Campaign.View
{
    /// <summary>
    /// Overmap camera: follows the player's column, releases to free pan when the player drags or
    /// uses WASD, and snaps back on demand. Modelled on Bannerlord's campaign camera.
    /// </summary>
    /// <remarks>
    /// Built as a pivot plus a boom rather than by moving the camera transform directly, so zoom,
    /// pitch and pan stay independent and none of them fight each other.
    /// </remarks>
    public sealed class OvermapCameraRig : MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField] private float _followLerp = 8f;
        [SerializeField] private bool _followPlayer = true;

        [Header("Boom")]
        [SerializeField] private float _distance = 120f;
        [SerializeField] private float _minDistance = 40f;
        [SerializeField] private float _maxDistance = 320f;
        [SerializeField] private float _zoomSpeed = 400f;
        [SerializeField] private float _zoomLerp = 10f;

        [Header("Angles")]
        [SerializeField] private float _pitchAtMinZoom = 35f;
        [SerializeField] private float _pitchAtMaxZoom = 60f;
        [Tooltip("45 gives the three-quarter overmap view. 0 looks straight down +Z.")]
        [SerializeField] private float _yaw = 45f;
        [SerializeField] private float _yawSpeed = 120f;

        [Header("Pan")]
        [SerializeField] private float _panSpeed = 120f;
        [SerializeField] private float _edgePanBorder = 8f;
        [SerializeField] private bool _edgePanEnabled = true;

        [Header("Bounds")]
        [SerializeField] private Vector2 _boundsMin = new Vector2(-2000f, -2000f);
        [SerializeField] private Vector2 _boundsMax = new Vector2(2000f, 2000f);

        private Transform _target;
        private Vector3 _pivot;
        private float _targetDistance;
        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponentInChildren<Camera>();
            _targetDistance = _distance;
            _pivot = transform.position;
        }

        /// <summary>Called by the overmap bootstrap once the player's view exists.</summary>
        public void SetTarget(Transform target, bool snap = true)
        {
            _target = target;
            _followPlayer = true;
            if (snap && target != null) _pivot = target.position;
        }

        public void ReturnToTarget() => _followPlayer = true;

        private void LateUpdate()
        {
            HandleZoom();
            HandleYaw();
            HandlePan();
            HandleFollow();
            ApplyTransform();
        }

        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
                _targetDistance = Mathf.Clamp(_targetDistance - scroll * _zoomSpeed * Time.deltaTime,
                    _minDistance, _maxDistance);

            _distance = Mathf.Lerp(_distance, _targetDistance, 1f - Mathf.Exp(-_zoomLerp * Time.deltaTime));
        }

        private void HandleYaw()
        {
            if (Input.GetKey(KeyCode.Q)) _yaw -= _yawSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.E)) _yaw += _yawSpeed * Time.deltaTime;
        }

        private void HandlePan()
        {
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            if (_edgePanEnabled && Application.isFocused)
            {
                Vector3 mouse = Input.mousePosition;
                if (mouse.x >= 0f && mouse.x <= Screen.width && mouse.y >= 0f && mouse.y <= Screen.height)
                {
                    if (mouse.x < _edgePanBorder) input.x -= 1f;
                    else if (mouse.x > Screen.width - _edgePanBorder) input.x += 1f;
                    if (mouse.y < _edgePanBorder) input.y -= 1f;
                    else if (mouse.y > Screen.height - _edgePanBorder) input.y += 1f;
                }
            }

            if (Input.GetKeyDown(KeyCode.Space)) ReturnToTarget();
            if (input.sqrMagnitude < 0.0001f) return;

            _followPlayer = false;

            // Pan on the ground plane, relative to the current yaw.
            Quaternion flatYaw = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 delta = flatYaw * new Vector3(input.x, 0f, input.y).normalized;

            // Scale pan speed with zoom so the map feels consistent at any altitude.
            float speed = _panSpeed * (_distance / _maxDistance + 0.35f);
            _pivot += delta * speed * Time.deltaTime;
            ClampPivot();
        }

        private void HandleFollow()
        {
            if (!_followPlayer || _target == null) return;
            _pivot = Vector3.Lerp(_pivot, _target.position, 1f - Mathf.Exp(-_followLerp * Time.deltaTime));
            ClampPivot();
        }

        private void ClampPivot()
        {
            _pivot.x = Mathf.Clamp(_pivot.x, _boundsMin.x, _boundsMax.x);
            _pivot.z = Mathf.Clamp(_pivot.z, _boundsMin.y, _boundsMax.y);
        }

        private void ApplyTransform()
        {
            float zoom01 = Mathf.InverseLerp(_minDistance, _maxDistance, _distance);
            float pitch = Mathf.Lerp(_pitchAtMinZoom, _pitchAtMaxZoom, zoom01);

            transform.position = _pivot;
            transform.rotation = Quaternion.Euler(pitch, _yaw, 0f);

            if (_camera == null) return;

            // The rig owns the camera's orientation entirely. Asserting identity here rather than
            // trusting the scene means a stray rotation on the camera child — which shows up as a
            // rolled horizon and is near-impossible to spot in the inspector — cannot survive.
            _camera.transform.localPosition = new Vector3(0f, 0f, -_distance);
            _camera.transform.localRotation = Quaternion.identity;
        }
    }
}
