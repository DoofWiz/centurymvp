using Century.Campaign.Model;
using Century.Campaign.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace Century.Campaign.View
{
    /// <summary>
    /// Fades a warband's marker with the column's line of sight: invisible beyond it, ghosting into
    /// view as it enters, fully opaque once well inside. Attached at spawn to every non-player
    /// <see cref="PartyView"/> by <see cref="OvermapWorld"/>; the player's own column never fades.
    /// </summary>
    public sealed class PartyVisibility : MonoBehaviour
    {
        private CampaignState _state;
        private PartyState _party;
        private CampaignEventLog _log;
        private Renderer[] _renderers;
        private Material[] _materials;
        private Color[] _baseColours;

        /// <summary>Set once this warband has been reported in the event log, so a contact that drifts
        /// along the edge of sight is announced once, not every time it flickers.</summary>
        private bool _sightingLogged;

        /// <summary>Last applied visibility, read by the hover tooltip to decide what it may reveal.</summary>
        public float Visibility { get; private set; } = 1f;

        public void Bind(CampaignState state, PartyState party, CampaignEventLog log)
        {
            _state = state;
            _party = party;
            _log = log;
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _materials = new Material[_renderers.Length];
            _baseColours = new Color[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++)
            {
                // Instance the material so one ghosting warband does not ghost its whole faction.
                Material material = _renderers[i].material;
                MakeTransparentCapable(material);
                _materials[i] = material;
                _baseColours[i] = BaseColour(material);
            }
        }

        private void LateUpdate()
        {
            if (_state == null || _renderers == null) return;

            // Party-aware: a stealthed stalker fades in far later than an open marcher.
            Visibility = _party != null
                ? LineOfSight.Visibility01(_state, _party)
                : LineOfSight.Visibility01(_state, transform.position);
            bool anySight = Visibility > 0.02f;

            // First clear look at this band goes into the event log — the scouts call it out.
            if (!_sightingLogged && Visibility > 0.35f && _party != null && _log != null)
            {
                _sightingLogged = true;
                bool hostile = _state.PlayerParty != null && PartyRelations.IsHostile(_state.PlayerParty, _party);
                _log.Push(
                    hostile ? CampaignEventKind.Threat : CampaignEventKind.Discovery,
                    $"Sighted: {_party.DisplayName}",
                    hostile ? "A hostile warband on the move" : "A party on the move",
                    _state.Clock.Now.DayNumber);
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].enabled = anySight;
                if (anySight) SetAlpha(_materials[i], _baseColours[i], Visibility);
            }
        }

        // --- Shared material plumbing (also used by the POI markers) ------------------------------

        /// <summary>Reconfigures a URP Lit/Unlit material instance to render transparent.</summary>
        public static void MakeTransparentCapable(Material material)
        {
            if (material == null) return;

            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);

            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        public static Color BaseColour(Material material)
        {
            if (material == null) return Color.white;
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color")) return material.GetColor("_Color");
            return Color.white;
        }

        public static void SetAlpha(Material material, Color baseColour, float alpha01)
        {
            if (material == null) return;

            Color colour = baseColour;
            colour.a = baseColour.a * Mathf.Clamp01(alpha01);

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
        }
    }
}
