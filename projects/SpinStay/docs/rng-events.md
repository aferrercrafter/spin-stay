# RNG events — configuration & behavior

Random environmental events that disrupt the walker independently of the
roulette picks.

Manager: [Assets/Scripts/Events/RngEventManager.cs](../Assets/Scripts/Events/RngEventManager.cs)
One event type = one SO config + one one-shot component that spawns it.

## Bird

Dark cube that flies across in front of the walker and knocks their
balance with a random tilt nudge.

Asset: `Assets/Configs/BirdEventConfig.asset`
Script: [Assets/Scripts/Events/BirdEvent.cs](../Assets/Scripts/Events/BirdEvent.cs)
Config: [Assets/Scripts/Configs/BirdEventConfig.cs](../Assets/Scripts/Configs/BirdEventConfig.cs)

### Fields

| Field | Meaning |
| --- | --- |
| `minInterval`, `maxInterval` | Seconds between bird spawns (picked uniformly per cycle). |
| `speed` | Bird travel speed (m/s). |
| `sidewaysRange` | How far left/right of the walker the bird spawns and exits. |
| `verticalOffset` | Height (above the rope) the bird flies at. |
| `scale` | Bird visual scale (cube `localScale`). |
| `color` | Bird color. |
| `impulseMin`, `impulseMax` | Random tilt delta (°) applied when the bird passes through the walker's hit radius. Sign randomized. Under the lean model there is no angular velocity to kick, so this is interpreted by `ApplyAngularImpulse` as a direct tilt delta. |
| `hitRadius` | World radius around the walker within which the impulse fires. |

### Flow

1. `RngEventManager` tick counts `birdTimer` down; when 0, spawns a bird
   and re-rolls the next interval.
2. `BirdEvent.Launch()` picks a side, positions at one edge, sets a
   straight-line path to the opposite edge.
3. Each frame the bird moves along its path. When within `hitRadius` of
   the walker (and hasn't already dealt damage), it calls
   `walker.ApplyAngularImpulse(dir × magnitude)`.
4. When it reaches the far side it destroys itself.

Birds do not spawn while the walker is fallen.

## Cloud

A drifting fog that briefly obscures the player's view and dims the
balance bar, making balance harder to read.

Asset: `Assets/Configs/CloudEventConfig.asset`
Script: [Assets/Scripts/Events/CloudEvent.cs](../Assets/Scripts/Events/CloudEvent.cs)
Config: [Assets/Scripts/Configs/CloudEventConfig.cs](../Assets/Scripts/Configs/CloudEventConfig.cs)

### Fields

| Field | Meaning |
| --- | --- |
| `minInterval`, `maxInterval` | Seconds between cloud spawns. |
| `duration` | How long the fog holds at peak (seconds). |
| `fadeIn`, `fadeOut` | Fade durations either side of the hold. |
| `overlayAlpha` | Peak opacity of the full-screen overlay (0..1). Current approach is a uniform gray tint — treat as fog, not true blur. |
| `overlayColor` | Overlay RGB. |
| `balanceBarAlpha` | Alpha multiplier on the balance bar at peak. `0.25` = mostly invisible. `1.0` = unchanged. |
| `cloudScale`, `cloudSpeed`, `cloudColor` | 3D cloud visual (transparent cube parented to walker) that drifts past for extra flavor. |

### Flow

1. `RngEventManager.SpawnCloud()` creates a `CloudEvent` and passes it
   the SO, the `CloudOverlay` RawImage, the balance bar, and the 3D
   cloud visual transform.
2. `CloudEvent.Update` runs the fadeIn → hold → fadeOut curve, driving
   both the overlay alpha and the balance bar alpha multiplier.
3. The 3D cloud visual drifts from one side to the other over the full
   duration, then is disabled.
4. The event `Destroy`s itself at the end.

## Manager fields

`RngEventManager` on `GameManager` holds:

- `walker` — target walker.
- `birdConfig`, `cloudConfig` — SO refs.
- `cloudOverlay` — the full-screen RawImage in `UICanvas/CloudOverlay`.
- `balanceBar` — reference to `BalanceBarUI`.
- `cloudVisual` — reusable 3D cloud (transparent cube) parented to walker.

Disable an event by unassigning its config; the manager simply skips
that timer path.

## True blur?

The current cloud uses an opaque gray overlay, not a shader-based blur.
If you want real blur, the clean path is a URP post-processing Volume
with a Depth of Field / Gaussian blur override that `CloudEvent` toggles
and animates. That's future work — keep the overlay if the prototype
doesn't need it.
