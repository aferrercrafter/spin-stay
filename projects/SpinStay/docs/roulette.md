# Roulette — configuration & behavior

The casino wheel that drives every gameplay decision. Spins continuously,
stops when the player presses Space / Click, and applies an **action** based
on the segment under the pointer.

Asset: `Assets/Configs/RouletteConfig.asset`
Script: [Assets/Scripts/Roulette.cs](../Assets/Scripts/Roulette.cs)
Config type: [Assets/Scripts/Configs/RouletteConfig.cs](../Assets/Scripts/Configs/RouletteConfig.cs)

## Top-level config fields

| Field | Meaning |
| --- | --- |
| `options[]` | The slices on the wheel. Each has a label, action type, weight, color, and action-specific fields. Weights must sum to **100**. |
| `spinSpeed` | Continuous spin speed while idle, in °/s. Default `360`. |
| `postStopSpinTime` | Seconds the wheel keeps turning after the player presses stop, easing out. `0` = snap to the segment under the pointer instantly. Default `1.0`. |
| `rimColor`, `dividerColor`, `centerColor` | Visual: gold rim, divider lines between segments, dark hub. |
| `textureSize` | Procedural wheel texture size (pixels). Default `512`. Higher = crisper, slower to regenerate. |
| `labelRadiusRatio` | Where in the radius the label text sits (0.5 = halfway out, 0.7 = near rim). Default `0.60`. |
| `labelFontSize` | Max font size (narrow segments auto-shrink so text stays inside the wedge). Default `24`. |

`OnValidate` logs a warning if total weight ≠ 100.

## Option fields

Every `RouletteOption` has:

- `label` — shown on the wheel and in logs.
- `action` — one of `RouletteActionType`. Determines which extra fields apply.
- `weightPercent` — slice size (% of 360°). Sum of all must be 100.
- `color` — wheel wedge color.

Action-specific fields:

| Action | Uses | What it does |
| --- | --- | --- |
| `TiltShift` | `tiltDelta` | ADDS `tiltDelta`° to the walker's current tilt. Positive = lean right, negative = lean left. **Additive**, so same-side picks accumulate toward a fall. |
| `ResetBalance` | (none) | Pulls the walker to exactly `0°` (full balance). Safest possible outcome. |
| `SpeedUpWheel` | `wheelSpeedMultiplier`, `wheelBoostDuration` | Boosts `spinSpeed` to `spinSpeed × multiplier` for `wheelBoostDuration` seconds, decaying linearly back to baseline. Applied when the next spin restarts after this pick. |
| `Fall` | (none) | Force-fail — walker immediately starts the fall animation. Typically a small-weight slice (high stakes). |

## Default asset values

The `RouletteConfig` C# default (used when creating a fresh asset) is a
simple two-option set:

```
LEFT   TiltShift   tiltDelta=-20   weight=50%   red
RIGHT  TiltShift   tiltDelta=+20   weight=50%   black
```

`RouletteConfig.asset` in the scene typically carries a richer set
(adding RESET / FAST / FALL); check the asset inspector for the
authoritative list. Total weight must always be 100 %.

## Active segment highlight

As the wheel spins, the segment currently under the pointer is
highlighted live so screenshots and the player both always see "where
you'd land right now."

Inspector fields on the `Roulette` component:

| Field | Meaning |
| --- | --- |
| `highlightImage` | Optional `RawImage` overlay drawn on top of the wheel. If left null, one is auto-created inside the wheel on Awake. |
| `highlightColor` | Additive tint for the active segment. Alpha controls brightness. Default `(1,1,1,0.32)`. |
| `highlightLabelColor` | Color applied to the active segment's label text. Default yellow. |
| `highlightLabelScale` | Scale multiplier for the active label (1..1.6). Default `1.2`. |
| `activeSegmentLabel` | Optional `Text` that always reads the active segment. If null, auto-created above the wheel on Awake. |
| `activeSegmentLabelOffset` | Pixel offset above the wheel where the readout sits. Default `(0, 60)`. |
| `activeSegmentLabelFontSize` | Font size for the auto-created readout. Default `28`. |
| `activeSegmentLabelColor` | Color for the auto-created readout. Default yellow. |

Per-segment highlight textures are baked once on `RebuildVisuals`; only
the active texture is assigned to the overlay each frame.

## Rendering details

- `RebuildVisuals()` runs on `Awake` and any time the component is re-added.
  It creates the procedural wheel texture, the per-segment highlight
  textures, and positions one UI Text per option at segment center.
- Label rect size is derived from the segment's arc length at
  `labelRadiusRatio` — narrow segments (small `weightPercent`) get narrower
  label rects AND smaller font so text stays inside the color wedge.
- Labels rotate to stay tangent to the wheel with their "up" pointing at the
  rim.

## Segment detection & snap

- The pointer is drawn at world-angle 90° (12 o'clock).
- Segment 0's **center** is aligned with the pointer when the wheel's
  local-Z rotation is 0°, so segment indexing walks the cumulative
  weights of `(currentAngle + halfSeg0) mod 360`.
- On stop, the wheel **snaps to the exact center** of the landed segment.
  This guarantees the pointer is never sitting on a divider.

## Timing (how fast the loop feels)

Total downtime per click = `postStopSpinTime` + `GameManager.respinDelay`.

With defaults (`1.0 + 0.15 ≈ 1.15 s`) the wheel eases out noticeably
before committing to a segment. Shorter feels snappier but less like a
casino wheel.

Snappiest possible:
- `postStopSpinTime = 0` → wheel snaps to result the instant you click.
- `GameManager.respinDelay = 0` → wheel restarts spinning the same frame
  the action fires.

Deceleration uses an ease-out quadratic (`(1-t)²`) for a natural
wind-down.

## Spin boost (`SpeedUpWheel`)

1. `HandleRouletteStopped` queues `pendingBoostMultiplier` and
   `pendingBoostDuration` in `GameManager`.
2. After `respinDelay`, `GameManager` calls `roulette.RestartSpin()` then
   `roulette.BoostSpin(mult, duration)`.
3. `Roulette.Update` lerps `CurrentSpeed` from `spinSpeed * mult` back to
   `spinSpeed` over `duration`.

Boosts are single-shot and don't stack — a new boost overrides any prior
one.

## Debug logs

| Field | Meaning |
| --- | --- |
| `logSegmentTransitions` | Master toggle for both transition and STOPPED logs. Default `true`. |
| `logSegmentTransitionsWhileSpinning` | Also log transitions at full speed (noisy). Default `false`. |

Log lines:

- `[Wheel] pointer entering segment #N "LABEL" (angle=X°, speed=Y°/s)` —
  fires when the segment under the pointer changes while the wheel is
  decelerating (or always, if the "while spinning" toggle is on).
- `[Wheel] STOPPED on #N "LABEL" (snapped angle=X°, center-of-segment)` —
  at `FinalizeStop`, right before the `OnStopped` event fires. Pairs with
  the next `[Pick] #N LABEL BEFORE: …` log from `GameManager`.

## Extending

### Adding a new action type

1. Add the enum value in `RouletteActionType`.
2. Add any action-specific fields to `RouletteOption` (document them in
   the tooltip).
3. Dispatch in `GameManager.HandleRouletteStopped`'s `switch (option.action)`.
4. Update this doc.

### Adding more options

Just add entries to the `options[]` array on the asset. Weights must sum
to 100. The wheel texture, highlight textures, and labels regenerate
automatically on Awake. For more than ~7 options, consider raising
`textureSize` and lowering `labelFontSize`.
