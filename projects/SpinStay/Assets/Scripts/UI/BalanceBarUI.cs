using UnityEngine;
using UnityEngine.UI;

namespace SpinStay
{
    /// <summary>
    /// Horizontal gradient balance indicator. Cursor shows current tilt, bar itself
    /// shakes past a configurable threshold so the player gets clear warning of the limit.
    /// </summary>
    public class BalanceBarUI : MonoBehaviour
    {
        [SerializeField] private TightropeWalker walker;

        [Header("References (auto-created if missing)")]
        [SerializeField] private RectTransform shakeRoot;
        [SerializeField] private RectTransform barRect;
        [SerializeField] private RectTransform cursor;
        [SerializeField] private RawImage barImage;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Direction")]
        [Tooltip("If true, the cursor moves the same visual direction as the walker tips on screen. Flip this if your camera setup ends up mirrored.")]
        public bool matchVisualTilt = true;

        [Header("Shake")]
        [Tooltip("Absolute normalized tilt [0..1] at which the bar starts shaking.")]
        [Range(0f, 1f)] public float shakeThreshold = 0.6f;
        [Tooltip("Peak shake amplitude in pixels when tilt is at the fall limit.")]
        public float shakeAmplitude = 14f;
        [Tooltip("Shake frequency (Hz).")]
        public float shakeFrequency = 24f;

        [Header("Colors")]
        public Color colorEdge    = new Color(0.80f, 0.10f, 0.14f); // roulette red
        public Color colorMid     = new Color(0.95f, 0.77f, 0.18f); // casino gold
        public Color colorCenter  = new Color(0.07f, 0.48f, 0.22f); // felt green

        Vector2 shakeBasePos;
        float barAlphaMul = 1f;

        void Awake()
        {
            if (shakeRoot == null) shakeRoot = (RectTransform)transform;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (barImage != null && barImage.texture == null)
                barImage.texture = CreateGradientTexture(256);
            shakeBasePos = shakeRoot.anchoredPosition;
        }

        public void SetAlphaMultiplier(float m) => barAlphaMul = Mathf.Clamp01(m);

        public void ApplyColors(Color edge, Color mid, Color center)
        {
            colorEdge   = edge;
            colorMid    = mid;
            colorCenter = center;
            if (barImage == null) return;
            var prev = barImage.texture as Texture2D;
            barImage.texture = CreateGradientTexture(256);
            if (prev != null) Destroy(prev);
        }

        void LateUpdate()
        {
            if (walker == null || barRect == null || cursor == null) return;

            // Positive TiltAngle now tips the walker to screen-right (the walker itself negates
            // the sign when applying it to its transform). The cursor follows that convention by
            // default; flip matchVisualTilt only if you reorient the camera to face the walker.
            float n = walker.NormalizedTilt; // -1..1
            float dirSign = matchVisualTilt ? 1f : -1f;
            float width = barRect.rect.width;
            cursor.anchoredPosition = new Vector2(n * dirSign * width * 0.5f, cursor.anchoredPosition.y);

            float over = Mathf.InverseLerp(shakeThreshold, 1f, Mathf.Abs(n));
            if (over > 0f)
            {
                float amp = shakeAmplitude * over;
                float t = Time.time;
                float sx = Mathf.Sin(t * Mathf.PI * 2f * shakeFrequency) * amp;
                float sy = Mathf.Cos(t * Mathf.PI * 2f * shakeFrequency * 1.3f) * amp * 0.5f;
                shakeRoot.anchoredPosition = shakeBasePos + new Vector2(sx, sy);
            }
            else
            {
                shakeRoot.anchoredPosition = shakeBasePos;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = barAlphaMul;
        }

        public Texture2D CreateGradientTexture(int width)
        {
            var tex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int x = 0; x < width; x++)
            {
                float t = x / (float)(width - 1); // 0..1
                float signed = t * 2f - 1f;       // -1..1
                float abs = Mathf.Abs(signed);
                Color c;
                if (abs < 0.5f) c = Color.Lerp(colorCenter, colorMid, abs / 0.5f);
                else            c = Color.Lerp(colorMid, colorEdge, (abs - 0.5f) / 0.5f);
                tex.SetPixel(x, 0, c);
            }
            tex.Apply(false, false);
            return tex;
        }
    }
}
