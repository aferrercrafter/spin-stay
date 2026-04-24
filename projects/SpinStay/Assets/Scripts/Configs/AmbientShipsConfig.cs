using UnityEngine;

namespace SpinStay
{
    [CreateAssetMenu(menuName = "SpinStay/Ambient Ships Config", fileName = "AmbientShipsConfig")]
    public class AmbientShipsConfig : ScriptableObject
    {
        [Header("Count")]
        [Min(0)] public int count = 6;

        [Header("Prefabs")]
        [Tooltip("Optional ship prefabs. When set, a random prefab is instantiated per ship instead of the primitive cube+cube fallback.")]
        public GameObject[] shipPrefabs;
        [Tooltip("Optional material applied to every MeshRenderer on the spawned prefab. Use this when the FBX's embedded material is missing the texture.")]
        public Material artMaterial;
        [Tooltip("Uniform scale range applied when using shipPrefabs. Keeps art-authored proportions.")]
        public float prefabScaleMin = 0.4f;
        public float prefabScaleMax = 0.9f;
        [Tooltip("Approximate prefab height (world units) used to seat it on the water plane and drive drift recycling.")]
        public float prefabHeight = 4.7f;
        [Tooltip("How far the keel sits below waterY. Small positive value so the boat looks partly submerged rather than floating.")]
        public float prefabWaterlineOffset = 0.4f;

        [Header("Primitive Fallback Scale")]
        public Vector3 minScale = new Vector3(5f, 1.2f, 2.5f);
        public Vector3 maxScale = new Vector3(14f, 2.8f, 4.5f);

        [Header("Speed")]
        [Tooltip("Lateral drift speed (world units / second). Ships enter from one camera edge on the water plane and drift to the other.")]
        public float minSpeed = 0.6f;
        public float maxSpeed = 3.0f;

        [Header("Spawn at camera edge")]
        [Tooltip("Viewport Y range where a ship crosses the camera's left/right edges on the water plane. 0 = bottom of view (near camera), 1 = top (far horizon). Keep below ~0.9 so ships don't spawn at infinity.")]
        [Range(0f, 1f)] public float viewportYMin = 0.25f;
        [Range(0f, 1f)] public float viewportYMax = 0.85f;

        [Tooltip("Extra world-X units beyond the viewport horizontal edge where a ship spawns and recycles. Bigger = ship is further offscreen before drifting in.")]
        public float edgeBufferX = 6f;

        [Tooltip("Water-level Y. Ships sit on this plane.")]
        public float waterY = -8.6f;

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
