using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpinStay
{
    public enum PendulumState { Swinging, Stopping, Stopped }

    /// <summary>
    /// Semicircle-gauge alternative to <see cref="Roulette"/>. A needle swings
    /// left-right over a colored arc; stopping it picks the segment under the
    /// needle. Reuses <see cref="RouletteConfig"/> so the downstream dispatch in
    /// <see cref="GameManager"/> is identical. Self-builds all its UI children
    /// (arc texture, needle, overlay, labels) on Awake.
    /// </summary>
    public class Pendulum : MonoBehaviour
    {
        [SerializeField] private RouletteConfig config;
        [SerializeField] private RectTransform root;
        [SerializeField] private RawImage arcImage;
        [SerializeField] private RectTransform needle;
        [SerializeField] private Font labelFont;

        [Header("Swing")]
        [Range(30f, 89f)][SerializeField] private float maxAngle = 85f;
        [Tooltip("Seconds for one full left-right-left cycle while swinging freely.")]
        [Min(0.2f)][SerializeField] private float swingPeriod = 2.2f;

        [Header("Active segment highlight")]
        [SerializeField] private Color highlightColor = new Color(1f, 1f, 1f, 0.32f);
        [SerializeField] private Color highlightLabelColor = new Color(1f, 0.94f, 0.28f);
        [Range(1f, 1.6f)][SerializeField] private float highlightLabelScale = 1.2f;

        [Header("Active segment readout (UI)")]
        [SerializeField] private Text activeSegmentLabel;
        [SerializeField] private Vector2 activeSegmentLabelOffset = new Vector2(0f, 40f);
        [Min(6)][SerializeField] private int activeSegmentLabelFontSize = 28;
        [SerializeField] private Color activeSegmentLabelColor = new Color(1f, 0.94f, 0.28f, 1f);

        [Header("Debug")]
        [SerializeField] private bool logSegmentTransitions = true;
        [SerializeField] private bool logSegmentTransitionsWhileSpinning = false;
        int lastReportedSegmentIndex = -1;

        public PendulumState State { get; private set; } = PendulumState.Swinging;
        /// <summary>Signed needle angle in degrees: -maxAngle = far left, +maxAngle = far right, 0 = straight up.</summary>
        public float CurrentAngle { get; private set; }
        public RouletteConfig Config => config;
        public RouletteOption[] Options => config != null ? config.options : null;
        public event Action<RouletteOption, int> OnStopped;

        RawImage highlightImage;
        Texture2D arcTexture;
        Texture2D[] segmentHighlightTextures;
        int currentHighlightedIndex = -1;

        readonly List<Text> labelTexts = new List<Text>();
        readonly List<Outline> labelOutlines = new List<Outline>();
        readonly List<Color> labelBaseColors = new List<Color>();
        readonly List<Vector3> labelBaseScales = new List<Vector3>();
        readonly List<Vector2> labelBaseOutlineDistances = new List<Vector2>();

        float phase;               // deg; needle angle = sin(phase) * maxAngle
        float phaseRate;           // deg/s
        float baselinePhaseRate;
        float stopElapsed;
        float stopStartPhaseRate;

        public void SetConfig(RouletteConfig newConfig)
        {
            config = newConfig;
            if (isActiveAndEnabled) RebuildVisuals();
        }

        void Reset() { root = GetComponent<RectTransform>(); }

        void Awake()
        {
            if (root == null) root = GetComponent<RectTransform>();
            if (labelFont == null)
            {
                try { labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch {}
                if (labelFont == null) try { labelFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch {}
            }
            baselinePhaseRate = 360f / Mathf.Max(0.1f, swingPeriod);
            phaseRate = baselinePhaseRate;
            phase = 0f;
            RebuildVisuals();
        }

        void OnValidate()
        {
            baselinePhaseRate = 360f / Mathf.Max(0.1f, swingPeriod);
        }

        public void RebuildVisuals()
        {
            if (root == null) return;
            EnsureArcImage();
            EnsureHighlightImage();
            EnsureNeedle();
            RebuildArcTexture();
            RebuildHighlightTextures();
            RebuildLabels();
            EnsureActiveSegmentLabel();
            currentHighlightedIndex = -1;
            UpdateHighlight();
        }

        void EnsureArcImage()
        {
            if (arcImage != null) return;
            var go = new GameObject("Arc", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            arcImage = go.AddComponent<RawImage>();
            arcImage.raycastTarget = false;
        }

        void EnsureHighlightImage()
        {
            if (highlightImage != null) return;
            var go = new GameObject("HighlightOverlay", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            highlightImage = go.AddComponent<RawImage>();
            highlightImage.raycastTarget = false;
            if (arcImage != null && arcImage.transform.parent == root)
                go.transform.SetSiblingIndex(arcImage.transform.GetSiblingIndex() + 1);
        }

        void EnsureNeedle()
        {
            if (needle != null) return;

            var go = new GameObject("Needle", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            float arcRadius = Mathf.Min(root.rect.width * 0.5f, root.rect.height);
            rt.sizeDelta = new Vector2(8f, Mathf.Max(60f, arcRadius * 0.9f));
            rt.anchoredPosition = Vector2.zero;
            needle = rt;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            img.raycastTarget = false;
        }

        void RebuildLabels()
        {
            var toRemove = new List<GameObject>();
            foreach (Transform c in root)
                if (c.name.StartsWith("Label_")) toRemove.Add(c.gameObject);
            foreach (var g in toRemove)
            {
                if (Application.isPlaying) Destroy(g); else DestroyImmediate(g);
            }
            labelTexts.Clear();
            labelOutlines.Clear();
            labelBaseColors.Clear();
            labelBaseScales.Clear();
            labelBaseOutlineDistances.Clear();

            if (config == null || config.options == null || config.options.Length == 0) return;

            float arcRadius = Mathf.Min(root.rect.width * 0.5f, root.rect.height);
            float labelRadius = arcRadius * Mathf.Clamp(config.labelRadiusRatio, 0.2f, 0.95f);

            float cumT = 0f;
            float totalW = SumWeights();
            for (int i = 0; i < config.options.Length; i++)
            {
                float w = Mathf.Max(0f, config.options[i].weightPercent);
                float startT = cumT;
                cumT += w / totalW;
                float endT = cumT;
                float centerT = 0.5f * (startT + endT);

                // t=0 → leftmost (180°), t=1 → rightmost (0°).
                float screenAngle = 180f - centerT * 180f;
                float rad = screenAngle * Mathf.Deg2Rad;

                float segArcDeg = (endT - startT) * 180f;
                float arcLen = segArcDeg * Mathf.Deg2Rad * labelRadius;
                float safeArc = Mathf.Max(28f, arcLen * 0.85f);
                float labelHeight = Mathf.Clamp(segArcDeg * 0.35f, 18f, 34f);
                int fontSize = Mathf.Clamp(Mathf.RoundToInt(safeArc * 0.30f), 10, config.labelFontSize);

                var go = new GameObject("Label_" + config.options[i].label, typeof(RectTransform));
                go.transform.SetParent(root, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(safeArc, labelHeight);
                rt.anchoredPosition = new Vector2(Mathf.Cos(rad) * labelRadius, Mathf.Sin(rad) * labelRadius);
                rt.localRotation = Quaternion.Euler(0f, 0f, screenAngle - 90f);

                var t = go.AddComponent<Text>();
                t.text = config.options[i].label;
                t.alignment = TextAnchor.MiddleCenter;
                t.fontSize = fontSize;
                t.fontStyle = FontStyle.Bold;
                t.color = Color.white;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                if (labelFont != null) t.font = labelFont;

                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                outline.effectDistance = new Vector2(1.2f, -1.2f);

                labelTexts.Add(t);
                labelOutlines.Add(outline);
                labelBaseColors.Add(t.color);
                labelBaseScales.Add(rt.localScale);
                labelBaseOutlineDistances.Add(outline.effectDistance);
            }
        }

        void EnsureActiveSegmentLabel()
        {
            if (activeSegmentLabel != null) return;

            // Parent under root so deactivating PendulumUI hides the readout too.
            var go = new GameObject("ActiveSegmentLabel", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = activeSegmentLabelOffset;
            rt.sizeDelta = new Vector2(Mathf.Max(200f, root.rect.width), activeSegmentLabelFontSize * 2.2f);

            activeSegmentLabel = go.AddComponent<Text>();
            activeSegmentLabel.font = labelFont;
            activeSegmentLabel.fontSize = activeSegmentLabelFontSize;
            activeSegmentLabel.color = activeSegmentLabelColor;
            activeSegmentLabel.alignment = TextAnchor.MiddleCenter;
            activeSegmentLabel.raycastTarget = false;
            activeSegmentLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            activeSegmentLabel.verticalOverflow = VerticalWrapMode.Overflow;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        void RebuildArcTexture()
        {
            if (arcImage == null || config == null) return;
            if (arcTexture != null)
            {
                if (Application.isPlaying) Destroy(arcTexture); else DestroyImmediate(arcTexture);
            }
            arcTexture = CreateSegmentTexture(-1, Color.white, highlightOnly: false);
            arcImage.texture = arcTexture;
        }

        void RebuildHighlightTextures()
        {
            if (config == null || config.options == null || config.options.Length == 0)
            {
                segmentHighlightTextures = null;
                return;
            }
            int n = config.options.Length;
            if (segmentHighlightTextures != null)
            {
                for (int i = 0; i < segmentHighlightTextures.Length; i++)
                    if (segmentHighlightTextures[i] != null)
                    {
                        if (Application.isPlaying) Destroy(segmentHighlightTextures[i]);
                        else DestroyImmediate(segmentHighlightTextures[i]);
                    }
            }
            segmentHighlightTextures = new Texture2D[n];
            for (int i = 0; i < n; i++)
                segmentHighlightTextures[i] = CreateSegmentTexture(i, highlightColor, highlightOnly: true);
        }

        Texture2D CreateSegmentTexture(int onlySegment, Color fill, bool highlightOnly)
        {
            var opts = config.options;
            int n = opts.Length;

            float total = SumWeights();
            float[] cumEnd = new float[n];
            float cum = 0f;
            for (int i = 0; i < n; i++)
            {
                cum += Mathf.Max(0f, opts[i].weightPercent) / total;
                cumEnd[i] = cum;
            }

            int w = Mathf.Max(64, config.textureSize);
            int h = Mathf.Max(32, w / 2);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float cx = w * 0.5f;
            float cy = 0f;
            float rOuter = h * 0.98f;
            float rRim   = h * 0.90f;
            float rInner = h * 0.30f;
            float dividerHalf = 1.2f;
            Color transparent = new Color(0, 0, 0, 0);

            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = transparent;

                    if (r <= rOuter && r >= rInner)
                    {
                        float angDeg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        if (angDeg >= 0f && angDeg <= 180f)
                        {
                            float t = 1f - (angDeg / 180f);                   // 0=left, 1=right
                            int seg = n - 1;
                            for (int i = 0; i < n; i++) { if (t < cumEnd[i]) { seg = i; break; } }

                            if (highlightOnly)
                            {
                                if (seg == onlySegment && r <= rRim)
                                {
                                    c = fill;
                                    float edge = Mathf.Clamp01(rRim - r);
                                    c.a *= edge;
                                }
                            }
                            else
                            {
                                if (r > rRim) c = config.rimColor;
                                else
                                {
                                    c = opts[seg].color;
                                    float segStart = (seg == 0) ? 0f : cumEnd[seg - 1];
                                    float distToBoundary = Mathf.Min(t - segStart, cumEnd[seg] - t);
                                    float distPixels = distToBoundary * 180f * Mathf.Deg2Rad * r;
                                    if (distPixels < dividerHalf) c = config.dividerColor;
                                }
                                c.a *= Mathf.Clamp01(rOuter - r);
                            }
                        }
                    }

                    pixels[y * w + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        float SumWeights()
        {
            if (config == null || config.options == null) return 1f;
            float s = 0f;
            for (int i = 0; i < config.options.Length; i++)
                s += Mathf.Max(0f, config.options[i].weightPercent);
            return s <= 0.0001f ? 1f : s;
        }

        void Update()
        {
            if (config == null || root == null) return;
            float dt = Time.deltaTime;

            if (State == PendulumState.Stopping)
            {
                float duration = Mathf.Max(0f, config.postStopSpinTime);
                if (duration <= 0.0001f)
                {
                    phaseRate = 0f;
                    FinalizeStop();
                    return;
                }
                stopElapsed += dt;
                float tn = Mathf.Clamp01(stopElapsed / duration);
                phaseRate = stopStartPhaseRate * (1f - tn) * (1f - tn);
                phase += phaseRate * dt;
                CurrentAngle = Mathf.Sin(phase * Mathf.Deg2Rad) * maxAngle;
                ApplyNeedle();
                UpdateHighlight();
                LogTransitions();
                if (tn >= 1f) { FinalizeStop(); return; }
                return;
            }

            if (State == PendulumState.Swinging)
            {
                phase += phaseRate * dt;
                CurrentAngle = Mathf.Sin(phase * Mathf.Deg2Rad) * maxAngle;
                ApplyNeedle();
                UpdateHighlight();
                LogTransitions();
            }
        }

        void ApplyNeedle()
        {
            if (needle != null) needle.localRotation = Quaternion.Euler(0f, 0f, -CurrentAngle);
        }

        void LogTransitions()
        {
            if (!logSegmentTransitions || config == null || config.options == null || config.options.Length == 0) return;
            int idx = GetCurrentSegmentIndex();
            bool shouldLog = logSegmentTransitionsWhileSpinning || State == PendulumState.Stopping;
            if (idx != lastReportedSegmentIndex)
            {
                if (shouldLog && lastReportedSegmentIndex >= 0)
                {
                    Debug.Log(string.Format(
                        "[Pendulum] needle entering segment #{0} \"{1}\"  (angle={2:F0}°)",
                        idx, config.options[idx].label, CurrentAngle));
                }
                lastReportedSegmentIndex = idx;
            }
        }

        void UpdateHighlight()
        {
            if (config == null || config.options == null || config.options.Length == 0) return;
            int idx = GetCurrentSegmentIndex();
            if (idx == currentHighlightedIndex) return;

            if (currentHighlightedIndex >= 0 && currentHighlightedIndex < labelTexts.Count)
            {
                var prev = labelTexts[currentHighlightedIndex];
                if (prev != null)
                {
                    prev.color = labelBaseColors[currentHighlightedIndex];
                    prev.rectTransform.localScale = labelBaseScales[currentHighlightedIndex];
                }
                var prevOut = labelOutlines[currentHighlightedIndex];
                if (prevOut != null) prevOut.effectDistance = labelBaseOutlineDistances[currentHighlightedIndex];
            }

            if (idx >= 0 && idx < labelTexts.Count)
            {
                var cur = labelTexts[idx];
                if (cur != null)
                {
                    cur.color = highlightLabelColor;
                    cur.rectTransform.localScale = labelBaseScales[idx] * highlightLabelScale;
                }
                var curOut = labelOutlines[idx];
                if (curOut != null) curOut.effectDistance = labelBaseOutlineDistances[idx] * 1.7f;
            }

            if (highlightImage != null && segmentHighlightTextures != null
                && idx >= 0 && idx < segmentHighlightTextures.Length)
            {
                highlightImage.texture = segmentHighlightTextures[idx];
                highlightImage.enabled = true;
            }

            if (activeSegmentLabel != null && idx >= 0 && idx < config.options.Length)
                activeSegmentLabel.text = config.options[idx].label;

            currentHighlightedIndex = idx;
        }

        public int GetCurrentSegmentIndex()
        {
            if (config == null || config.options == null || config.options.Length == 0) return 0;
            var opts = config.options;
            float t = Mathf.Clamp01((CurrentAngle + maxAngle) / (2f * maxAngle));
            float total = SumWeights();
            float cum = 0f;
            for (int i = 0; i < opts.Length; i++)
            {
                float start = cum;
                cum += Mathf.Max(0f, opts[i].weightPercent) / total;
                if (t >= start && t < cum) return i;
            }
            return opts.Length - 1;
        }

        public void RequestStop()
        {
            if (State != PendulumState.Swinging) return;
            State = PendulumState.Stopping;
            stopElapsed = 0f;
            stopStartPhaseRate = phaseRate;
        }

        public void RestartSpin()
        {
            State = PendulumState.Swinging;
            phaseRate = baselinePhaseRate;
        }

        void FinalizeStop()
        {
            State = PendulumState.Stopped;
            int idx = GetCurrentSegmentIndex();

            var opts = config.options;
            float total = SumWeights();
            float cum = 0f;
            for (int i = 0; i < idx; i++) cum += Mathf.Max(0f, opts[i].weightPercent) / total;
            float centerT = cum + Mathf.Max(0f, opts[idx].weightPercent) / total * 0.5f;
            CurrentAngle = (centerT * 2f - 1f) * maxAngle;
            phase = Mathf.Asin(Mathf.Clamp(CurrentAngle / Mathf.Max(0.0001f, maxAngle), -1f, 1f)) * Mathf.Rad2Deg;
            ApplyNeedle();
            UpdateHighlight();

            if (logSegmentTransitions)
            {
                Debug.Log(string.Format(
                    "[Pendulum] STOPPED on #{0} \"{1}\"  (snapped angle={2:F0}°, center-of-segment)",
                    idx, opts[idx].label, CurrentAngle));
            }
            OnStopped?.Invoke(opts[idx], idx);
        }
    }
}
