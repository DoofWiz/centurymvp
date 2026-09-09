# CENTURY — "Marble & Iron" merged style spec (iteration on style 02)

The designer picked **Imperial Marble** (style 02) as the winner but dislikes its Palatino/serif
typography, and wants **Iron & Blood's** (style 04) text style grafted in. This spec is the single
source of truth for the 5 screens in this folder. Follow it exactly so the screens read as one game.

## Base (unchanged from Imperial Marble — copy these verbatim from `../02-imperial-marble.html`)

Palette tokens:
```css
--marble-hi:   #e4e1da;   --marble:      #d9d6cf;   --marble-lo:   #b9b5ab;
--marble-deep: #a6a297;   --groove:      #8f8b80;
--slate:       #3b3a36;   --slate-dim:   #6b6960;   --slate-faint: #8d8a80;
--purple:      #5c2a4d;   --purple-soft: rgba(92,42,77,.28);
--bronze:      #a8842c;   --bronze-deep: #7c6120;
--oxblood:     #7e2a20;   --laurel:      #6d7a5c;   --sienna:      #8c531d;
--engrave: 0 1px 0 rgba(255,255,255,.6);
```

Roles: **purple** = selection/emphasis/active · **bronze** = command/keys/our forces ·
**oxblood** = danger/enemy/health · **laurel** = morale/good · **sienna** = wounded/warning.
Never pure white or pure black.

Panel chrome: the `.slab` recipe from 02 (veined marble gradient fills, light top/left + dark
bottom/right bevel borders, engraved inner frame via `::before`, soft drop shadow). Bars are
recessed stone grooves with inlaid colour fills. Keycaps are carved marble tablets. Active
buttons press IN (inset shadow + translateY, purple border). Greek-key meander data-URI as
heading underlines; laurel-branch SVG pairs as flourishes on major titles.

## Typography (NEW — this is the whole point of the iteration)

```css
--font-head: Impact, "Arial Narrow", "Franklin Gothic Medium", sans-serif;  /* from Iron & Blood */
--font-body: "Segoe UI", "Helvetica Neue", Arial, sans-serif;                /* from Iron & Blood */
```

- **All headings, titles, numerals/values, unit names, command labels, eyebrows, SVG map labels**:
  `--font-head`, `font-weight: 400` (Impact is already heavy — never bold it), UPPERCASE,
  wide tracking (heads 3–4px, labels 1.5–2.5px, big display titles 5–6px).
- **Treatment stays CARVED, not glowing**: colour `--slate` (or role colour) with
  `text-shadow: var(--engrave)`. NO dark glows, NO neon — that was Iron & Blood's mood, we take
  only its letterforms. Think modern monumental signage cut into stone.
- **Body copy, prose, detail lines**: `--font-body`, sentence case, 12–13px, `--slate` /
  `--slate-dim`, subtle engrave shadow. No serifs anywhere.
- Big display moments (screen titles like "THE FIELD IS OURS", clock numerals) go LARGE in
  `--font-head` — condensed caps at 30–44px tracked wide look spectacular carved into marble.

## Iconography (NEW — second focus of the iteration)

One consistent icon language across all 5 screens:
- **Glyph style**: solid single-colour silhouettes on a 24×24 viewBox, blocky and geometric
  (straight lines, simple curves), no outlines, no interior detail beyond 1–2 cuts. Roman
  vocabulary: helmet with crest, scutum, gladius, pilum bundle, aquila/eagle, vexillum banner,
  laurel wreath, column capital, amphora, wheat, flame, tent, boot, eye, wax tablet, coin.
- **Mounting**: functional icons sit in a **recessed square stone boss**: a 26–34px square with
  `background: linear-gradient(#c6c2b8,#aeaa9f); border:1px solid var(--groove);
  box-shadow: inset 0 1px 2px rgba(40,37,31,.30);` — icon in `--bronze-deep` (or role colour).
  Selected/active bosses switch icon colour to `--purple`.
- **Decorative motifs** (laurels, meander, eagle atop titles) are incised LINE work
  (stroke, no fill, `--slate-dim` at ~60% opacity), distinct from the solid functional glyphs.
- Define icons once per file as `<symbol id="i-*">` in a hidden SVG and reuse with `<use>`.

## Worlds behind the HUD

- Battle screens: the incised-relief stone field from 02 (stone ground #c9c4b8, river as polished
  groove, stippled forests, bronze vs oxblood inlay units).
- Overmap / camp / overlay screens: the HUD must also prove itself over the REAL game's dark 3D
  scenes — use the muted dark naturalistic backdrop gradients from `../../_src/common.css`
  (`.world--overmap`, `.world--camp`) and float the light marble slabs on top. Slabs get a
  slightly stronger drop shadow there (0 10px 26px rgba(20,18,14,.55)) to sit off the dark ground.

## Content rules (from the game's brief — unchanged)

Copy per screen comes verbatim from the reference fragments in `../../_src/pages/`. Percentages
are hidden (ladder words: Broken/Breaking/Wavering/Confident/Fearless; Serviceable/Worn/Failing;
Tiro/Miles/Veteranus/Evocatus). Counts of men, days of food, coin ARE numbers. Entity
descriptions state mechanics, never flavour. Latin sparingly and correctly.

## Files in this series

1. `01-battle-hud.html` — battle HUD (re-cut of style 02 with the new type + icon bosses)
2. `02-overmap-hud.html` — overmap HUD over the dark overmap world
3. `03-camp-rest-orders.html` — camp Rest & Orders over the dark camp world
4. `04-battle-summary.html` — post-battle sheet (display-type showcase)
5. `05-inventory.html` — inventory overlay (iconography showcase: the tile grid)

All fully standalone (own CSS, system fonts, inline SVG only, no build step), 1920×1080 stage
scaled to fit the window. Throwaway style studies — nothing here feeds production.
