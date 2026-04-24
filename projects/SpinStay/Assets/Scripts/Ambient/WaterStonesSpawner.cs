using UnityEngine;

namespace SpinStay
{
    /// <summary>
    /// Scatters N stone prefabs on the water plane around the walker path, avoiding
    /// an exclusion band around the rope so stones never appear under the walker's
    /// feet. Each stone is rotated randomly and partially submerged so it reads as
    /// poking out of the water.
    /// </summary>
    public class WaterStonesSpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject[] rockPrefabs;
        [Tooltip("Optional material applied to every MeshRenderer on the spawned stone.")]
        public Material artMaterial;
        [Min(0)] public int count = 24;

        [Header("Placement")]
        [Tooltip("Water plane Y level. Stones sit near this height with a random sink below.")]
        public float waterY = -8.6f;
        [Tooltip("Z range along the rope where stones may spawn.")]
        public float zMin = -60f;
        public float zMax = 520f;
        [Tooltip("Minimum |X| distance from the rope so stones never spawn under the walker.")]
        public float xMinAbs = 18f;
        [Tooltip("Maximum |X| distance from the rope.")]
        public float xMax = 110f;

        [Header("Scale")]
        public float scaleMin = 0.25f;
        public float scaleMax = 1.0f;

        [Header("Submersion (world units)")]
        public float sinkMin = 0.3f;
        public float sinkMax = 2.5f;

        [Header("Determinism")]
        public int randomSeed = 777;

        void Awake()
        {
            if (rockPrefabs == null || rockPrefabs.Length == 0) return;
            var rng = new System.Random(randomSeed);

            for (int i = 0; i < count; i++)
            {
                var prefab = rockPrefabs[rng.Next(rockPrefabs.Length)];
                if (prefab == null) continue;

                float s = Mathf.Lerp(scaleMin, scaleMax, (float)rng.NextDouble());
                float sign = rng.Next(2) == 0 ? -1f : 1f;
                float x = sign * Mathf.Lerp(xMinAbs, xMax, (float)rng.NextDouble());
                float z = Mathf.Lerp(zMin, zMax, (float)rng.NextDouble());
                float sink = Mathf.Lerp(sinkMin, sinkMax, (float)rng.NextDouble());
                float yaw = (float)(rng.NextDouble() * 360.0);

                // Compose yaw with the prefab's authored rotation (FBX bake).
                var go = Instantiate(prefab, new Vector3(x, waterY - sink, z), Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation, transform);
                // FBX prefab roots bake in a large scale (cm→m). Multiply, don't overwrite.
                go.transform.localScale = prefab.transform.localScale * s;
                go.name = prefab.name + "_" + i;
                if (artMaterial != null)
                {
                    foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
                        mr.sharedMaterial = artMaterial;
                }
            }
        }
    }
}
