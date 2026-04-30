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
        [Tooltip("Optional 3D cloud prefab. When set, a fresh instance is spawned per event and takes priority over the scene cloud visual reference.")]
        public GameObject cloudPrefab;
        [Tooltip("Uniform multiplier applied to the spawned cloud prefab's native scale.")]
        [Min(0.01f)] public float cloudPrefabScale = 1f;
        [Tooltip("World-space scale used for the primitive-cube fallback / scene cloudVisual reference. Ignored when cloudPrefab is set.")]
        public Vector3 cloudScale = new Vector3(12f, 3.5f, 6f);
        public float cloudSpeed = 3f;
        public Color cloudColor = new Color(0.95f, 0.95f, 0.98f, 0.85f);

        [Header("Collision / explosion")]
        [Tooltip("World distance from walker at which the event cloud is considered to hit and explodes.")]
        [Min(0.1f)] public float hitRadius = 3.5f;
        [Tooltip("Forward offset (relative to walker) where the cloud's swept path sits. 0 = cloud sweeps directly through walker.")]
        public float aheadOffset = 0f;
        [Tooltip("Vertical offset above the walker — where the cloud meets the walker.")]
        public float verticalOffset = 1.4f;
        [Tooltip("Sideways spawn distance from the walker. Cloud crosses through walker's column.")]
        [Min(1f)] public float sidewaysRange = 12f;
        [Tooltip("Number of puff particles released by the cartoon explosion.")]
        [Min(1)] public int explosionParticleCount = 24;
        [Tooltip("Lifetime of the cartoon explosion before particles fade out.")]
        [Min(0.1f)] public float explosionDuration = 0.9f;
        [Tooltip("Outward speed of the puff particles at burst.")]
        [Min(0.1f)] public float explosionSpeed = 4.5f;
        [Tooltip("Average puff size in world units.")]
        [Min(0.05f)] public float explosionPuffSize = 0.7f;
        public Color explosionColor = new Color(1f, 1f, 1f, 1f);
    }
}
