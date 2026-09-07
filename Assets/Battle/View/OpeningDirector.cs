using System;
using System.Collections;
using System.Collections.Generic;
using Century.Battle.Model;
using Century.Battle.Sim;
using Century.Core.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Battle.View
{
    /// <summary>
    /// THE OPENING SEQUENCE. One wounded Centurion wakes on a rainy forest floor after the massacre:
    /// blur to focus, a blink, two lines of thought, then three tutorial beats in the brief's order
    /// (move, stealth, fight) with time frozen until the player does the thing each one asks. The
    /// battle scene runs its ordinary simulation underneath; this director owns the beats, the
    /// barbarians' awareness, the corridor the player walks and the way out.
    /// </summary>
    /// <remarks>
    /// Everything is in battle-local metres around the field centre. The camera's yaw is zero, so
    /// +X is screen-right: "to their right" in the brief is +X here. The ground is dressed as a
    /// corridor of trees running east, with the warband's clearing bulging off its north side, so
    /// the way forward is the only open way; a floating chevron points along it besides.
    ///
    /// The warband cannot see the player at all until the stealth beat has been taught: the beat
    /// fires the moment they come into view, well outside their (deliberately short) sight, so a
    /// new player is never chased before being told how to hide.
    /// </remarks>
    public sealed class OpeningDirector : MonoBehaviour
    {
        /// <summary>Everything the bootstrap hands over. A struct, so the call site reads as a list.</summary>
        public struct Context
        {
            public BattleState State;
            public BattleSettings Settings;
            public PlayerCharacterController Player;
            public BattleCameraRig Camera;
            public VisualElement HudRoot;
            public Transform SpawnRoot;
            public TerrainDecorProfile Decor;
            public BattleTimeControls Time;
            public Action<bool> SetWorldInput;
            public Action Finished;
            public Action PlayerDied;
        }

        // --- The stage ------------------------------------------------------------------------

        private static readonly Vector3 PlayerStart = new Vector3(-30f, 0f, -6f);
        private static readonly Vector3 Checkpoint1 = new Vector3(-8f, 0f, -6f);
        private static readonly Vector3 WarriorsAnchor = new Vector3(12f, 0f, 4f);
        private static readonly Vector3 Checkpoint2 = new Vector3(26f, 0f, -6f);
        private static readonly Vector3 LooterAnchor = new Vector3(38f, 0f, -5f);

        /// <summary>The looter hears the Centurion inside this: he turns and comes.</summary>
        private const float LooterHearingRange = 12f;

        /// <summary>Where the legionary walks on from, and where he leads the Centurion out.</summary>
        private static readonly Vector3 ExtraEntry = new Vector3(51f, 0f, -5f);
        private static readonly Vector3 ExitPoint = new Vector3(66f, 0f, -6f);

        private const float CorridorZ = -6f;
        private const float CheckpointRadius = 3.2f;

        /// <summary>The warband comes into view at this range: the stealth beat fires here.</summary>
        private const float RevealRange = 24f;

        /// <summary>The warband's sight, cut short for this group: a man on his feet is marked at
        /// eleven metres, a crouching one only inside four and a half.</summary>
        private const float SpotRangeOpen = 11f;
        private const float SpotRangeStealthed = 4.5f;

        private const float CloseCameraDistance = 11f;

        /// <summary>
        /// Places the actors before any view exists: the Centurion where he fell, the passing
        /// warband in its clearing north of the road, the looter beyond the second checkpoint. Enemy
        /// squads are scripted (the AI leaves them alone) and unaware (they do not see the player)
        /// until the sequence says otherwise; nobody carries pila.
        /// </summary>
        public static void Layout(BattleState state, BattleSettings settings)
        {
            if (state.PlayerCharacter != null)
            {
                state.PlayerCharacter.WorldPosition = PlayerStart;
                state.PlayerCharacter.Facing = Vector3.right;
                state.PlayerCharacter.PilaRemaining = 0;
            }

            for (int i = 0; i < state.EnemySquads.Count; i++)
            {
                BattleSquad squad = state.EnemySquads[i];
                bool warriors = i == 0;

                squad.Scripted = true;
                squad.Unaware = true;
                squad.Fearless = !warriors;   // the looter never runs: he is the fight
                squad.IsOffField = false;
                squad.Order = SquadOrder.HoldPosition;
                squad.Formation = FormationType.Loose;
                // The warband faces away up its clearing; the looter has his back to the road,
                // bent over the bodies he is stripping.
                squad.AnchorPosition = warriors ? WarriorsAnchor : LooterAnchor;
                squad.AnchorFacing = warriors ? Vector3.forward : Vector3.right;
                squad.OrderedPosition = squad.AnchorPosition;

                for (int m = 0; m < squad.Members.Count; m++)
                {
                    BattleCombatant man = squad.Members[m];
                    man.WorldPosition = SquadFormationSolver.GetWorldSlot(squad, m, settings);
                    man.Facing = squad.AnchorFacing;
                    man.PilaRemaining = 0;
                }
            }
        }

        // --- State ----------------------------------------------------------------------------

        private Context _ctx;
        private BattleSquad _warriors, _looter;
        private bool _stealthTaught;
        private bool _warriorsAware;
        private bool _warriorsGone;
        private bool _finished, _died;
        private bool _cutscene;
        private float _tutorialHideAt = float.MaxValue;
        private Transform _lookTarget;
        private OpeningGuideArrow _arrow;
        private OpeningExtra _extra;
        private Coroutine _run;

        private VisualElement _subtitle, _tutorial, _eyelidTop, _eyelidBottom, _stealthTag, _veil;
        private Label _subtitleText, _tutorialTitle, _tutorialKeys, _tutorialBody, _tutorialHint;

        public void Initialise(Context context)
        {
            _ctx = context;

            List<BattleSquad> enemies = _ctx.State.EnemySquads;
            _warriors = enemies.Count > 0 ? enemies[0] : null;
            _looter = enemies.Count > 1 ? enemies[1] : null;

            CacheHud(_ctx.HudRoot);
            DressTheField();

            _lookTarget = new GameObject("OpeningLookTarget").transform;
            _lookTarget.SetParent(transform, false);

            Transform follow = _ctx.Player != null ? _ctx.Player.transform : null;
            RainEffect.Create(transform, follow);
            _arrow = OpeningGuideArrow.Create(transform, follow);
            OpeningPostFx fx = OpeningPostFx.Create(transform);

            _run = StartCoroutine(Run(fx));
        }

        private void CacheHud(VisualElement root)
        {
            if (root == null) return;

            _subtitle = root.Q<VisualElement>("opening-subtitle");
            _subtitleText = root.Q<Label>("opening-subtitle-text");
            _tutorial = root.Q<VisualElement>("opening-tutorial");
            _tutorialTitle = root.Q<Label>("opening-tutorial-title");
            _tutorialKeys = root.Q<Label>("opening-tutorial-keys");
            _tutorialBody = root.Q<Label>("opening-tutorial-body");
            _tutorialHint = root.Q<Label>("opening-tutorial-hint");
            _eyelidTop = root.Q<VisualElement>("opening-eyelid-top");
            _eyelidBottom = root.Q<VisualElement>("opening-eyelid-bottom");
            _stealthTag = root.Q<VisualElement>("opening-stealth");
            _veil = root.Q<VisualElement>("opening-veil");

            if (_subtitle != null) _subtitle.style.opacity = 0f;
            if (_tutorial != null) _tutorial.style.display = DisplayStyle.None;
            if (_tutorialKeys != null) _tutorialKeys.style.display = DisplayStyle.None;
            if (_tutorialHint != null) _tutorialHint.style.display = DisplayStyle.None;
            if (_stealthTag != null) _stealthTag.style.display = DisplayStyle.None;
            if (_veil != null)
            {
                _veil.style.display = DisplayStyle.None;
                _veil.style.opacity = 0f;
            }
        }

        // --- The sequence ---------------------------------------------------------------------

        private IEnumerator Run(OpeningPostFx fx)
        {
            PlayerCharacterController player = _ctx.Player;

            // Waking: flat on the ground, the world a blur, the eye opening twice before it holds.
            _cutscene = true;
            _ctx.SetWorldInput?.Invoke(false);
            if (player != null)
            {
                player.StealthAllowed = false;
                player.PosePitch = 82f;
            }
            _ctx.Camera?.SetDistance(CloseCameraDistance, snap: true);

            fx?.BeginBlurToFocus(4.2f);
            yield return WaitRealtime(1.1f);
            yield return Blink(0.55f);
            yield return WaitRealtime(0.7f);
            yield return Blink(0.35f);
            yield return WaitRealtime(0.9f);

            // He pulls himself up.
            yield return Over(1.9f, t =>
            {
                if (player != null) player.PosePitch = Mathf.Lerp(82f, 0f, 1f - Mathf.Pow(1f - t, 3f));
            });
            if (player != null) player.PosePitch = 0f;

            yield return Subtitle("I'm alive... thank Mars.", 2.6f);
            yield return Subtitle("I should search for other survivors...", 2.6f);

            // Tutorial 1: MOVE. Time stands still until a movement key is pressed.
            yield return Tutorial("MOVE:  W  A  S  D", string.Empty,
                () => Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A)
                      || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D));
            _cutscene = false;
            _arrow?.Point(Ground(Checkpoint1));

            // On past the first checkpoint's line, or until the warband comes into view, whichever
            // is first: the stealth beat must land BEFORE they could ever have him.
            yield return new WaitUntil(() => _died || PassedX(Checkpoint1.x) || WarriorsInView());
            if (_died) yield break;

            // The warband: the eye swings to them, and the thought that keeps him alive.
            _cutscene = true;
            _arrow?.Point(null);
            _ctx.SetWorldInput?.Invoke(false);
            if (_warriors != null && _lookTarget != null)
            {
                _lookTarget.position = _warriors.CentreOfMass();
                _ctx.Camera?.SetTarget(_lookTarget, snap: false);
            }
            yield return Subtitle("Barbarians... gods be good, they've slaughtered everyone. I cannot be seen...", 3.6f);
            if (player != null) _ctx.Camera?.SetTarget(player.transform, snap: false);
            yield return WaitRealtime(0.5f);

            // Tutorial 2: STEALTH.
            if (player != null) player.StealthAllowed = true;
            yield return Tutorial("STEALTH:  C", "Crouch: slower, and unseen unless close",
                () => Input.GetKeyDown(KeyCode.C));
            player?.SetStealth(true);
            _stealthTaught = true;
            _cutscene = false;

            // The way on is the man at the end of the road: the arrow points straight at him. He
            // is woken when the Centurion crosses the second checkpoint's line or gets close enough
            // to be heard, whichever comes first (Update sends the warband away once he is past it).
            _arrow?.Point(Ground(LooterAnchor));
            yield return new WaitUntil(() => _died || PassedX(Checkpoint2.x) || LooterHears());
            if (_died) yield break;

            SendWarriorsAway();

            // Tutorial 3: FIGHT. The looter has heard him, turns, and comes on.
            if (_looter != null)
            {
                _looter.Unaware = false;
                _looter.Scripted = false;
                _looter.Order = SquadOrder.Advance;
                _looter.AnchorFacing = Vector3.left;
                _looter.OrderedPosition = player != null ? player.transform.position : PlayerStart;
                _arrow?.Point(_looter.CentreOfMass());
            }
            StartCoroutine(Subtitle("Damn it all! A pilfering barbarian has spotted me... I will NOT die this day.", 3.4f));
            yield return WaitRealtime(0.9f);

            // Whatever the road cost him, he meets the man with his wind back: a shield that breaks
            // on the first blow teaches nothing.
            if (player != null && player.Combatant != null) player.Combatant.Stamina01 = 1f;

            _cutscene = true;
            yield return Tutorial("FIGHT:  LEFT CLICK", "Hold and release: thrust  ·  L-CTRL: shield  ·  SHIFT: sprint",
                () => Input.GetMouseButtonDown(0));
            player?.SetStealth(false);
            _cutscene = false;

            yield return new WaitUntil(() => _died || _looter == null || _looter.IsDestroyed);
            if (_died) yield break;
            _arrow?.Point(null);

            // The way out: one of ours walks on from the east, calls him, and leads him off.
            yield return WaitRealtime(1.0f);
            _cutscene = true;
            _ctx.SetWorldInput?.Invoke(false);
            yield return Cinematic(player);

            _finished = true;
            _ctx.Finished?.Invoke();
        }

        /// <summary>
        /// A legionary comes down the road, stops a few paces short, and calls the Centurion on;
        /// the Centurion walks to him, and when he is halfway there the legionary turns and leads
        /// the way out. The screen goes dark on the two of them leaving.
        /// </summary>
        private IEnumerator Cinematic(PlayerCharacterController player)
        {
            if (player == null) yield break;

            Vector3 here = player.transform.position;
            Vector3 meet = new Vector3(here.x + 5.5f, 0f, here.z);
            if (meet.x > ExtraEntry.x - 4f) meet.x = ExtraEntry.x - 4f;

            _extra = OpeningExtra.Spawn(transform, Ground(ExtraEntry), Vector3.left, roman: true);
            _extra.WalkTo(Ground(meet), 3.2f);

            float timeout = Time.unscaledTime + 12f;
            yield return new WaitUntil(() => _extra.Arrived || Time.unscaledTime > timeout);

            _extra.Face(here - _extra.transform.position);
            yield return Subtitle("Commander, this way, quick! We must leave this place...", 3.2f);

            // The Centurion goes to him.
            Vector3 stopShort = _extra.transform.position + Vector3.left * 1.4f;
            player.ScriptedSpeed = 2.4f;
            player.ScriptedDestination = stopShort;

            float total = Vector3.Distance(here, stopShort);
            timeout = Time.unscaledTime + 10f;
            yield return new WaitUntil(() =>
            {
                Vector3 to = stopShort - player.transform.position;
                to.y = 0f;
                return to.magnitude <= total * 0.5f || Time.unscaledTime > timeout;
            });

            // Halfway: the legionary turns and leads; the Centurion follows him out.
            Vector3 exit = Ground(ExitPoint);
            _extra.WalkTo(exit, 2.6f);
            player.ScriptedSpeed = 2.6f;
            player.ScriptedDestination = exit;

            yield return WaitRealtime(2.4f);
            yield return FadeVeil(1.6f);
            player.ScriptedDestination = null;
        }

        private void Update()
        {
            if (_finished || _died) return;

            PlayerCharacterController player = _ctx.Player;

            // Fell in the forest: the sequence starts again. The brief's tutorial is short enough.
            if (player != null && player.Combatant != null && !player.Combatant.IsAlive)
            {
                _died = true;
                if (_run != null) StopCoroutine(_run);
                StartCoroutine(Death());
                return;
            }

            if (_stealthTag != null && player != null)
                _stealthTag.style.display = player.IsStealthed ? DisplayStyle.Flex : DisplayStyle.None;

            if (_tutorial != null && Time.unscaledTime >= _tutorialHideAt)
            {
                _tutorial.style.display = DisplayStyle.None;
                _tutorialHideAt = float.MaxValue;
            }

            // The looter keeps coming at wherever the Centurion actually is.
            if (_looter != null && !_looter.Scripted && _looter.IsEffective && _arrow != null && player != null)
                _arrow.Point(_looter.CentreOfMass());

            // Snuck past: the warband moves on up its clearing, hunting bigger game.
            if (_stealthTaught && !_cutscene && player != null && player.transform.position.x > WarriorsAnchor.x + 5f)
                SendWarriorsAway();

            TickAwareness(player);
        }

        /// <summary>The warband walks off north, if it never marked him. Once only.</summary>
        private void SendWarriorsAway()
        {
            if (_warriorsGone || _warriors == null || _warriorsAware) return;
            _warriorsGone = true;
            _warriors.Order = SquadOrder.Advance;
            _warriors.OrderedPosition = WarriorsAnchor + Vector3.forward * 110f;
        }

        private bool PassedX(float x) => _ctx.Player != null && _ctx.Player.transform.position.x >= x;

        private bool LooterHears()
        {
            if (_looter == null || _ctx.Player == null) return false;
            Vector3 to = _looter.CentreOfMass() - _ctx.Player.transform.position;
            to.y = 0f;
            return to.sqrMagnitude <= LooterHearingRange * LooterHearingRange;
        }

        /// <summary>
        /// The warband notices a man on his feet at a distance and a crouching man only up close,
        /// and only once the stealth beat has been taught. Once they have him, they have him: the
        /// squad goes to the ordinary AI and charges.
        /// </summary>
        private void TickAwareness(PlayerCharacterController player)
        {
            if (!_stealthTaught || _warriorsAware || _warriorsGone || _cutscene || _warriors == null
                || player == null || !_warriors.IsEffective) return;

            float range = player.IsStealthed ? SpotRangeStealthed : SpotRangeOpen;
            if (NearestWarriorSqr(player.transform.position) > range * range) return;

            _warriorsAware = true;
            _warriors.Unaware = false;
            _warriors.Scripted = false;
            _warriors.Order = SquadOrder.Advance;
            _warriors.OrderedPosition = player.transform.position;
            StartCoroutine(Subtitle("They've seen me!", 1.8f));
        }

        private bool WarriorsInView()
        {
            if (_warriors == null || _ctx.Player == null) return false;
            return NearestWarriorSqr(_ctx.Player.transform.position) <= RevealRange * RevealRange;
        }

        private float NearestWarriorSqr(Vector3 from)
        {
            float best = float.MaxValue;
            for (int i = 0; i < _warriors.Members.Count; i++)
            {
                BattleCombatant man = _warriors.Members[i];
                if (!man.IsAlive) continue;

                Vector3 to = man.WorldPosition - from;
                to.y = 0f;
                if (to.sqrMagnitude < best) best = to.sqrMagnitude;
            }
            return best;
        }

        private IEnumerator Death()
        {
            _ctx.Time?.ReleaseHold();
            _ctx.SetWorldInput?.Invoke(false);
            _arrow?.Point(null);
            if (_tutorial != null) _tutorial.style.display = DisplayStyle.None;

            yield return WaitRealtime(1.4f);
            ShowTutorial("YOU FELL", string.Empty);
            yield return WaitRealtime(2.4f);
            yield return FadeVeil(1.2f);

            _ctx.PlayerDied?.Invoke();
        }

        // --- Beats ----------------------------------------------------------------------------

        /// <summary>A pop-up that freezes the world until the player does what it asks, then lingers
        /// three seconds with time running so the doing and the reading overlap. Controls only: the
        /// line names the action and the keys, and nothing else.</summary>
        private IEnumerator Tutorial(string line, string detail, Func<bool> done)
        {
            ShowTutorial(line, detail);
            _ctx.Time?.Hold();

            yield return null;   // never satisfied by the key that opened the last one
            yield return new WaitUntil(() => _died || done());

            _ctx.Time?.ReleaseHold();
            _ctx.SetWorldInput?.Invoke(true);
            _tutorialHideAt = Time.unscaledTime + 3f;
        }

        private void ShowTutorial(string line, string detail)
        {
            if (_tutorial == null) return;
            if (_tutorialTitle != null) _tutorialTitle.text = line;
            if (_tutorialBody != null)
            {
                _tutorialBody.text = detail ?? string.Empty;
                _tutorialBody.style.display = string.IsNullOrEmpty(detail) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            _tutorial.style.display = DisplayStyle.Flex;
            _tutorialHideAt = float.MaxValue;
        }

        private IEnumerator Subtitle(string text, float hold)
        {
            if (_subtitle == null) yield break;
            if (_subtitleText != null) _subtitleText.text = text;

            yield return Over(0.4f, t => _subtitle.style.opacity = t);
            yield return WaitRealtime(hold);
            yield return Over(0.45f, t => _subtitle.style.opacity = 1f - t);
            _subtitle.style.opacity = 0f;
        }

        /// <summary>The eyelids close from top and bottom and part again.</summary>
        private IEnumerator Blink(float closed01)
        {
            if (_eyelidTop == null || _eyelidBottom == null) yield break;

            yield return Over(0.2f, t => SetEyelids(Mathf.Lerp(0f, closed01, t)));
            yield return WaitRealtime(0.12f);
            yield return Over(0.32f, t => SetEyelids(Mathf.Lerp(closed01, 0f, t)));
            SetEyelids(0f);
        }

        private void SetEyelids(float fraction)
        {
            Length height = Length.Percent(Mathf.Clamp01(fraction) * 50f);
            _eyelidTop.style.height = height;
            _eyelidBottom.style.height = height;
        }

        private IEnumerator FadeVeil(float seconds)
        {
            if (_veil == null) yield break;
            _veil.style.display = DisplayStyle.Flex;
            yield return Over(seconds, t => _veil.style.opacity = t);
            _veil.style.opacity = 1f;
        }

        private bool Near(Vector3 localPoint)
        {
            if (_ctx.Player == null) return false;
            Vector3 to = _ctx.Player.transform.position - localPoint;
            to.y = 0f;
            return to.sqrMagnitude <= CheckpointRadius * CheckpointRadius;
        }

        private IEnumerator WaitUntilNear(Vector3 localPoint)
        {
            yield return new WaitUntil(() => _died || Near(localPoint));
        }

        private static Vector3 Ground(Vector3 flat) => BattleTerrainBuilder.Grounded(flat);

        // --- The field ------------------------------------------------------------------------

        /// <summary>
        /// The massacre and the way through it. Two walls of trees run east either side of the
        /// road, with the warband's clearing bulging off the north wall and the looter's hollow
        /// closing the east end; rocks and shrubs sit at the walls' feet, and the fallen of the
        /// legions lie along the road. The forest is the map: there is one way to go.
        /// </summary>
        private void DressTheField()
        {
            Transform root = _ctx.SpawnRoot != null ? _ctx.SpawnRoot : transform;
            var random = new System.Random(4127);

            // --- The fallen ------------------------------------------------------------------
            var corpses = new GameObject("TheFallen").transform;
            corpses.SetParent(root, false);

            for (int i = 0; i < 26; i++)
            {
                Vector3 p = new Vector3(
                    Mathf.Lerp(-36f, 46f, (float)random.NextDouble()), 0f,
                    CorridorZ + Mathf.Lerp(-8f, 8f, (float)random.NextDouble()));

                if (Vector3.Distance(p, PlayerStart) < 3.5f) continue;
                if (Vector3.Distance(p, LooterAnchor) < 4f) continue;
                if (Vector3.Distance(p, WarriorsAnchor) < 5f) continue;

                PlaceCorpse(corpses, p, i % 4 != 3, (float)random.NextDouble() * 360f, i * 31);
            }

            // The looter's pickings: three of ours heaped in front of him, where he is bent over them.
            PlaceCorpse(corpses, LooterAnchor + new Vector3(1.6f, 0f, 0.3f), true, 100f, 701);
            PlaceCorpse(corpses, LooterAnchor + new Vector3(2.4f, 0f, -1.3f), true, 40f, 733);
            PlaceCorpse(corpses, LooterAnchor + new Vector3(1.1f, 0f, -1.9f), false, 250f, 767);

            DressTheCorridor(root, random);
        }

        private static void PlaceCorpse(Transform parent, Vector3 flat, bool roman, float yaw, int variant)
        {
            var corpse = new GameObject(roman ? "Legionary (fallen)" : "Warrior (fallen)");
            corpse.transform.SetParent(parent, false);
            corpse.transform.position = Ground(flat) + Vector3.up * 0.12f;
            corpse.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            SoldierRig.Build(corpse.transform, 0.95f, roman, OfficerRole.None, false, variant);
        }

        private void DressTheCorridor(Transform root, System.Random random)
        {
            // --- The corridor's walls ---------------------------------------------------------
            var trees = new List<Vector3>();
            var rocks = new List<Vector3>();
            var shrubs = new List<Vector3>();

            float NorthWall(float x) => x > 0f && x < 26f ? CorridorZ + 20f : CorridorZ + 10f;   // the clearing
            const float southWall = CorridorZ - 12f;

            for (float x = -46f; x <= 52f; x += 2.4f)
            {
                float jitter = ((float)random.NextDouble() - 0.5f) * 1.6f;
                float north = NorthWall(x);

                // Two staggered rows a wall deep, so nothing shows through it.
                trees.Add(new Vector3(x + jitter, 0f, north + (float)random.NextDouble() * 1.5f));
                trees.Add(new Vector3(x + 1.2f + jitter, 0f, north + 3f + (float)random.NextDouble() * 2f));
                trees.Add(new Vector3(x + 0.4f + jitter, 0f, north + 6.5f + (float)random.NextDouble() * 2f));

                trees.Add(new Vector3(x - jitter, 0f, southWall - (float)random.NextDouble() * 1.5f));
                trees.Add(new Vector3(x + 1.2f - jitter, 0f, southWall - 3f - (float)random.NextDouble() * 2f));
                trees.Add(new Vector3(x + 0.4f - jitter, 0f, southWall - 6.5f - (float)random.NextDouble() * 2f));

                // Undergrowth and stone at the feet of the walls.
                if (random.NextDouble() < 0.7) shrubs.Add(new Vector3(x + jitter * 2f, 0f, north - 1.5f - (float)random.NextDouble() * 2f));
                if (random.NextDouble() < 0.7) shrubs.Add(new Vector3(x - jitter * 2f, 0f, southWall + 1.5f + (float)random.NextDouble() * 2f));
                if (random.NextDouble() < 0.22) rocks.Add(new Vector3(x + jitter, 0f, north - 2.5f));
                if (random.NextDouble() < 0.22) rocks.Add(new Vector3(x - jitter, 0f, southWall + 2.5f));
            }

            // The ends: a wall behind the Centurion, a wall past the looter's hollow with a gap in
            // it on the line of the road, which is where the legionary comes from and leads to.
            for (float z = southWall - 8f; z <= CorridorZ + 12f; z += 2.4f)
            {
                float jitter = ((float)random.NextDouble() - 0.5f) * 1.6f;
                trees.Add(new Vector3(-44f + jitter, 0f, z));
                trees.Add(new Vector3(-47.5f - jitter, 0f, z + 1.2f));

                bool gap = z > CorridorZ - 4f && z < CorridorZ + 4f;
                if (gap) continue;
                trees.Add(new Vector3(52f + jitter, 0f, z));
                trees.Add(new Vector3(55.5f - jitter, 0f, z + 1.2f));
            }

            // A few stones in the clearing for the warband to loiter among.
            rocks.Add(new Vector3(6f, 0f, CorridorZ + 15f));
            rocks.Add(new Vector3(19f, 0f, CorridorZ + 16f));

            for (int i = 0; i < trees.Count; i++) trees[i] = Ground(trees[i]);
            for (int i = 0; i < rocks.Count; i++) rocks[i] = Ground(rocks[i]);
            for (int i = 0; i < shrubs.Count; i++) shrubs[i] = Ground(shrubs[i]);

            ForestBuilder.BuildForest(root, trees, withColliders: false, _ctx.Decor);
            if (_ctx.Decor != null)
            {
                ForestBuilder.ScatterClutter(root, "CorridorRocks", rocks, _ctx.Decor.Rocks, withColliders: false);
                ForestBuilder.ScatterClutter(root, "CorridorUnderbrush", shrubs, _ctx.Decor.Shrubs, withColliders: false);
            }
        }

        // --- Time helpers (unscaled: the beats must run through the held clock) ----------------

        private static IEnumerator WaitRealtime(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) yield return null;
        }

        private static IEnumerator Over(float seconds, Action<float> apply)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                apply(Mathf.Clamp01(t / seconds));
                yield return null;
            }
            apply(1f);
        }
    }
}
