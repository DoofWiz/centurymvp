using Century.Core.World;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// The opening's way-marker: a flat gold chevron floating over the Centurion's head, turned
    /// toward wherever the sequence wants him next and bobbing so it reads as a marker, not a
    /// piece of the world. Hidden whenever there is nowhere to point.
    /// </summary>
    public sealed class OpeningGuideArrow : MonoBehaviour
    {
        private const float Height = 2.7f;
        private const float Lead = 0.9f;

        private Transform _follow;
        private Transform _body;
        private Vector3? _target;
        private float _time;

        public static OpeningGuideArrow Create(Transform parent, Transform follow)
        {
            var arrow = new GameObject("GuideArrow").AddComponent<OpeningGuideArrow>();
            arrow.transform.SetParent(parent, false);
            arrow._follow = follow;
            arrow.Build();
            return arrow;
        }

        /// <summary>Point at a world position, or null to hide.</summary>
        public void Point(Vector3? worldTarget) => _target = worldTarget;

        private void Build()
        {
            _body = new GameObject("Chevron").transform;
            _body.SetParent(transform, false);

            Material gold = FxMaterials.Unlit(new Color(0.95f, 0.80f, 0.36f));

            // A shaft and two swept-back barbs: the classic marker, lying flat.
            Part(new Vector3(0f, 0f, -0.15f), new Vector3(0.14f, 0.05f, 0.70f), Quaternion.identity, gold);
            Part(new Vector3(-0.22f, 0f, 0.05f), new Vector3(0.12f, 0.05f, 0.55f), Quaternion.Euler(0f, 38f, 0f), gold);
            Part(new Vector3(0.22f, 0f, 0.05f), new Vector3(0.12f, 0.05f, 0.55f), Quaternion.Euler(0f, -38f, 0f), gold);

            _body.gameObject.SetActive(false);
        }

        private void Part(Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = "Part";
            part.transform.SetParent(_body, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void Update()
        {
            bool show = _target.HasValue && _follow != null;
            if (_body.gameObject.activeSelf != show) _body.gameObject.SetActive(show);
            if (!show) return;

            _time += Time.unscaledDeltaTime;

            Vector3 to = _target.Value - _follow.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) to = Vector3.forward;
            Vector3 dir = to.normalized;

            // Over his head, a little ahead along the way, breathing up and down.
            _body.position = _follow.position + Vector3.up * (Height + Mathf.Sin(_time * 3.2f) * 0.12f) + dir * Lead;
            _body.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }
}
