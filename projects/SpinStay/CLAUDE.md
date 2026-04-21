# SpinStay — Claude context

## What the game is

A prototype tightrope walker runner. The walker auto-advances along an endless
rope over water. While they walk, a casino-style **roulette wheel** sits at the
bottom of the screen spinning continuously. The player's only verb is **press
Space / click to stop the wheel** — wherever it lands decides what happens to
the walker's balance. Pick well and they stay up; pick badly (or spam one side)
and they tip, teeter, and fall into the water.

The hook is pure timing under partial information — the wheel result is
legible (big label, animated) but you have to time your stop against which
side the walker is currently leaning.

Theme: casino / circus. Visuals are primitive cubes and cylinders right now
(pre-art). Mechanics and tuning are the focus.

## Engine

- **Unity 6000.4.3f1** (Unity 6)
- **URP 17.4.0** (Universal Render Pipeline) — fallback shader lookups in code
  use `Universal Render Pipeline/Lit` then `Standard`.
- **Input System Package 1.19.0** only (`activeInputHandler=1`). All input goes
  through `Keyboard.current` / `Mouse.current`; do NOT use the legacy
  `UnityEngine.Input.*` API.
- IMGUI (`OnGUI`) is used for a small debug HUD in `GameManager`.
- **uGUI** for the wheel UI. No TextMeshPro — built-in `Text` with
  `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` fallback.

## Scene

`Assets/Scenes/PrototypeScene.unity` is the main/only scene. Key roots:

- **World** — environmental objects: `Water`, `Skyline` (60 backdrop towers),
  `SideBuildings` (40 flanking buildings), `Rope`, and `Ambient` (ships and
  floating clouds/birds, spawned at runtime by `AmbientShipsSpawner` and the
  static ambient drifters).
- **Walker** — the tightrope walker. Children:
  - `Tilt` — pivot that rotates for balance and gets shaken at the limit.
    Children: `Body` (capsule), `Head` (sphere), `BalancePole` (horizontal cube).
  - `Roulette`-UI-owned `CloudVisual` (a transparent cloud sprite toggled on
    for cloud events).
  - `Main Camera` — third-person chase camera parented to the walker.
- **GameManager** — top-level controller + `RngEventManager` component.
- **UICanvas** — `CloudOverlay` (fullscreen dimmer for cloud events),
  `RouletteUI` (`Wheel` RawImage + procedural labels + `Pointer`),
  `BalanceBar` (top-of-screen gradient + cursor that tracks walker tilt).

## High-level dataflow

```
 Player input (Space/Click)
        │
        ▼
 GameManager.Update ──► roulette.RequestStop()
                                │
                                ▼
            Roulette.Update decelerates (postStopSpinTime)
                                │
                                ▼
                 Roulette.FinalizeStop() snaps to segment center
                                │  OnStopped(option, index)
                                ▼
                 GameManager.HandleRouletteStopped switches on option.action:
                     ├─ TiltShift     → walker.PullToTargetTilt(tilt + delta)
                     ├─ ResetBalance  → walker.PullToTargetTilt(0)
                     ├─ SpeedUpWheel  → queue boost, applied on next restart
                     └─ Fall          → walker.ForceFall()
                                │
                                ▼
                TightropeWalker animates (recovery ease, shake, fall-anim)
```

## Code layout (`Assets/Scripts/`)

| Path | Role |
| --- | --- |
| `TightropeWalker.cs` | Balance physics, recovery pull, shake at limit, fall animation into water. |
| `Roulette.cs` | Spin, stop-with-decel, segment detection, procedural wheel texture + labels, `BoostSpin`, debug transition logging. |
| `GameManager.cs` | Input, roulette ↔ walker dispatch by `RouletteActionType`, before/after pick logging, click-to-reset after a fall. |
| `Configs/RouletteConfig.cs` | ScriptableObject: options with `RouletteActionType`, tilt delta, boost multiplier/duration, weight %, colors, visuals. |
| `Configs/BirdEventConfig.cs` | SO: frequency, speed, hit radius, impulse range for RNG bird events. |
| `Configs/CloudEventConfig.cs` | SO: frequency, duration, fades, overlay / balance-bar dim values. |
| `Configs/AmbientShipsConfig.cs` | SO: ship count, scale/speed ranges, hull & sail color palettes. |
| `UI/BalanceBarUI.cs` | Gradient bar + cursor that tracks tilt, shakes past threshold, dims during cloud events. |
| `Events/BirdEvent.cs` | One-shot bird spawn; flies through walker area, applies random angular impulse. |
| `Events/CloudEvent.cs` | Fades overlay in/hold/out, drifts a 3D cloud, dims balance bar. |
| `Events/RngEventManager.cs` | Timer-driven spawner for birds and clouds. |
| `Ambient/AmbientDrifter.cs` | Drifts an object by `velocity`, recycles when too far from follow-target. |
| `Ambient/AmbientShipsSpawner.cs` | Spawns N ships with randomized scale/color/speed per `AmbientShipsConfig`. |

