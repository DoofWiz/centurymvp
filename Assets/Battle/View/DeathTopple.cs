using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// The fall of a killed man: a light, physical-looking topple in the direction the killing blow
    /// travelled, with a little slide, a little overshoot at the ground, and a scatter of spin — in
    /// place of the old instant flip from standing to lying. Deliberately understated: at battle-camera
    /// height a body should DROP, not cartwheel.
    /// </summary>
    /// <remarks>
    /// Hand-integrated rather than a Rigidbody ragdoll: the corpse needs no colliders, never fights
    /// the living for physics, always settles exactly on the sculpted ground, and costs nothing once
    /// the component destroys itself at rest. The final pose matches the old one (90° over, lifted
    /// 0.12 so the body's thickness sits ON the ground — the rig's pivot is at the feet), so
    /// everything downstream that reads corpses is unchanged.
    /// </remarks>
    public sealed class DeathTopple : MonoBehaviour
    {
        private Transform _mover;
        private Transform _body;
        private Vector3 _fall;
        private Vector3 _startPos;
        private Quaternion _startRot;
        private Vector3 _axis;
        private float _slide;
        private float _spin;
        private float _seconds;
        private float _time;

        /// <summary>
        /// Starts the fall. <paramref name="mover"/> is the transform whose world position carries
        /// the man; <paramref name="bodyRoot"/> the one carrying his rotation (they may be the same).
        /// <paramref name="fallDirection"/> is the flat direction the killing blow travelled.
        /// </summary>
        public static void Begin(
            GameObject host, Transform mover, Transform bodyRoot,
            Vector3 fallDirection, SoldierRig rig, int seed)
        {
            var topple = host.AddComponent<DeathTopple>();
            var random = new System.Random(seed);
            float Spread(float min, float max) => min + (float)random.NextDouble() * (max - min);

            fallDirection.y = 0f;
            topple._fall = fallDirection.sqrMagnitude > 0.001f
                ? fallDirection.normalized
                : (bodyRoot != null ? -bodyRoot.forward : Vector3.back);
            topple._fall.y = 0f;
            if (topple._fall.sqrMagnitude < 0.001f) topple._fall = Vector3.back;
            topple._fall.Normalize();

            topple._mover = mover;
            topple._body = bodyRoot != null ? bodyRoot : mover;
            topple._startPos = mover.position;
            topple._startRot = topple._body.rotation;
            topple._axis = Vector3.Cross(Vector3.up, topple._fall);
            topple._slide = Spread(0.3f, 0.55f);
            topple._spin = Spread(-22f, 22f);
            topple._seconds = Spread(0.7f, 0.9f);

            rig?.SetLimp(seed);
        }

        private void Update()
        {
            _time += Time.deltaTime;
            float p = Mathf.Clamp01(_time / _seconds);

            // Gravity's shape: slow off the feet, fast into the ground, an 8° overshoot at impact
            // that settles back — the whole "ragdoll" read in one curve.
            float angle = p < 0.72f
                ? 98f * Mathf.Pow(p / 0.72f, 1.7f)
                : Mathf.Lerp(98f, 90f, EaseOutQuad((p - 0.72f) / 0.28f));

            float yaw = _spin * Mathf.SmoothStep(0f, 1f, p);
            _body.rotation = Quaternion.AngleAxis(yaw, Vector3.up)
                             * Quaternion.AngleAxis(angle, _axis)
                             * _startRot;

            // The blow carries him a step; the ground receives him with the lying-height lift.
            Vector3 at = _startPos + _fall * (_slide * EaseOutQuad(p));
            at.y = BattleTerrainBuilder.GroundHeight(at) + 0.12f * Mathf.Clamp01(angle / 90f);
            _mover.position = at;

            if (p >= 1f) Destroy(this);
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    }
}
