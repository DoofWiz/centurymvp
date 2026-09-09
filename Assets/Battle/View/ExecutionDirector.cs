using System.Collections;
using System.Collections.Generic;
using Century.Battle.Model;
using Century.Battle.Sim;
using UnityEngine;

namespace Century.Battle.View
{
    /// <summary>
    /// The killing blow as a scene. When the Centurion's melee strike would kill, MeleeCombat offers
    /// the kill here first: the victim is held at death's door, both men are stepped into position,
    /// and a short execution plays out — the enemy seized, the blade driven through him and torn
    /// back out — before the death actually lands. One execution exists for now; the phase timeline
    /// is the system, so further animations are new cases of the same beats.
    /// </summary>
    /// <remarks>
    /// The sim keeps running around the scene: only the two participants are frozen (and untouchable
    /// — an execution the enemy could interrupt would teach the player never to use his own killing
    /// blows). The kill lands through <see cref="MeleeCombat.ResolveExecution"/>, so morale, kill
    /// counts and the corpse's fall all run through the ordinary death path, one second late.
    /// </remarks>
    public sealed class ExecutionDirector : MonoBehaviour
    {
        private BattleState _state;
        private BattleSettings _settings;
        private PlayerCharacterController _player;
        private List<SoldierView> _soldiers;
        private MeleeCombat _combat;

        private bool _running;

        public void Initialise(
            BattleState state, BattleSettings settings, PlayerCharacterController player,
            List<SoldierView> soldiers, MeleeCombat combat)
        {
            _state = state;
            _settings = settings;
            _player = player;
            _soldiers = soldiers;
            _combat = combat;

            combat.ExecutionOffer = Offer;
        }

        /// <summary>MeleeCombat's offer of a killing blow. Refusing hands the kill back to the
        /// ordinary death path, so every gate here is safe.</summary>
        private bool Offer(BattleCombatant executioner, BattleCombatant victim)
        {
            if (_running || _player == null || !_player.IsControlEnabled || _player.IsExecuting) return false;
            if (_state == null || _state.Phase != BattlePhase.Fighting) return false;

            SoldierView view = FindView(victim);
            if (view == null || view.Rig == null) return false;

            // The blow reached him, so he is close — but never teleport across a visible gap.
            Vector3 gap = victim.WorldPosition - executioner.WorldPosition;
            gap.y = 0f;
            if (gap.sqrMagnitude > 12f) return false;

            _running = true;
            StartCoroutine(Run(executioner, victim, view));
            return true;
        }

        private IEnumerator Run(BattleCombatant executioner, BattleCombatant victim, SoldierView view)
        {
            _player.BeginExecution();

            Transform playerBody = _player.BodyRoot;
            float standOffset = _player.transform.position.y
                                - BattleTerrainBuilder.GroundHeight(_player.transform.position);

            // --- Step in: the two men squared to each other, a blade's length apart -------------
            Vector3 face = victim.WorldPosition - _player.transform.position;
            face.y = 0f;
            face = face.sqrMagnitude > 0.001f ? face.normalized : playerBody.forward;
            Quaternion playerLook = Quaternion.LookRotation(face, Vector3.up);
            Quaternion victimLook = Quaternion.LookRotation(-face, Vector3.up);

            Vector3 startPos = _player.transform.position;
            Vector3 endPos = victim.WorldPosition - face * 0.78f;
            Quaternion playerFrom = playerBody.rotation;
            Quaternion victimFrom = view.BodyRoot.rotation;

            for (float t = 0f; t < 0.22f; t += Time.deltaTime)
            {
                if (Interrupted(executioner, victim, view)) { Finish(executioner, victim, face); yield break; }

                float k = Mathf.SmoothStep(0f, 1f, t / 0.22f);
                Vector3 at = Vector3.Lerp(startPos, endPos, k);
                at.y = BattleTerrainBuilder.GroundHeight(at) + standOffset;
                _player.transform.position = at;
                playerBody.rotation = Quaternion.Slerp(playerFrom, playerLook, k);
                view.BodyRoot.rotation = Quaternion.Slerp(victimFrom, victimLook, k);
                SyncModels(executioner, victim, view, face);

                PosePlayer(0, k * 0.5f);
                PoseVictim(victim, view, twist: 0f, lean: -4f * k);
                yield return null;
            }

            // --- The seize: shield boss into his chest, blade drawn back ------------------------
            yield return Beat(0.32f, executioner, victim, view, face, (p, dt) =>
            {
                PosePlayer(0, p);
                PoseVictim(victim, view, twist: Mathf.Sin(p * 14f) * 4f, lean: -8f * p);
            });
            if (!_running) yield break;

            // --- The plunge ---------------------------------------------------------------------
            bool bled = false;
            yield return Beat(0.22f, executioner, victim, view, face, (p, dt) =>
            {
                PosePlayer(1, p);
                PoseVictim(victim, view, twist: 6f, lean: Mathf.Lerp(-8f, -18f, p));
                if (p > 0.55f && !bled)
                {
                    bled = true;
                    view.Rig.NotifyHit();
                    HitEffects.Spawn(victim.WorldPosition + Vector3.up * 1.15f);
                }
            });
            if (!_running) yield break;

            // --- Held in: the beat the whole field can read -------------------------------------
            yield return Beat(0.3f, executioner, victim, view, face, (p, dt) =>
            {
                PosePlayer(2, p);
                PoseVictim(victim, view, twist: 6f - 10f * p, lean: Mathf.Lerp(-18f, 10f, p));
            });
            if (!_running) yield break;

            // --- The yank: torn out, violently --------------------------------------------------
            bool torn = false;
            yield return Beat(0.26f, executioner, victim, view, face, (p, dt) =>
            {
                PosePlayer(3, p);
                PoseVictim(victim, view, twist: -14f * p, lean: Mathf.Lerp(10f, -20f, p));
                if (p > 0.35f && !torn)
                {
                    torn = true;
                    HitEffects.SpawnDeath(victim.WorldPosition + Vector3.up * 1.1f);
                }
            });
            if (!_running) yield break;

            // The death lands, and the corpse falls away from the blade like any other.
            Finish(executioner, victim, face);
        }

