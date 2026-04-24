using UnityEngine;

namespace SpinStay
{
    /// <summary>
    /// Spawns N ambient ships at the camera's left/right viewport edges on the water
    /// plane, so every ship enters the scene where the player would actually see it
    /// appear. Ships drift across the view and are recycled back to the edge on exit.
    /// </summary>
    public class AmbientShipsSpawner : MonoBehaviour
    {
        [SerializeField] private AmbientShipsConfig config;
        [SerializeField] private Transform followTarget;
        [Tooltip("Camera used to compute viewport edges. Falls back to Camera.main at Start if null.")]
        [SerializeField] private Camera cam;

        void Start()
        {
            if (config == null || followTarget == null) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            bool usePrefabs = config.shipPrefabs != null && config.shipPrefabs.Length > 0;

            for (int i = 0; i < config.count; i++)
            {
                bool left = Random.value < 0.5f;
                float speed = Random.Range(config.minSpeed, config.maxSpeed);

                GameObject ship;
                float shipHeight;

                if (usePrefabs)
                {
                    var prefab = config.shipPrefabs[Random.Range(0, config.shipPrefabs.Length)];
                    float s = Random.Range(config.prefabScaleMin, config.prefabScaleMax);
                    shipHeight = Mathf.Max(0.01f, config.prefabHeight * s);
                    Vector3 pos = ComputeEdgeSpawnWorldPos(cam, config, left, shipHeight);
                    // Boat FBX pivot is at the keel and the mesh faces +X; sit the keel slightly
                    // below the water plane and yaw 180° when drifting right-to-left.
                    pos.y = config.waterY - config.prefabWaterlineOffset;
                    // Compose yaw with the prefab's authored rotation (FBX roots typically
                    // have a 270° X bake to convert DCC Y-up → Unity Y-up).
                    ship = Instantiate(prefab, pos, Quaternion.Euler(0f, left ? 0f : 180f, 0f) * prefab.transform.rotation, transform);
                    ship.name = prefab.name + "_" + i;
                    // FBX prefab roots bake in a large scale (e.g. 100) to convert cm→m.
                    // Multiply rather than overwrite so the art retains its authored proportions.
                    ship.transform.localScale = prefab.transform.localScale * s;
                    if (config.artMaterial != null)
                    {
                        foreach (var mr in ship.GetComponentsInChildren<MeshRenderer>())
                            mr.sharedMaterial = config.artMaterial;
                    }
                }
                else
                {
                    float sx = Random.Range(config.minScale.x, config.maxScale.x);
                    float sy = Random.Range(config.minScale.y, config.maxScale.y);
                    float sz = Random.Range(config.minScale.z, config.maxScale.z);
                    Color hull = PickRandomColor(config.hullPalette, Color.gray);
                    Color sail = PickRandomColor(config.sailPalette, Color.white);

                    Vector3 pos = ComputeEdgeSpawnWorldPos(cam, config, left, sy);

                    ship = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ship.name = "Ship_" + i;
                    ship.transform.SetParent(transform, false);
                    ship.transform.position = pos;
                    ship.transform.localScale = new Vector3(sx, sy, sz);
                    Strip(ship);
                    Paint(ship, shader, hull);

                    var sailGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    sailGO.name = "Sail";
                    sailGO.transform.SetParent(ship.transform, false);
                    float sailH = Random.Range(1.5f, 2.6f);
                    sailGO.transform.localPosition = new Vector3(0f, 0.55f + sailH * 0.5f / sy, 0f);
                    sailGO.transform.localScale = new Vector3(0.12f / sx, sailH / sy, Random.Range(0.5f, 0.9f));
                    Strip(sailGO);
                    Paint(sailGO, shader, sail);

                    shipHeight = sy;
                }

                var drift = ship.AddComponent<AmbientDrifter>();
                drift.followTarget = followTarget;
                drift.cam = cam;
                drift.config = config;
                drift.velocity = new Vector3(speed * (left ? 1f : -1f), 0f, 0f);
                drift.bobAmplitude = new Vector3(0f, config.bobAmplitudeY, 0f);
                drift.bobFrequency = config.bobFrequency;
                drift.spawnLeft = left;
                drift.shipHeight = shipHeight;
            }
        }

        /// <summary>Projects a viewport ray onto the water plane and nudges it just
        /// outside the left/right viewport edge so a ship spawns visibly offscreen.</summary>
        public static Vector3 ComputeEdgeSpawnWorldPos(Camera cam, AmbientShipsConfig cfg, bool left, float shipHeight)
        {
            float vpY = Random.Range(cfg.viewportYMin, cfg.viewportYMax);
            float vpX = left ? 0f : 1f;
            Vector3 edge = ViewportToWater(cam, new Vector3(vpX, vpY, 0f), cfg.waterY);
            edge.x += left ? -cfg.edgeBufferX : cfg.edgeBufferX;
            edge.y = cfg.waterY + shipHeight * 0.5f;
            return edge;
        }

        public static Vector3 ViewportToWater(Camera cam, Vector3 viewportPoint, float waterY)
        {
            Ray r = cam.ViewportPointToRay(viewportPoint);
            float denom = r.direction.y;
            if (Mathf.Abs(denom) < 1e-5f) return r.origin + r.direction * 100f;
            float t = (waterY - r.origin.y) / denom;
            // Ray points up (above horizon) — pick a sensible far distance.
            if (t < 0f) t = 200f;
            return r.origin + r.direction * t;
        }

        static Color PickRandomColor(Color[] palette, Color fallback)
        {
            if (palette == null || palette.Length == 0) return fallback;
            return palette[Random.Range(0, palette.Length)];
        }

        static void Strip(GameObject g)
        {
            var c = g.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        static void Paint(GameObject g, Shader s, Color c)
        {
            var mr = g.GetComponent<MeshRenderer>();
            if (mr == null) return;
            var m = new Material(s);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.color = c;
            mr.sharedMaterial = m;
        }
    }
}
