using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// The pilum aim preview: a trajectory arc from the Centurion to a ground reticle under the
    /// cursor, both drawn from the exact ballistic solve the throw will use, so what you aim is what
    /// you get. Built entirely in code — a LineRenderer for the arc and a flat disc for the reticle.
    /// </summary>
    public sealed class BattleAimIndicator : MonoBehaviour
    {
        private const int Samples = 24;

        private LineRenderer _line;
        private GameObject _reticle;
        private Material _material;
        private bool _built;

        public void Show(Vector3 from, Vector3 target, BattleMissiles missiles)
        {
            if (missiles == null) return;
            EnsureBuilt();

            Vector3 velocity = missiles.SolveLaunchVelocity(from, target);
            Vector3 gravity = Physics.gravity;

            float flight = TimeToLand(from.y, target.y, velocity.y, gravity.y);

            _line.enabled = true;
            _line.positionCount = Samples;
            for (int i = 0; i < Samples; i++)
            {
                float t = flight * (i / (float)(Samples - 1));
                Vector3 point = from + velocity * t + 0.5f * gravity * (t * t);
                _line.SetPosition(i, point);
            }

            _reticle.SetActive(true);
            _reticle.transform.position = target + Vector3.up * 0.05f;
        }

        public void Hide()
        {
            if (_line != null) _line.enabled = false;
            if (_reticle != null) _reticle.SetActive(false);
        }

        private static float TimeToLand(float fromY, float targetY, float velocityY, float gravityY)
        {
            // Solve fromY + vY*t + 0.5*gY*t^2 = targetY for the later positive root.
            float a = 0.5f * gravityY;
            float b = velocityY;
            float c = fromY - targetY;

            float disc = b * b - 4f * a * c;
            if (disc <= 0f || Mathf.Abs(a) < 0.0001f) return 0.8f;

            float root = Mathf.Sqrt(disc);
            float t = Mathf.Max((-b + root) / (2f * a), (-b - root) / (2f * a));
            return t > 0.05f ? t : 0.8f;
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Sprites/Default");
            _material = new Material(shader);
            var gold = new Color(0.88f, 0.75f, 0.35f, 0.9f);
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", gold);
            if (_material.HasProperty("_Color")) _material.SetColor("_Color", gold);

            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.widthMultiplier = 0.16f;
            _line.numCapVertices = 2;
            _line.sharedMaterial = _material;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.enabled = false;

            _reticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _reticle.name = "AimReticle";
            _reticle.transform.SetParent(transform, false);
            _reticle.transform.localScale = new Vector3(1.4f, 0.02f, 1.4f);

            Collider collider = _reticle.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            if (_reticle.TryGetComponent(out Renderer renderer)) renderer.sharedMaterial = _material;
            _reticle.SetActive(false);
        }
    }
}
