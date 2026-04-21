# Ambient life — configuration & behavior

Purely visual scenery. Ships on the water, clouds in the sky, birds gliding
through. None of these interact with the walker.

Drifter: [Assets/Scripts/Ambient/AmbientDrifter.cs](../Assets/Scripts/Ambient/AmbientDrifter.cs)
Ships spawner: [Assets/Scripts/Ambient/AmbientShipsSpawner.cs](../Assets/Scripts/Ambient/AmbientShipsSpawner.cs)
Ships config: [Assets/Scripts/Configs/AmbientShipsConfig.cs](../Assets/Scripts/Configs/AmbientShipsConfig.cs)

## AmbientDrifter

Each moving decorative object has this component. It moves the object
along a velocity each frame and recycles it when it drifts too far from
the walker.

| Field | Meaning |
| --- | --- |
| `followTarget` | The walker. Used as the distance reference for recycling. |
| `velocity` | World velocity (m/s). |
| `bobAmplitude` | Per-axis sinusoidal bob amplitude (world units). |
| `bobFrequency` | Bob Hz. |
| `recycleRadius` | When `|position - target|` exceeds this, the drifter respawns at `target + recycleOffset`. |
| `recycleOffset` | Offset from target used when respawning. Jittered slightly each time. |

This replaces a proper object pool for the prototype. Performance is
fine at the current counts (~15-20 drifters).

## Ambient ships (randomized)

Asset: `Assets/Configs/AmbientShipsConfig.asset`

| Field | Meaning |
| --- | --- |
| `count` | How many ships to spawn. Default `6`. |
| `minScale`, `maxScale` | Per-axis scale range (hull cube). |
| `minSpeed`, `maxSpeed` | Drift speed range (m/s). |
| `sideOffsetMin`, `sideOffsetMax` | Random X distance from the rope at spawn (left or right). |
| `waterY` | World Y to place the hulls at. Match the water plane. |
| `zSpread` | How far along Z ships are spread at spawn. |
| `hullPalette[]`, `sailPalette[]` | Color pools — each ship picks randomly. |
| `bobAmplitudeY`, `bobFrequency` | Bob on water. |

`AmbientShipsSpawner.Start` spawns `count` ships under its own transform,
each a hull cube + a sail cube child, randomized per the config, with an
`AmbientDrifter` attached.

## Ambient clouds & birds

Currently hand-placed in the scene under `World/Ambient` (cloud cubes,
dark bird cubes) with `AmbientDrifter` components. No dedicated config
yet — if you need designer-tunable counts/palettes, follow the ships
pattern: create a `ScriptableObject` + a spawner component.

## Tuning

- Richer horizon: increase ships `count`, widen the palettes, expand
  `zSpread` so they pre-populate along the path.
- Calmer sea: reduce `bobAmplitudeY` and `minSpeed`.
- Wider world: widen `sideOffsetMin/Max`.
