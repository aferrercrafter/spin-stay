using UnityEngine;

namespace SpinStay
{
    [CreateAssetMenu(menuName = "SpinStay/Bird Event Config", fileName = "BirdEventConfig")]
    public class BirdEventConfig : ScriptableObject
    {
        [Header("Frequency")]
        [Tooltip("Minimum seconds between bird spawns.")]
        [Min(0f)] public float minInterval = 6f;
        [Tooltip("Maximum seconds between bird spawns.")]
        [Min(0f)] public float maxInterval = 14f;

        [Header("Motion")]
        [Tooltip("Bird travel speed (m/s).")]
        public float speed = 16f;
        [Tooltip("How far left/right of the walker the bird spawns and disappears (m).")]
        public float sidewaysRange = 14f;
        [Tooltip("Vertical offset from walker's head where the bird flies through.")]
        public float verticalOffset = 1.4f;
        [Tooltip("Bird visual scale.")]
        public Vector3 scale = new Vector3(0.5f, 0.35f, 0.8f);
        public Color color = new Color(0.15f, 0.15f, 0.18f);

        [Header("Impact")]
        [Tooltip("Angular velocity (°/s) applied to the walker when the bird passes. Sign is randomized.")]
        public float impulseMin = 20f;
        public float impulseMax = 45f;
        [Tooltip("Radius (m) around the walker within which the impulse fires.")]
        public float hitRadius = 1.2f;
    }
}
