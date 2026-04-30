using UnityEngine;

namespace SpinStay
{
    /// <summary>
    /// Arranges every direct child inside an ellipse on the XZ plane at a fixed Y so
    /// the old hand-scattered-over-water buildings instead read as a single
    /// ground-anchored island. Use twice: once on Skyline (dense inner cluster of
    /// tall towers) and once on SideBuildings (outer ring of shorter buildings)
    /// sharing the same center so they form one island.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class IslandLayout : MonoBehaviour
    {
        [Header("Center (local to this transform)")]
        public Vector3 centerOffset = new Vector3(0f, 0f, 500f);

        [Header("Ellipse radii")]
        public float radiusX = 150f;
        public float radiusZ = 120f;

        [Tooltip("Min fraction of the ellipse radius a child is placed at. 0 = can land at center; ~0.6 = ring around center (use for outer fringe).")]
        [Range(0f, 1f)] public float minRadiusFraction = 0f;

        [Tooltip("Radial bias. <1 pushes children outward, 1 ≈ linear r, >1 concentrates near center.")]
        [Range(0.1f, 3f)] public float radialBias = 1f;

        [Header("Per-child scale")]
        [Tooltip("Uniform multiplier applied to each child's authored scale before placement. Use to make every building larger without re-authoring each prefab. 1 = unchanged.")]
        [Min(0.1f)] public float scaleMultiplier = 1f;

        [Header("Ground plane")]
        [Tooltip("Y level the island ground sits at. Children are placed with their bottom on this Y.")]
        public float groundY = -1.5f;

        [Header("Determinism")]
        public int randomSeed = 1234;
        [Tooltip("Re-layout on every Awake with the same seed. Off = varies each run.")]
        public bool deterministic = true;

        void Awake() => Layout();

        public void Layout()
        {
            System.Random rng = deterministic
                ? new System.Random(randomSeed)
                : new System.Random();

            int n = transform.childCount;
            for (int i = 0; i < n; i++)
            {
                Transform c = transform.GetChild(i);
                if (c == null) continue;

                float u = (float)rng.NextDouble();
                float f = Mathf.Pow(u, radialBias);
                float r01 = Mathf.Lerp(minRadiusFraction, 1f, f);
                float theta = (float)(rng.NextDouble() * Mathf.PI * 2.0);

                float x = centerOffset.x + Mathf.Cos(theta) * radiusX * r01;
                float z = centerOffset.z + Mathf.Sin(theta) * radiusZ * r01;

                if (!Mathf.Approximately(scaleMultiplier, 1f))
                    c.localScale *= scaleMultiplier;

                float h = Mathf.Max(0.01f, c.localScale.y);
                float y = groundY + h * 0.5f;

                c.localPosition = new Vector3(x, y, z);
            }
        }
    }
}
