using UnityEngine;

namespace SpinStay
{
    [CreateAssetMenu(menuName = "SpinStay/Ambient Ships Config", fileName = "AmbientShipsConfig")]
    public class AmbientShipsConfig : ScriptableObject
    {
        [Header("Count")]
        [Min(0)] public int count = 6;

        [Header("Scale")]
        public Vector3 minScale = new Vector3(5f, 1.2f, 2.5f);
        public Vector3 maxScale = new Vector3(14f, 2.8f, 4.5f);

        [Header("Speed")]
        public float minSpeed = 0.6f;
        public float maxSpeed = 3.0f;

        [Header("Placement")]
        [Tooltip("Half-width of the channel (X distance from the rope where ships spawn).")]
        public float sideOffsetMin = 45f;
        public float sideOffsetMax = 75f;
        [Tooltip("Water-level Y.")]
        public float waterY = -8.6f;
        [Tooltip("How far ahead/behind along Z ships are initially spread.")]
        public float zSpread = 200f;

        [Header("Colors")]
        public Color[] hullPalette = new[]
        {
            new Color(0.35f, 0.28f, 0.22f),
            new Color(0.24f, 0.20f, 0.17f),
            new Color(0.55f, 0.42f, 0.30f),
            new Color(0.65f, 0.58f, 0.48f),
            new Color(0.30f, 0.35f, 0.40f),
        };
        public Color[] sailPalette = new[]
        {
            new Color(0.95f, 0.92f, 0.85f),
            Color.white,
            new Color(0.85f, 0.75f, 0.60f),
            new Color(0.80f, 0.30f, 0.25f),
            new Color(0.25f, 0.35f, 0.65f),
        };

        [Header("Bobbing")]
        public float bobAmplitudeY = 0.18f;
        public float bobFrequency = 0.45f;
    }
}
