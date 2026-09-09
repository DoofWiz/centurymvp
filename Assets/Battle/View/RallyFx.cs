using System.Collections.Generic;
using Century.Battle.Model;
using Century.Battle.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Century.Battle.View
{
    /// <summary>
    /// Everything the rally burst looks like, in one place: a warm gold vignette breathing at the
    /// screen's edge, a burst of gold over every man of the century the instant the call goes up,
    /// rising motes on each of them for as long as the surge holds, and the camera's dolly zoom
    /// (driven through <see cref="BattleCameraRig.SetRallyLens"/>). Polls the simulation's rally
    /// clock, so no rally code needed to change to be seen — the same pattern as BattleGroundFx.
    /// </summary>
    /// <remarks>
    /// The vignette is a URP volume built in code, the OpeningPostFx pattern. Post-processing is
    /// switched on for the camera only while the vignette is actually visible and restored to its
    /// previous state after, so the ordinary fight pays nothing for the effect.
    /// </remarks>
    public sealed class RallyFx : MonoBehaviour
    {
        private static readonly Color GoldDeep = new Color(0.86f, 0.62f, 0.18f);
        private static readonly Color GoldPale = new Color(1.00f, 0.85f, 0.42f);

        private BattleState _state;
        private BattleSettings _settings;
        private List<SoldierView> _soldiers;
        private PlayerCharacterController _player;
        private BattleCameraRig _cameraRig;

        private ParticleSystem _motes;
        private Volume _volume;
        private Vignette _vignette;
        private UniversalAdditionalCameraData _cameraData;
        private bool _postWasOn;
        private bool _postToggled;

        private bool _wasActive;
        private float _fade;
        private float _moteClock;

        public void Initialise(
            BattleState state, BattleSettings settings, List<SoldierView> soldiers,
            PlayerCharacterController player, BattleCameraRig cameraRig)
        {
            _state = state;
            _settings = settings;
            _soldiers = soldiers;
            _player = player;
            _cameraRig = cameraRig;

            BuildMotes();
            BuildVignette();
        }

        private void Update()
        {
            if (_state == null || _vignette == null) return;

            bool active = _state.RallyActive;
            if (active && !_wasActive) OnBurstBegin();
            _wasActive = active;

            if (_cameraRig != null) _cameraRig.SetRallyLens(active);

            // The vignette breathes in quickly, holds with a slow pulse, and lets go gently.
            float step = Time.unscaledDeltaTime / (active ? 0.35f : 0.8f);
            _fade = Mathf.MoveTowards(_fade, active ? 1f : 0f, step);
            float pulse = active ? 1f + Mathf.Sin(Time.unscaledTime * 2.4f) * 0.1f : 1f;
            _vignette.intensity.Override(0.30f * _fade * pulse);
            SetPostProcessing(_fade > 0.001f);

            if (active) TickMotes();
        }

        /// <summary>The call goes up: a burst of gold over every man it reaches, heaviest on the
        /// Centurion himself.</summary>
        private void OnBurstBegin()
        {
            _moteClock = 0f;

            if (_player != null && _player.Combatant != null && _player.Combatant.IsAlive)
                EmitAt(_player.transform.position + Vector3.up * 1.3f, 16);

            if (_soldiers == null) return;
            for (int i = 0; i < _soldiers.Count; i++)
            {
                SoldierView soldier = _soldiers[i];
                if (soldier == null || soldier.Combatant == null) continue;
                if (!soldier.Combatant.IsAlive || !soldier.Combatant.IsPlayerSide) continue;
                if (soldier.Squad != null && soldier.Squad.IsOffField) continue;

                EmitAt(soldier.transform.position + Vector3.up * 1.2f, 8);
            }
        }

        /// <summary>The surge made visible on the men themselves: a slow rain of rising gold off
        /// every soldier under the buff, for as long as it holds.</summary>
        private void TickMotes()
        {
            _moteClock -= Time.deltaTime;
            if (_moteClock > 0f) return;
            _moteClock = 0.45f;

            if (_player != null && _player.Combatant != null && _player.Combatant.IsAlive)
                EmitAt(_player.transform.position + Vector3.up * 1.1f, 2);

            if (_soldiers == null) return;
            for (int i = 0; i < _soldiers.Count; i++)
            {
                SoldierView soldier = _soldiers[i];
                if (soldier == null || soldier.Combatant == null) continue;
                if (!soldier.Combatant.IsAlive || !soldier.Combatant.IsPlayerSide) continue;
                if (soldier.Squad != null && (soldier.Squad.IsOffField || soldier.Squad.IsRouted)) continue;

                EmitAt(soldier.transform.position + Vector3.up * (0.7f + Random.value * 0.7f), 1);
            }
        }

        private void EmitAt(Vector3 position, int count)
        {
            if (_motes == null) return;
            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                startColor = Color.Lerp(GoldDeep, GoldPale, Random.value)
            };
            _motes.Emit(emit, count);
        }

        // --- Construction ----------------------------------------------------------------------

        private void BuildMotes()
        {
            _motes = gameObject.AddComponent<ParticleSystem>();
            _motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _motes.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.gravityModifier = -0.25f;   // gold RISES; blood falls
            main.maxParticles = 1024;
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = _motes.emission;
            emission.enabled = false;   // bursts come only through Emit()

            ParticleSystem.ShapeModule shape = _motes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.30f;

            ParticleSystem.SizeOverLifetimeModule size = _motes.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // A white-based unlit material, so the per-particle gold arrives untinted.
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default");
            var material = new Material(shader);
            Century.Core.World.FxMaterials.SetColour(material, Color.white);
            renderer.sharedMaterial = material;

            _motes.Play();
        }

        private void BuildVignette()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Rally (runtime)";

            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.Override(0f);
            _vignette.smoothness.Override(0.75f);
            _vignette.color.Override(new Color(0.55f, 0.40f, 0.12f));

            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 90f;   // beneath the opening's own volume, should they ever overlap
            _volume.sharedProfile = profile;
        }

        /// <summary>Turns the camera's post-processing on only while the vignette shows, and puts
        /// it back the way it was found afterwards.</summary>
        private void SetPostProcessing(bool needed)
        {
            if (_cameraData == null)
            {
                Camera camera = Camera.main;
                if (camera == null) return;
                _cameraData = camera.GetUniversalAdditionalCameraData();
                if (_cameraData == null) return;
            }

            if (needed && !_postToggled)
            {
                _postWasOn = _cameraData.renderPostProcessing;
                _cameraData.renderPostProcessing = true;
                _postToggled = true;
            }
            else if (!needed && _postToggled)
            {
                _cameraData.renderPostProcessing = _postWasOn;
                _postToggled = false;
            }
        }

        private void OnDestroy()
        {
            if (_volume != null && _volume.sharedProfile != null) Destroy(_volume.sharedProfile);
        }
    }
}
