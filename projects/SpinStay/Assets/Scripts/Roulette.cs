using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpinStay
{
    public enum RouletteState { Spinning, Stopping, Stopped }

    public class Roulette : MonoBehaviour
    {
        [SerializeField] private RouletteConfig config;
        [Tooltip("RectTransform that gets rotated. Its children (labels) are regenerated to match the config.")]
        [SerializeField] private RectTransform wheel;
        [Tooltip("RawImage whose texture will be regenerated from the config.")]
        [SerializeField] private RawImage wheelImage;
        [SerializeField] private Font labelFont;

        [Header("Active segment highlight")]
        [Tooltip("Optional RawImage overlay that brightens the segment currently under the pointer. Auto-created as a child of the wheel if left null.")]
        [SerializeField] private RawImage highlightImage;
        [Tooltip("Additive tint drawn on top of the active segment. Alpha controls how strongly the segment is brightened.")]
        [SerializeField] private Color highlightColor = new Color(1f, 1f, 1f, 0.32f);
        [Tooltip("Text color applied to the active segment's label.")]
        [SerializeField] private Color highlightLabelColor = new Color(1f, 0.94f, 0.28f);
        [Tooltip("Scale multiplier applied to the active segment's label.")]
        [Range(1f, 1.6f)][SerializeField] private float highlightLabelScale = 1.2f;

        [Header("Active segment readout (UI)")]
        [Tooltip("Optional Text that always displays the label of the segment currently under the pointer. Auto-created above the wheel if left null. Screenshot-friendly: whatever it shows is what a pick at that instant would land on.")]
        [SerializeField] private Text activeSegmentLabel;
        [Tooltip("Pixel offset from the top of the wheel where the auto-created readout sits.")]
        [SerializeField] private Vector2 activeSegmentLabelOffset = new Vector2(0f, 60f);
        [Tooltip("Font size for the auto-created readout.")]
        [SerializeField, Min(6)] private int activeSegmentLabelFontSize = 28;
        [Tooltip("Text color for the auto-created readout.")]
        [SerializeField] private Color activeSegmentLabelColor = new Color(1f, 0.94f, 0.28f, 1f);

        [Header("Debug")]
        [Tooltip("Logs every segment transition the pointer crosses once the wheel starts stopping. Useful for verifying that the visible segment under the pointer matches the fired action.")]
        [SerializeField] private bool logSegmentTransitions = true;
        [Tooltip("Also logs the transitions while the wheel is spinning at full speed (noisy).")]
        [SerializeField] private bool logSegmentTransitionsWhileSpinning = false;
        int lastReportedSegmentIndex = -1;

        public RouletteState State { get; private set; } = RouletteState.Spinning;
        public float CurrentAngle { get; private set; }
        public float CurrentSpeed { get; private set; }
        public RouletteConfig Config => config;
        public RouletteOption[] Options => config != null ? config.options : null;

        /// <summary>Runtime-only multiplier applied to the configured spin speed. Does not mutate the config asset.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        public event Action<RouletteOption, int> OnStopped;

        float stopElapsed;
        float stopStartSpeed;

        readonly List<Text> labelTexts = new List<Text>();
        readonly List<Outline> labelOutlines = new List<Outline>();
        readonly List<Color> labelBaseColors = new List<Color>();
        readonly List<Vector3> labelBaseScales = new List<Vector3>();
        readonly List<Vector2> labelBaseOutlineDistances = new List<Vector2>();
        Texture2D[] segmentHighlightTextures;
        int currentHighlightedIndex = -1;

        // Spin boost state.
        bool  boostActive;
        float boostElapsed;
        float boostDuration;
        float boostFromSpeed;

        /// <summary>Temporarily boost wheel spin speed; decays linearly back to baseline over duration.</summary>
        public void BoostSpin(float multiplier, float duration)
        {
            if (config == null) return;
            if (State != RouletteState.Spinning) State = RouletteState.Spinning;
            boostActive = true;
            boostElapsed = 0f;
            boostDuration = Mathf.Max(0.05f, duration);
            boostFromSpeed = config.spinSpeed * Mathf.Max(1f, multiplier);
            CurrentSpeed = boostFromSpeed;
        }

        void Reset()
        {
            wheel = GetComponent<RectTransform>();
        }

        void Awake()
        {
            if (labelFont == null)
            {
                try { labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch {}
                if (labelFont == null) try { labelFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch {}
            }
            RebuildVisuals();
            if (config != null) CurrentSpeed = config.spinSpeed;
        }

        public void RebuildVisuals()
        {
            if (config == null || wheel == null) return;

            if (wheelImage != null)
                wheelImage.texture = CreateWheelTexture();

            // Clear old labels.
            var toRemove = new List<GameObject>();
            foreach (Transform c in wheel)
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

            // Place new labels. Label rect & font are sized from each segment's arc-length
            // at the label radius so the text stays clearly inside its color wedge.
            float cumAngle = 0f;
            float radius = wheel.sizeDelta.x * 0.5f * config.labelRadiusRatio;
            for (int i = 0; i < config.options.Length; i++)
            {
                float size = config.options[i].weightPercent * 3.6f;
                float centerInWheel = cumAngle + size * 0.5f;
                cumAngle += size;

                // Same inverse of the texture's pixel-to-wheelLocal mapping, so each label sits on its own wedge.
                float halfSeg0Labels = config.options[0].weightPercent * 3.6f * 0.5f;
                float screenAngle = 90f + halfSeg0Labels - centerInWheel;
                float rad = screenAngle * Mathf.Deg2Rad;

                // Arc length available to this segment at the label radius.
                float arcLen = size * Mathf.Deg2Rad * radius;
                float safeArc = Mathf.Max(28f, arcLen * 0.85f);
                float labelHeight = Mathf.Clamp(size * 0.35f, 18f, 34f);
                int   fontSize = Mathf.Clamp(Mathf.RoundToInt(safeArc * 0.30f), 10, config.labelFontSize);

                var go = new GameObject("Label_" + config.options[i].label, typeof(RectTransform));
                go.transform.SetParent(wheel, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(safeArc, labelHeight);
                rt.anchoredPosition = new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
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

            EnsureHighlightImage();
            RebuildHighlightTextures();
            EnsureActiveSegmentLabel();
            currentHighlightedIndex = -1;
            UpdateHighlight();
        }

        void EnsureActiveSegmentLabel()
        {
            if (wheel == null) return;
            if (activeSegmentLabel != null) return;

            var parent = wheel.parent as RectTransform;
            if (parent == null) return;

            var go = new GameObject("ActiveSegmentLabel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = wheel.anchorMin;
            rt.anchorMax = wheel.anchorMax;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = wheel.anchoredPosition + new Vector2(0f, wheel.rect.height * 0.5f) + activeSegmentLabelOffset;
            rt.sizeDelta = new Vector2(Mathf.Max(200f, wheel.rect.width), activeSegmentLabelFontSize * 2.2f);

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

        void EnsureHighlightImage()
        {
            if (wheel == null) return;
            if (highlightImage != null)
            {
                highlightImage.raycastTarget = false;
                // Keep overlay rendered on top of the wheel graphic but below labels.
                if (wheelImage != null && highlightImage.transform.parent == wheel)
                    highlightImage.transform.SetSiblingIndex(wheelImage.transform.GetSiblingIndex() + 1);
                return;
            }

            var go = new GameObject("HighlightOverlay", typeof(RectTransform));
            go.transform.SetParent(wheel, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;

            highlightImage = go.AddComponent<RawImage>();
            highlightImage.raycastTarget = false;
            // Sit above the wheel graphic but below the labels (labels were added after).
            int insertAt = (wheelImage != null && wheelImage.transform.parent == wheel)
                ? wheelImage.transform.GetSiblingIndex() + 1
                : 0;
            go.transform.SetSiblingIndex(insertAt);
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
                segmentHighlightTextures[i] = CreateHighlightTexture(i);
        }

        Texture2D CreateHighlightTexture(int segmentIndex)
        {
            var options = config.options;
            int n = options.Length;

            float[] cumEnd = new float[n];
            float cum = 0f;
            for (int i = 0; i < n; i++)
            {
                cum += options[i].weightPercent * 3.6f;
                cumEnd[i] = cum;
            }
            float halfSeg0 = options[0].weightPercent * 3.6f * 0.5f;
            float segStart = (segmentIndex == 0) ? 0f : cumEnd[segmentIndex - 1];
            float segEnd = cumEnd[segmentIndex];

            int size = Mathf.Max(64, config.textureSize);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float cx = size * 0.5f;
            float cy = size * 0.5f;
            float rOuter = size * 0.48f;
            float rRim = size * 0.44f;
            float rInner = size * 0.14f;
            Color transparent = new Color(0, 0, 0, 0);
            Color fill = highlightColor;

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    // Texture2D has y growing upward (origin at bottom-left), matching screen-space
                    // where labels and GetCurrentSegmentIndex live. Using `cy - y` mirrored the colored
                    // wedges vertically, so the label "RESET" ended up sitting on LEFT's color etc.
                    float dy = y - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = transparent;
                    if (r >= rInner && r <= rRim)
                    {
                        float angDeg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        if (angDeg < 0) angDeg += 360f;
                        float wheelLocal = Mathf.Repeat(90f + halfSeg0 - angDeg, 360f);
                        if (wheelLocal >= segStart && wheelLocal < segEnd)
                        {
                            c = fill;
                            float edge = Mathf.Clamp01(rRim - r);
                            c.a *= edge;
                        }
                    }
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
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

        Texture2D CreateWheelTexture()
        {
            int size = Mathf.Max(64, config.textureSize);
            var options = config.options;
            int n = options.Length;

            // Cumulative segment end-angles in wheel-local space.
            float[] cumEnd = new float[n];
            float cum = 0f;
            for (int i = 0; i < n; i++)
            {
                cum += options[i].weightPercent * 3.6f;
                cumEnd[i] = cum;
            }
            float halfSeg0 = options[0].weightPercent * 3.6f * 0.5f;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float cx = size * 0.5f;
            float cy = size * 0.5f;
            float rOuter = size * 0.48f;
            float rRim   = size * 0.44f;
            float rInner = size * 0.14f;
            float dividerHalf = 1.2f;
            Color transparent = new Color(0, 0, 0, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    // Texture2D has y growing upward (origin at bottom-left), matching screen-space
                    // where labels and GetCurrentSegmentIndex live. Using `cy - y` mirrored the colored
                    // wedges vertically, so the label "RESET" ended up sitting on LEFT's color etc.
                    float dy = y - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = transparent;

                    if (r <= rOuter)
                    {
                        if (r > rRim) c = config.rimColor;
                        else if (r < rInner) c = config.centerColor;
                        else
                        {
                            float angDeg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                            if (angDeg < 0) angDeg += 360f;
                            // Convert to wheel-local so segment 0 is centered at pointer when angle=0.
                            float wheelLocal = Mathf.Repeat(90f + halfSeg0 - angDeg, 360f);
                            int seg = n - 1;
                            for (int i = 0; i < n; i++)
                            {
                                if (wheelLocal < cumEnd[i]) { seg = i; break; }
                            }
                            c = options[seg].color;

                            float segStart = (seg == 0) ? 0f : cumEnd[seg - 1];
                            float distToBoundary = Mathf.Min(wheelLocal - segStart, cumEnd[seg] - wheelLocal);
                            float distPixels = distToBoundary * Mathf.Deg2Rad * r;
                            if (distPixels < dividerHalf) c = config.dividerColor;
                        }
                        c.a *= Mathf.Clamp01(rOuter - r);
                    }

                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply(false, false);
            return tex;
        }

        void Update()
        {
            if (config == null || wheel == null) return;
            float dt = Time.deltaTime;

            if (State == RouletteState.Stopping)
            {
                float duration = config.postStopSpinTime;
                if (duration <= 0.0001f)
                {
                    CurrentSpeed = 0f;
                    FinalizeStop();
                    return;
                }
                stopElapsed += dt;
                float tn = Mathf.Clamp01(stopElapsed / duration);
                // Ease-out quadratic for a natural wind-down.
                CurrentSpeed = stopStartSpeed * (1f - tn) * (1f - tn);
                if (tn >= 1f)
                {
                    CurrentSpeed = 0f;
                    FinalizeStop();
                    return;
                }
            }

            // Spin boost decays linearly back to the baseline spin speed.
            if (boostActive && State == RouletteState.Spinning)
            {
                boostElapsed += dt;
                float bk = Mathf.Clamp01(boostElapsed / boostDuration);
                CurrentSpeed = Mathf.Lerp(boostFromSpeed, config.spinSpeed, bk) * Mathf.Max(0f, SpeedMultiplier);
                if (bk >= 1f) boostActive = false;
            }
            else if (State == RouletteState.Spinning)
            {
                CurrentSpeed = config.spinSpeed * Mathf.Max(0f, SpeedMultiplier);
            }

            if (CurrentSpeed > 0f)
            {
                CurrentAngle = Mathf.Repeat(CurrentAngle + CurrentSpeed * dt, 360f);
                wheel.localRotation = Quaternion.Euler(0f, 0f, CurrentAngle);
            }

            UpdateHighlight();

            if (logSegmentTransitions && config != null && config.options != null && config.options.Length > 0)
            {
                bool shouldLog = logSegmentTransitionsWhileSpinning || State == RouletteState.Stopping;
                int idx = GetCurrentSegmentIndex();
                if (idx != lastReportedSegmentIndex)
                {
                    if (shouldLog && lastReportedSegmentIndex >= 0)
                    {
                        Debug.Log(string.Format(
                            "[Wheel] pointer entering segment #{0} \"{1}\"  (angle={2:F0}°, speed={3:F0}°/s)",
                            idx, config.options[idx].label, CurrentAngle, CurrentSpeed));
                    }
                    lastReportedSegmentIndex = idx;
                }
            }
        }

        void FinalizeStop()
        {
            State = RouletteState.Stopped;
            int idx = GetCurrentSegmentIndex();

            // Snap to segment center so the pointer is unambiguously inside it.
            float halfSeg0 = config.options[0].weightPercent * 3.6f * 0.5f;
            float cum = 0f;
            for (int i = 0; i < idx; i++) cum += config.options[i].weightPercent * 3.6f;
            float centerInWheel = cum + config.options[idx].weightPercent * 3.6f * 0.5f;
            CurrentAngle = Mathf.Repeat(centerInWheel - halfSeg0, 360f);
            wheel.localRotation = Quaternion.Euler(0f, 0f, CurrentAngle);

            if (logSegmentTransitions)
            {
                Debug.Log(string.Format(
                    "[Wheel] STOPPED on #{0} \"{1}\"  (snapped angle={2:F0}°, center-of-segment)",
                    idx, config.options[idx].label, CurrentAngle));
            }
            OnStopped?.Invoke(config.options[idx], idx);
        }

        public void RequestStop()
        {
            if (State != RouletteState.Spinning) return;
            State = RouletteState.Stopping;
            stopElapsed = 0f;
            stopStartSpeed = CurrentSpeed;
        }

        public void RestartSpin()
        {
            State = RouletteState.Spinning;
            if (config != null) CurrentSpeed = config.spinSpeed * Mathf.Max(0f, SpeedMultiplier);
        }

        public int GetCurrentSegmentIndex()
        {
            if (config == null || config.options == null || config.options.Length == 0) return 0;

            var opts = config.options;
            float halfSeg0 = opts[0].weightPercent * 3.6f * 0.5f;
            float wheelLocal = Mathf.Repeat(CurrentAngle + halfSeg0, 360f);

            float cum = 0f;
            for (int i = 0; i < opts.Length; i++)
            {
                float segStart = cum;
                cum += opts[i].weightPercent * 3.6f;
                float segEnd = cum;

                if (wheelLocal >= segStart && wheelLocal < segEnd)
                    return i;
            }

            return opts.Length - 1;
        }
    }
}
