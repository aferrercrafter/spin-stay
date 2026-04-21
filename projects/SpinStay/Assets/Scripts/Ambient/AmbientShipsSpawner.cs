using UnityEngine;

namespace SpinStay
{
    /// <summary>Spawns N ambient ships, each randomized per AmbientShipsConfig, parented under this transform.</summary>
    public class AmbientShipsSpawner : MonoBehaviour
    {
        [SerializeField] private AmbientShipsConfig config;
        [SerializeField] private Transform followTarget;

        void Start()
        {
            if (config == null || followTarget == null) return;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            for (int i = 0; i < config.count; i++)
            {
                float sx = Random.Range(config.minScale.x, config.maxScale.x);
                float sy = Random.Range(config.minScale.y, config.maxScale.y);
                float sz = Random.Range(config.minScale.z, config.maxScale.z);
                bool left = Random.value < 0.5f;
                float side = Random.Range(config.sideOffsetMin, config.sideOffsetMax) * (left ? -1f : 1f);
                float z = followTarget.position.z + Random.Range(-config.zSpread * 0.5f, config.zSpread * 1.5f);
                float speed = Random.Range(config.minSpeed, config.maxSpeed);
                Color hull = PickRandomColor(config.hullPalette, Color.gray);
                Color sail = PickRandomColor(config.sailPalette, Color.white);

                var ship = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ship.name = "Ship_" + i;
                ship.transform.SetParent(transform, false);
                ship.transform.position = new Vector3(side, config.waterY + sy * 0.5f, z);
                ship.transform.localScale = new Vector3(sx, sy, sz);
                Strip(ship);
                Paint(ship, shader, hull);

                // Sail (child cube).
                var sailGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sailGO.name = "Sail";
                sailGO.transform.SetParent(ship.transform, false);
                float sailH = Random.Range(1.5f, 2.6f);
                sailGO.transform.localPosition = new Vector3(0f, 0.55f + sailH * 0.5f / sy, 0f);
                sailGO.transform.localScale = new Vector3(0.12f / sx, sailH / sy, Random.Range(0.5f, 0.9f));
                Strip(sailGO);
                Paint(sailGO, shader, sail);

                var drift = ship.AddComponent<AmbientDrifter>();
                drift.followTarget = followTarget;
                drift.velocity = new Vector3(speed * (left ? 1f : -1f), 0f, 0f); // drift inward-ish
                drift.bobAmplitude = new Vector3(0f, config.bobAmplitudeY, 0f);
                drift.bobFrequency = config.bobFrequency;
                drift.recycleRadius = Mathf.Max(150f, config.zSpread);
                drift.recycleOffset = new Vector3(left ? -60f : 60f, 0f, -config.zSpread * 0.5f);
            }
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
