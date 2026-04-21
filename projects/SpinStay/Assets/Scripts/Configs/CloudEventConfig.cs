using UnityEngine;

namespace SpinStay
{
    [CreateAssetMenu(menuName = "SpinStay/Cloud Event Config", fileName = "CloudEventConfig")]
    public class CloudEventConfig : ScriptableObject
    {
        [Header("Frequency")]
        [Min(0f)] public float minInterval = 10f;
        [Min(0f)] public float maxInterval = 22f;

        [Header("Duration")]
        [Tooltip("How long the cloud fog stays over the player (seconds).")]
        [Min(0f)] public float duration = 3.5f;
        [Min(0f)] public float fadeIn = 0.5f;
        [Min(0f)] public float fadeOut = 0.8f;

        [Header("Obscuration")]
        [Tooltip("Screen-space fog opacity at peak (0..1).")]
        [Range(0f, 1f)] public float overlayAlpha = 0.55f;
        public Color overlayColor = new Color(0.82f, 0.85f, 0.88f, 1f);
        [Tooltip("Alpha multiplier applied to the balance bar at peak (0 = invisible, 1 = unchanged).")]
        [Range(0f, 1f)] public float balanceBarAlpha = 0.25f;

        [Header("Cloud visual")]
        public Vector3 cloudScale = new Vector3(12f, 3.5f, 6f);
        public float cloudSpeed = 3f;
        public Color cloudColor = new Color(0.95f, 0.95f, 0.98f, 0.85f);
    }
}