        /// <summary>One phase of the scene: runs <paramref name="pose"/> with phase progress until
        /// the beat's seconds are spent, bailing out (and finishing the kill) on any interruption.</summary>
        private IEnumerator Beat(
            float seconds, BattleCombatant executioner, BattleCombatant victim, SoldierView view,
            Vector3 face, System.Action<float, float> pose)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (Interrupted(executioner, victim, view))
                {
                    Finish(executioner, victim, face);
                    yield break;
                }

                SyncModels(executioner, victim, view, face);
                pose(Mathf.Clamp01(t / seconds), Time.deltaTime);
                yield return null;
            }
        }

        private bool Interrupted(BattleCombatant executioner, BattleCombatant victim, SoldierView view)
            => !_running || executioner == null || !executioner.IsAlive
               || victim == null || view == null || view.Rig == null
               || _state.Phase == BattlePhase.Aftermath;

        /// <summary>Ends the scene however it got here: the held man dies, both are released.</summary>
        private void Finish(BattleCombatant executioner, BattleCombatant victim, Vector3 face)
        {
            if (victim != null) victim.LastHitDirection = face;   // the corpse falls off the blade
            _combat.ResolveExecution(executioner, victim);
            _player.EndExecution();
            _running = false;
        }

        private void SyncModels(
            BattleCombatant executioner, BattleCombatant victim, SoldierView view, Vector3 face)
        {
            executioner.WorldPosition = _player.transform.position;
            executioner.Facing = face;
            if (view != null) victim.WorldPosition = view.transform.position;
            victim.Facing = -face;
        }

        private void PosePlayer(int phase, float t01)
        {
            if (_player.Rig == null || _player.Gear == null) return;

            _player.Rig.Animate(0f, Time.deltaTime);

            // The victim's chest in the player's body-root frame, where the gear is posed.
            Vector3 chestWorld = _player.transform.position
                                 + _player.BodyRoot.forward * 0.78f + Vector3.up * 1.15f;
            Vector3 chestLocal = _player.BodyRoot.InverseTransformPoint(chestWorld);

            _player.Gear.PoseExecution(phase, t01, Time.deltaTime, _player.Rig, chestLocal);
        }

        private void PoseVictim(BattleCombatant victim, SoldierView view, float twist, float lean)
        {
            if (view == null || view.Rig == null) return;

            view.Rig.Animate(0f, Time.deltaTime);
            view.Gear?.Pose(victim, Time.deltaTime, view.Rig);
            view.Rig.SetCombatPose(twist, lean);   // after Gear.Pose, so the scene's body wins
        }

        private SoldierView FindView(BattleCombatant combatant)
        {
            if (_soldiers == null) return null;
            for (int i = 0; i < _soldiers.Count; i++)
                if (_soldiers[i] != null && _soldiers[i].Combatant == combatant) return _soldiers[i];
            return null;
        }
    }
}
