using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// Foxhole-style battle camera: fixed pitch, follows the Centurion, and leans toward the cursor
    /// so that looking where you aim costs nothing.
    /// </summary>
    /// <remarks>
    /// The lean is the important part. A camera rigidly centred on the player hides exactly the
    /// ground you are about to order men onto; offsetting toward the cursor means the command you
    /// are about to give is always on screen.
    /// </remarks>
    public sealed class BattleCameraRig : MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField] private float _followLerp = 10f;

        [Header("Boom")]
        [SerializeField] private float _distance = 26f;
        [SerializeField] private float _minDistance = 14f;
        [SerializeField] private float _maxDistance = 48f;
        [SerializeField] private float _zoomSpeed = 60f;
        [SerializeField] private float _zoomLerp = 10f;

        [Header("Angles")]
        [SerializeField] private float _pitch = 55f;
        [SerializeField] private float _yaw;

        [Header("Cursor lean")]
        [Tooltip("Fraction of the distance to the cursor that the camera shifts.")]
        [Range(0f, 0.6f)] [SerializeField] private float _leanFactor = 0.28f;
        [SerializeField] private float _maxLean = 9f;

        /// <summary>False while a modal phase owns the screen; the camera then simply holds its frame.</summary>
        public bool IsInputEnabled { get; set; } = true;

        private Transform _target;
        private Camera _camera;
        private Vector3 _pivot;
        private float _targetDistance;
        private Plane _groundPlane;

        // The rally's dolly zoom: 0 = the natural lens, 1 = tightened. The boom compensates so the
        // frame holds while the world's perspective compresses — the classic in-camera "surge".
        private float _lens;
        private float _lensTarget;
        private float _baseFov = -1f;

        /// <summary>Eases the dolly-zoom lens in while on, back out while off. Driven by RallyFx.</summary>
        public void SetRallyLens(bool on) => _lensTarget = on ? 1f : 0f;

        private void Awake()
        {
            _camera = GetComponentInChildren<Camera>();
            _targetDistance = _distance;
            _pivot = transform.position;
            _groundPlane = new Plane(Vector3.up, Vector3.zero);

            // The concept art's framing: the player must be ABLE to come down close enough to see
            // helmets and crests, whatever the scene's serialized floor was.
            _minDistance = Mathf.Min(_minDistance, 7f);
        }

        public void SetTarget(Transform target, bool snap = true)
        {
            _target = target;
            if (snap && target != null) _pivot = target.position;
        }

        /// <summary>Sets the boom length from a script (the opening comes in close). Eases there
        /// unless snapped; the player's wheel takes over again afterwards.</summary>
        public void SetDistance(float distance, bool snap = false)
        {
            _targetDistance = Mathf.Clamp(distance, _minDistance, _maxDistance);
            if (snap) _distance = _targetDistance;
        }

        /// <summary>
        /// World point under the cursor ON THE GROUND. Physics first (the sculpted terrain), the
        /// flat plane as fallback — a plane-only answer lands orders metres off on any hillside.
        /// </summary>
        public bool TryGetCursorGroundPoint(out Vector3 point)
        {
            point = Vector3.zero;
            if (_camera == null) return false;

            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 600f))
            {
                point = hit.point;
                return true;
            }

            if (!_groundPlane.Raycast(ray, out float enter)) return false;
            point = ray.GetPoint(enter);
            return true;
        }

        private void LateUpdate()
        {
            if (IsInputEnabled) HandleZoom();

            if (_target == null) return;

            Vector3 desired = _target.position;

            if (IsInputEnabled && TryGetCursorGroundPoint(out Vector3 cursor))
            {
                Vector3 lean = (cursor - _target.position) * _leanFactor;
                lean.y = 0f;
                desired += Vector3.ClampMagnitude(lean, _maxLean);
            }

            // Unscaled time: the commander's eye keeps moving through a tactical pause.
            _pivot = Vector3.Lerp(_pivot, desired, 1f - Mathf.Exp(-_followLerp * Time.unscaledDeltaTime));

            transform.position = _pivot;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            if (_camera == null) return;

            // The lens: unscaled time, like the eye — the rally reads through a tactical pause too.
            if (_baseFov < 0f) _baseFov = _camera.fieldOfView;
            _lens = Mathf.MoveTowards(_lens, _lensTarget, Time.unscaledDeltaTime / 0.6f);
            float fov = Mathf.Lerp(_baseFov, _baseFov * 0.8f, _lens);
            float compensate = Mathf.Tan(_baseFov * 0.5f * Mathf.Deg2Rad)
                               / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            _camera.fieldOfView = fov;

            // As with the overmap rig, the camera's orientation is owned here rather than trusted
            // to whatever rotation the child transform happens to be carrying.
            _camera.transform.localPosition = new Vector3(0f, 0f, -_distance * compensate);
            _camera.transform.localRotation = Quaternion.identity;
        }

        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
                _targetDistance = Mathf.Clamp(_targetDistance - scroll * _zoomSpeed * Time.unscaledDeltaTime,
                    _minDistance, _maxDistance);

            _distance = Mathf.Lerp(_distance, _targetDistance, 1f - Mathf.Exp(-_zoomLerp * Time.unscaledDeltaTime));
        }
    }
}
