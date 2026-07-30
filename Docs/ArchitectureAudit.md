# Century — Architecture Audit (2026-07-30, branch `refactor`)

A full-codebase review for structure, efficiency and readability. 126 source files, ~20.7k lines.

## Verdict

The architecture is holding up well under growth. The foundations the project set out with are
still intact and doing their job:

- **Assembly boundaries are clean.** Battle references Core only — zero `using Century.Campaign`
  anywhere in the Battle assembly. Everything crossing the boundary goes through
  `Core.Contracts` (`BattleRequest` in, `BattleResult` out), exactly as designed.
- **No LINQ, no per-frame allocation patterns, no scene scans in hot paths.**
  `FindObjectOfType` appears only in bootstraps. Iteration is indexed `for` loops throughout.
- **The layering discipline held:** sim writes intent, views execute it, one mutation point per
  concern (e.g. `BattleResultApplier` is still the only writer of post-battle roster state).
- **Code-authored catalogs** (items, skills, POIs, crafting, stations) stayed one-line-per-entry
  and designer-editable.

The debt found was accretion, not rot: copy-pasted plumbing, tuning fields orphaned by later
iterations, and a few hot paths that grew O(everything) callers over time.

## Changes made in this pass

### Duplication
- **`Core/World/FxMaterials.cs` (new)** — the shader-fallback dance (`Shader.Find` URP →
  legacy → sprites) existed in **fourteen files**, with a subtle correctness split hiding in it:
  URP Unlit renders material colour but ignores vertex colour, while Sprites/Default honours
  vertex colour + alpha (required by every fading LineRenderer/Trail). The helper now encodes
  that distinction (`Unlit()` vs `VertexTinted()`) and all fourteen sites use it.

### Dead code
- **Ten dead tuning fields removed from `BattleSettings`** — all only ever *defined*, never read,
  each orphaned by a later combat iteration: `DefaultDeploymentCount`, `TargetSearchRange`,
  `MeleeTickSeconds`, `AttackIntervalSeconds`, `BaseHitDamage`, `BaseHitChance`,
  `ShieldBlockStaminaCost`, `ShieldBlockArcDot`, `ShieldBlockChanceScale`, `StaminaPerAttack`.
  A designer tuning the asset was being shown ten knobs connected to nothing.
- **Dead extensions removed**: `FormationType.FrontalProtection()` and `FormationType.Next()`.
- **`TutorialInfo/` deleted** — Unity template cruft (readme scripts, layout, icons).

### Efficiency
- **`BattleSquad.CentreOfMass()` is now frame-memoised.** Eight systems ask for it (targeting,
  morale, squad AI, anchors, labels, missiles, ground fx, hover panel), several per squad per
  frame; it re-walked the member list every call. One walk per squad per frame now.
- **Shields-up proximity is squad-level.** The "raise your board, a warband is coming" check ran
  per idle man per frame against every enemy squad. It is now one squad-vs-squad sweep per melee
  tick writing `BattleSquad.HostileNearby`, read as a flag by every man.

## Deliberately left alone (with reasons)

- **The three big HUD controllers** (`CampHudController` 1.2k lines, `BattleHudController` ~1k,
  `OvermapHudController` ~0.8k). They are large but cohesive — element caching, refresh, and
  event wiring for one document each, with no logic that belongs elsewhere. Splitting them would
  scatter one screen's behaviour across files to satisfy a line count. Revisit only if a screen
  gains a second document.
- **The six near-miss "find nearest X" scans** (`AcquireTarget`, `FindVictim`, `NearestTarget`,
  `FindNearestPlayerPosition`, `TryNearestOpponent`, morale's threat scan). They look like one
  function but differ in real semantics: leash-from-slot vs radius-from-point, include-the-
  Centurion or not, off-field filters, tie-breaking. A forced unification would need a parameter
  object more complex than the six loops it replaces. At current scale (~120 combatants) they are
  also nowhere near the profile.
- **`Assets/Scenes/SampleScene.unity`** — Unity template scene, still index 0 in Build Settings.
  Removing a build-settings scene is a designer-visible change: flagged for the designer to
  delete from Build Settings + disk when convenient (Boot should be index 0).
- **View classes writing sim fields** (`SoldierView` burns sprint stamina, views write
  `WorldPosition`). This is the project's established position-authority pattern, documented in
  the class remarks; consistent, and changing it buys nothing.

## Watch list (fine today, revisit at scale)

- `MeleeCombat.AcquireTarget` is O(men × opponents) per frame (~14k distance ops at full battle).
  Fine at a century's scale; needs spatial bucketing before cohort-scale battles.
- Runtime `Material` instances from code-gen views are never explicitly destroyed; scene unload
  collects them eventually. Only worth plumbing if battles are ever restarted many times within
  one session (the F5 test loop reloads the scene, which is fine).
- `BattleSettings.asset` only serializes fields up to `SquadFrontage`; everything later runs on
  code defaults until the asset is next re-saved in the Inspector — at which point code-default
  changes stop applying. Intentional for now (designer will claim the asset when tuning), but
  worth remembering when a "why didn't my default change take?" bug appears.