Configs live in `Assets/Configs/*.asset` and are referenced from the scene
by `GameManager`, `Roulette`, and `RngEventManager`.

## Mechanics quick-reference

- **Balance model** — constant-lean: the walker always leans at `leanSpeed`
  (°/s) in whichever direction their current tilt is signed. When tilt is
  within `zeroTiltThreshold` of zero, the sign is re-rolled randomly. There
  is no pendulum, no drift, no damping — the roulette picks are the only
  real input on tilt. A `disableBalance` debug toggle freezes tilt at 0
  for roulette-only tuning. See [docs/walker.md](docs/walker.md).
- **Recovery pull** — roulette pick animates tilt from current to target over
  `recoveryDuration` (ease-in-out). Physics is suppressed during recovery so
  the lean is a clean, readable animation.
- **Fall sequence** — at the fall angle, walker gets a grace period
  (`graceAtLimit`) at the limit before the fall triggers. Then a 1.4 s
  animation rotates to 90° and drops to `fallWaterY = -8`. The player
  must click/Space once to reset.
- **Shake at the limit** — walker body shakes via `tiltRoot.localPosition`
  once `|normalizedTilt| > shakeThreshold`, same pattern used by the balance
  bar UI.
- **Roulette options** — five default: `LEFT`, `RIGHT`, `RESET`, `FAST`,
  `FALL`. See [docs/roulette.md](docs/roulette.md) for each action type and
  the config schema.
- **RNG events** — a bird that knocks the walker, a cloud that fogs the
  screen and dims the balance bar. See [docs/rng-events.md](docs/rng-events.md).
- **Ambient life** — ships, clouds, birds drifting for flavor, no gameplay
  effect. See [docs/ambient.md](docs/ambient.md).

## Input

- **Space** or **Left Mouse Button** —
  - While the wheel is spinning and walker is alive → stops the wheel.
  - After the fall animation completes → resets the run.
- No other input. Runs endlessly until fall.

## Debug logs

Enable/disable from component inspectors. Key logs:

- `[Wheel] pointer entering segment #N "LABEL"` — on every segment transition
  while the wheel is decelerating (or always, if
  `logSegmentTransitionsWhileSpinning` is on). Lets you verify the visible
  segment under the pointer matches the fired action.
- `[Wheel] STOPPED on #N "LABEL"` — at `FinalizeStop`.
- `[Pick] #N LABEL BEFORE: tilt=… angVel=… <action summary> (drift since last
  pick: …)` — logged by `GameManager.HandleRouletteStopped`.
- `[Pick] LABEL AFTER: tilt=… Δ=… (target …, error …)` — logged after the
  recovery animation completes.
- `[Pick] LABEL AFTER*: …  [flushed — next pick arrived]` — flushed early
  when a new pick came in before the previous AFTER log could fire.
- `[Walker] Fell after X m. Click / Space to restart.` — on fall trigger.

## Gotchas for future sessions

- **Additive, not absolute tilt deltas**: `option.tiltDelta` is ADDED to the
  current tilt. Spamming the same side accumulates to a fall. This is
  intentional — do not go back to the absolute-target semantic; that made
  spamming the same option safe forever.
- **Field name changes** are done via `[FormerlySerializedAs]`. Specifically
  `RouletteOption.tiltDelta` previously was `balanceImpulse` → `targetTilt`.
  Don't rename again without adding `FormerlySerializedAs` or existing asset
  values will be lost.
- **MCP play-mode script-execute**: `EditorApplication.isPlaying = true`
  starts play mode asynchronously. A subsequent script-execute will see the
  game running, but edit-only APIs (`EditorSceneManager.MarkSceneDirty`) fail
  during play. Always stop play mode before mutating the scene.
- **No TMP**: Labels use legacy `UnityEngine.UI.Text` with a built-in font
  fallback. Don't pull TMP in without also importing TMP Essentials.

## Project / package notes

- `Packages/manifest.json` intentionally does NOT include MCP tooling
  (`com.ivanmurzak.unity.mcp*`) or the OpenUPM scoped registry — those
  are per-developer. If you want MCP locally, install it via the Unity
  Package Manager and mark the manifest skip-worktree so your local
  changes don't get committed:
  `git update-index --skip-worktree projects/SpinStay/Packages/manifest.json projects/SpinStay/Packages/packages-lock.json`
- `.mcp.json`, `.claude/`, and `.vsconfig` are gitignored for the same
  reason.
- `Assets/Scenes/SampleScene.unity` is the Unity template default and
  unused — the prototype lives in `PrototypeScene.unity`.

---
Detailed docs referenced above live in `docs/`:
- [docs/roulette.md](docs/roulette.md)
- [docs/walker.md](docs/walker.md)
- [docs/rng-events.md](docs/rng-events.md)
- [docs/ambient.md](docs/ambient.md)
- [docs/balance-bar.md](docs/balance-bar.md)
