using Century.Battle.Model;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// The commander's ground reticle: a ring that rides the cursor at all times, recoloured by what
    /// it is over, with a short line of text naming it — a friendly squad shows its name and current
    /// order (and that Tab selects it); an enemy squad shows its name and remaining strength. This is
    /// the player's aiming and selection instrument, so it is created in code and needs no scene setup.
    /// </summary>
    public sealed class BattleReticle : MonoBehaviour
    {
        private const int Segments = 40;

        private static readonly Color NeutralColour = new Color(0.85f, 0.8f, 0.68f, 0.75f);
        private static readonly Color FriendlyColour = new Color(0.94f, 0.79f, 0.41f, 1f);
        private static readonly Color SelectedColour = new Color(1f, 0.92f, 0.6f, 1f);
        private static readonly Color EnemyColour = new Color(0.85f, 0.3f, 0.24f, 1f);

        private BattleCameraRig _cameraRig;
        private SquadCommandInput _commands;
        private PlayerCharacterController _player;

        private LineRenderer _ring;
        private TextMesh _label;
        private Transform _labelRoot;
        private Camera _camera;

        public void Initialise(
            BattleCameraRig cameraRig, SquadCommandInput commands, PlayerCharacterController player)
        {
            _cameraRig = cameraRig;
            _commands = commands;
            _player = player;
            _camera = Camera.main;

            BuildRing();
            BuildLabel();
        }

        private void BuildRing()
        {
            _ring = gameObject.AddComponent<LineRenderer>();
            _ring.loop = true;
            _ring.useWorldSpace = false;
            _ring.positionCount = Segments;
            _ring.startWidth = 0.09f;
            _ring.endWidth = 0.09f;
            _ring.material = new Material(
                Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default"));

            const float radius = 0.9f;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i / (float)Segments * Mathf.PI * 2f;
                _ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.05f, Mathf.Sin(angle) * radius));
            }
        }

        private void BuildLabel()
        {
            _labelRoot = new GameObject("ReticleLabel").transform;
            _labelRoot.SetParent(transform, false);
            _labelRoot.localPosition = new Vector3(0f, 1.6f, 0f);

            _label = _labelRoot.gameObject.AddComponent<TextMesh>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.GetComponent<MeshRenderer>().material = _label.font.material;
            _label.fontSize = 44;
            _label.characterSize = 0.055f;
            _label.anchor = TextAnchor.LowerCenter;
            _label.alignment = TextAlignment.Center;
        }

        private void LateUpdate()
        {
            if (_cameraRig == null || _commands == null) return;

            // The pilum aim arc has its own reticle; two rings under one cursor read as a glitch.
            Vector3 point = Vector3.zero;
            bool visible = _commands.IsEnabled && (_player == null || !_player.IsAiming);
            if (visible) visible = _cameraRig.TryGetCursorGroundPoint(out point);

            _ring.enabled = visible;
            _label.gameObject.SetActive(visible);
            if (!visible) return;

            transform.position = point;
            UpdateAppearance();

            // The label always faces the camera squarely, like a map annotation.
            if (_camera != null) _labelRoot.rotation = _camera.transform.rotation;
        }

        private void UpdateAppearance()
        {
            int hovered = _commands.HoveredSquadIndex;

            if (hovered >= 0)
            {
                BattleSquad squad = _commands.SquadViews[hovered].Squad;
                bool selected = _commands.IsSelected(hovered);

                Colour(selected ? SelectedColour : FriendlyColour);
                _label.text = squad.IsRouted
                    ? $"{ShortName(squad)} · ROUTED"
                    : $"{ShortName(squad)} · {squad.Order.ToString().ToUpperInvariant()}\n" +
                      (selected ? "TAB · DESELECT" : "TAB · SELECT");
                return;
            }

            BattleSquad enemy = _commands.HoveredEnemySquad;
            if (enemy != null)
            {
                Colour(EnemyColour);
                _label.text = enemy.IsRouted
                    ? $"{enemy.DisplayName.ToUpperInvariant()} · ROUTED"
                    : $"{enemy.DisplayName.ToUpperInvariant()} · {enemy.AliveCount} MEN";
                return;
            }

            Colour(NeutralColour);
            _label.text = string.Empty;
        }

        private void Colour(Color colour)
        {
            _ring.startColor = colour;
            _ring.endColor = colour;

            Material material = _ring.material;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);

            _label.color = colour;
        }

        /// <summary>"Contubernium III" reads as "III" at reticle size.</summary>
        private static string ShortName(BattleSquad squad)
        {
            string name = squad.DisplayName ?? "";
            int cut = name.LastIndexOf(' ');
            return (cut >= 0 && cut < name.Length - 1 ? name.Substring(cut + 1) : name).ToUpperInvariant();
        }
    }
}
