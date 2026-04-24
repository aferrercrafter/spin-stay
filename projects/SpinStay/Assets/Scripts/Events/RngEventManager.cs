using UnityEngine;
using UnityEngine.UI;

namespace SpinStay
{
    public class RngEventManager : MonoBehaviour
    {
        [SerializeField] private TightropeWalker walker;

        [Header("Bird")]
        [SerializeField] private BirdEventConfig birdConfig;

        [Header("Cloud")]
        [SerializeField] private CloudEventConfig cloudConfig;
        [Tooltip("Full-screen overlay image used to dim the view during a cloud event.")]
        [SerializeField] private RawImage cloudOverlay;
        [SerializeField] private BalanceBarUI balanceBar;
        [Tooltip("A reusable 3D cloud visual (a scaled cube/sphere). Will be toggled on/off by CloudEvent.")]
        [SerializeField] private Transform cloudVisual;

        float birdTimer;
        float cloudTimer;

        void Start()
        {
            ScheduleBird();
            ScheduleCloud();
            if (cloudVisual != null) cloudVisual.gameObject.SetActive(false);
        }

        void Update()
        {
            if (walker == null || walker.IsFallen) return;

            if (birdConfig != null)
            {
                birdTimer -= Time.deltaTime;
                if (birdTimer <= 0f)
                {
                    SpawnBird();
                    ScheduleBird();
                }
            }

            if (cloudConfig != null)
            {
                cloudTimer -= Time.deltaTime;
                if (cloudTimer <= 0f)
                {
                    SpawnCloud();
                    ScheduleCloud();
                }
            }
        }

        void ScheduleBird()
        {
            if (birdConfig == null) return;
            birdTimer = Random.Range(birdConfig.minInterval, birdConfig.maxInterval);
        }

        void ScheduleCloud()
        {
            if (cloudConfig == null) return;
            cloudTimer = Random.Range(cloudConfig.minInterval, cloudConfig.maxInterval);
        }

        void SpawnBird()
        {
            GameObject go;
            Quaternion baseRot = Quaternion.identity;
            if (birdConfig.birdPrefabs != null && birdConfig.birdPrefabs.Length > 0)
            {
                var prefab = birdConfig.birdPrefabs[Random.Range(0, birdConfig.birdPrefabs.Length)];
                go = Instantiate(prefab);
                go.name = prefab.name + "(Event)";
                float s = Random.Range(birdConfig.prefabScaleMin, birdConfig.prefabScaleMax);
                // FBX prefab roots bake in a large scale (cm→m). Multiply, don't overwrite.
                go.transform.localScale = prefab.transform.localScale * s;
                baseRot = prefab.transform.rotation;
                if (birdConfig.artMaterial != null)
                {
                    foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
                        mr.sharedMaterial = birdConfig.artMaterial;
                }
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Bird(Event)";
                var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var mat = new Material(GetShader());
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", birdConfig.color);
                    if (mat.HasProperty("_Color")) mat.color = birdConfig.color;
                    mr.sharedMaterial = mat;
                }
                go.transform.localScale = birdConfig.scale;
            }
            var bird = go.AddComponent<BirdEvent>();
            bird.baseRotation = baseRot;
            bird.Launch(birdConfig, walker);
        }

        void SpawnCloud()
        {
            var go = new GameObject("CloudEvent");
            var cloud = go.AddComponent<CloudEvent>();
            cloud.Launch(cloudConfig, walker, cloudOverlay, balanceBar, cloudVisual);
        }

        static Shader GetShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Hidden/InternalErrorShader");
        }
    }
}
