# Balance bar — configuration & behavior

Script: [Assets/Scripts/UI/BalanceBarUI.cs](../Assets/Scripts/UI/BalanceBarUI.cs)

Top-of-screen indicator showing the walker's current tilt as a position
along a red→yellow→green→yellow→red gradient. Cursor moves, and the whole
bar shakes once the walker gets close to the limit.

## Fields

| Field | Meaning |
| --- | --- |
| `walker` | The `TightropeWalker` to track. |
| `shakeRoot`, `barRect`, `cursor`, `barImage`, `canvasGroup` | UI refs auto-wired by the scene setup script. |
| `matchVisualTilt` | If true, the cursor moves the **same visual direction** the walker is leaning on screen. Default `true`. Flip if the camera ever mirrors. |
| `shakeThreshold` | Normalized tilt (0..1) at which shake begins. Default `0.6`. |
| `shakeAmplitude` | Peak shake amplitude (pixels) at the fall limit. Default `14`. |
| `shakeFrequency` | Shake Hz. Default `24`. |
| `colorEdge`, `colorMid`, `colorCenter` | Gradient stops: edges (danger), middle (caution), center (safe). |

## Gradient texture

`CreateGradientTexture(width)` builds a 1-pixel-tall gradient texture at
runtime. The mapping is:

- center = `colorCenter`
- halfway to each edge = `colorMid`
- edges = `colorEdge`

Assigned to `barImage.texture` on Awake and again by the scene setup script
for editor preview.

## Cursor

Each LateUpdate:

```
n = walker.NormalizedTilt  // -1..1
dirSign = matchVisualTilt ? -1 : +1
cursor.anchoredPosition.x = n * dirSign * barWidth * 0.5
```

## Shake

If `|n| > shakeThreshold`, shake scales from 0 at the threshold to full
amplitude at the limit, using `Mathf.InverseLerp(shakeThreshold, 1,
|n|)`. The same `shakeRoot` offset pattern is mirrored on the walker
itself in `TightropeWalker.ApplyVisualTiltAndShake`.

## Cloud dimming

`SetAlphaMultiplier(float)` lets `CloudEvent` dim the bar as the fog rolls
in. Restored to `1.0` when the event ends.
