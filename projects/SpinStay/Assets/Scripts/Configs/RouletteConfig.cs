using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpinStay
{
    public enum RouletteActionType
    {
        TiltShift,     // add tiltDelta to current tilt (LEFT / RIGHT)
        ResetBalance,  // smooth pull back to 0° — instant recovery
        SpeedUpWheel,  // boost wheel spin for wheelBoostDuration, decaying back to normal
        Fall,          // force-lose immediately
        Nothing,       // no-op — pick happens, walker state untouched (used heavily by the pendulum gauge)
    }

    [Serializable]
    public class RouletteOption
    {
        public string label = "OPT";
        public RouletteActionType action = RouletteActionType.TiltShift;

        [Tooltip("TILT SHIFT only: degrees ADDED to the walker's current tilt. Negative = left, positive = right. Spamming the same side accumulates.")]
        [FormerlySerializedAs("balanceImpulse")]
        [FormerlySerializedAs("targetTilt")]
        public float tiltDelta;

        [Tooltip("SPEED UP WHEEL only: multiplier applied to spin speed (e.g. 2.5 = 2.5× as fast).")]
        public float wheelSpeedMultiplier = 2.5f;
        [Tooltip("SPEED UP WHEEL only: seconds the boost lasts before decaying back to normal spin speed.")]
        public float wheelBoostDuration = 3.5f;

        [Tooltip("Slice weight (percent of the wheel). All weights across options should sum to 100. FALL should be small!")]
        [Range(0.01f, 100f)] public float weightPercent = 25f;
        public Color color = Color.gray;
    }

    [CreateAssetMenu(menuName = "SpinStay/Roulette Config", fileName = "RouletteConfig")]
    public class RouletteConfig : ScriptableObject
    {
        [Header("Options")]
        public RouletteOption[] options = new RouletteOption[]
        {
            new RouletteOption { label = "LEFT",  tiltDelta = -20f, weightPercent = 50f, color = new Color(0.80f, 0.10f, 0.14f) },
            new RouletteOption { label = "RIGHT", tiltDelta =  20f, weightPercent = 50f, color = new Color(0.07f, 0.07f, 0.09f) },
        };

        [Header("Spin")]
        [Tooltip("Continuous spin speed while idle (°/s).")]
        public float spinSpeed = 360f;
        [Tooltip("Seconds the wheel continues spinning after the player presses stop. 0 = instant.")]
        [Min(0f)] public float postStopSpinTime = 1.0f;

        [Header("Visuals")]
        public Color rimColor     = new Color(0.92f, 0.72f, 0.20f); // brass/gold rim
        public Color dividerColor = new Color(0.98f, 0.88f, 0.50f); // bright gold dividers
        public Color centerColor  = new Color(0.12f, 0.04f, 0.05f); // deep mahogany hub
        [Min(64)] public int textureSize = 512;
        [Range(0.2f, 0.9f)] public float labelRadiusRatio = 0.60f;
        [Min(8)] public int labelFontSize = 24;

        public float TotalWeight()
        {
            if (options == null) return 0f;
            float s = 0f;
            for (int i = 0; i < options.Length; i++) s += Mathf.Max(0f, options[i].weightPercent);
            return s;
        }

        void OnValidate()
        {
            if (options == null || options.Length == 0) return;
            float s = TotalWeight();
            if (Mathf.Abs(s - 100f) > 0.01f)
                Debug.LogWarning($"[RouletteConfig:{name}] Option weights sum to {s:F1}, expected 100.", this);
        }
    }
}
