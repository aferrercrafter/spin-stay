# Tightrope walker — configuration & behavior

Script: [Assets/Scripts/TightropeWalker.cs](../Assets/Scripts/TightropeWalker.cs)

Everything the walker does — forward motion, passive balance lean,
recovery animation, shake, and fall — lives on this single component.
Fields are serialized directly (no ScriptableObject yet); promote if
you need per-level tuning.

## Movement

| Field | Meaning |
| --- | --- |
| `forwardSpeed` | Constant forward speed (world units / s) along `transform.forward`. Default `1.5`. |

The walker never stops advancing until it falls.

## Debug

| Field | Meaning |
| --- | --- |
| `disableBalance` | Freezes balance/fall entirely — tilt held at 0, roulette recovery / drift / bird impulses all ignored. Use when tuning the roulette in isolation. Default `false`. |

## Balance — limits

| Field | Meaning |
| --- | --- |
| `fallAngle` | Absolute tilt angle (degrees) that counts as "at the limit". Default `60`. |
| `graceAtLimit` | Seconds the walker can linger AT the limit before actually falling. Gives the player time to spin and recover. Default `0.9`. |
| `limitRecoveryThreshold` | While teetering, once `|tilt|` drops below this fraction of `fallAngle`, teetering clears and the grace timer resets. Default `0.75`. |

## Balance — lean

The current model is **constant-lean**: the walker always leans at
`leanSpeed`, in whichever direction their current tilt is signed. When
`|tilt|` is at or below `zeroTiltThreshold` (near balanced), the sign is
re-rolled randomly so they never sit stable at zero.

| Field | Meaning |
| --- | --- |
| `leanSpeed` | Constant tilt rate (°/s). The walker always leans at this speed toward the current sign. Default `12`. |
| `zeroTiltThreshold` | If `\|tilt\|` ≤ this, the next-frame lean direction is re-rolled randomly. Default `0.25`. |

Previously this was a pendulum model (`tipGravity`, `damping`, `noise`,
drift). Those fields are gone — the constant-lean model is simpler and
makes the roulette picks the only real input on tilt.

## Recovery response (roulette pick)

Driven by `PullToTargetTilt(targetTilt)`. Animates the walker's tilt to
the target over `recoveryDuration` with **ease-out cubic** easing (fast
start, settles at the end — gives the pull a punchy feel).

| Field | Meaning |
| --- | --- |
| `recoveryDuration` | Seconds to reach the target tilt. Shorter = snappier, longer = more dramatic lean. Default `0.45`. |
| `suppressPhysicsDuringRecovery` | If true, the constant-lean update is skipped during the pull so the animation reads cleanly. Default `true`. Flip off if you want lean to fight the recovery. |

`PullToTargetTilt` allows targets up to ±120 % of `fallAngle`. If the
pull overshoots the limit, the grace/teeter/fall logic still kicks in
during the recovery animation — that's what makes spamming one side
eventually fatal.

## Shake at the limit

Visual feedback when near falling. Offsets `tiltRoot.localPosition`.

| Field | Meaning |
| --- | --- |
| `shakeThreshold` | Normalized tilt (0..1) at which shake starts. Default `0.6` (60 % of fall angle). |
| `shakeAmplitude` | Peak shake offset (world units) at the fall angle. Default `0.08`. |
| `shakeFrequency` | Shake Hz. Default `18`. |

The balance bar UI uses the same pattern.

## Fall sequence

Triggered when the grace timer expires OR a `Fall` roulette option fires
via `ForceFall()`.

| Field | Meaning |
| --- | --- |
| `fallWaterY` | World Y the body drops to. Should match the water surface. Default `-8`. |
| `fallAnimationDuration` | Total seconds for tilt-to-90 + drop. Default `1.4`. |

Sequence (timings as fractions of `fallAnimationDuration`):

1. **0 → 30 %**: rotate `tiltRoot` smoothly from its current tilt to
   ±90° in the fall direction.
2. **20 → 100 %**: drop `transform.position.y` from current to
   `fallWaterY`, with a small sideways arc. The tilt and drop phases
   overlap by 10 % so the motion blends.
3. At 100 % `IsFallAnimating` becomes false.
   `FallAnimationComplete = IsFallen && !IsFallAnimating`.
4. `GameManager` listens for input and calls `ResetWalker()` on the next
   Space / Click.

## Public API

| Member | Purpose |
| --- | --- |
| `TiltAngle`, `AngularVelocity`, `NormalizedTilt` | Current balance state. `AngularVelocity` under the lean model is just `leanSign * leanSpeed` (or the recovery derivative). |
| `IsFallen`, `IsTeetering`, `IsFallAnimating`, `FallAnimationComplete` | Flags used by `GameManager` and `BalanceBarUI`. |
| `DistanceTravelled` | Meters travelled; your "score" for the run. |
| `FallAngle`, `RecoveryDuration` | Read-only getters for the UI / log code. |
| `PullToTargetTilt(targetTilt)` | Start an animated recovery to the given tilt. |
| `ApplyAngularImpulse(tiltDelta °)` | Instant tilt nudge. Under the lean model there's no velocity to kick, so the argument is interpreted as a direct tilt delta (clamped to ±`fallAngle`). Used by `BirdEvent`. |
| `ApplyBalanceImpulse(value)` | `[Obsolete]` back-compat alias; routes to `PullToTargetTilt`. |
| `ForceFall()` | Trigger the fall sequence unconditionally. |
| `ResetWalker()` | Clear all state for a fresh run. `GameManager.ResetRun()` also repositions the walker at the start. |

## Tuning tips

- Easier: lower `leanSpeed`, raise `graceAtLimit`, shorten
  `recoveryDuration`.
- Harder: raise `leanSpeed`, lower `graceAtLimit`, lower
  `shakeThreshold` so the shake starts later (less warning).
- More dramatic fall: raise `fallAnimationDuration`.
