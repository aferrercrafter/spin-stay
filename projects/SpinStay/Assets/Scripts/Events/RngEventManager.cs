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

        // Manual-trigger pending delays. >0 = counting down toward firing.
        float manualBirdTimer  = -1f;
        float manualCloudTimer = -1f;

        public bool BirdTriggerPending  => manualBirdTimer  > 0f;
        public bool CloudTriggerPending => manualCloudTimer > 0f;
        public float BirdTriggerRemaining  => Mathf.Max(0f, manualBirdTimer);
        public float CloudTriggerRemaining => Mathf.Max(0f, manualCloudTimer);

        /// <summary>Queue an event-bird spawn after <paramref name="delaySeconds"/>. Used by the manual trigger button.</summary>
        public void TriggerBirdNow(float delaySeconds = 1f)
        {
            if (birdConfig == null) return;
            manualBirdTimer = Mathf.Max(0.001f, delaySeconds);
        }

        /// <summary>Queue an event-cloud spawn after <paramref name="delaySeconds"/>. Used by the manual trigger button.</summary>
        public void TriggerCloudNow(float delaySeconds = 1f)
        {
            if (cloudConfig == null) return;
            manualCloudTimer = Mathf.Max(0.001f, delaySeconds);
        }

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

            if (manualBirdTimer > 0f)
            {
                manualBirdTimer -= Time.deltaTime;
                if (manualBirdTimer <= 0f)
                {
                    manualBirdTimer = -1f;
                    if (birdConfig != null) SpawnBird();
                }
            }
            if (manualCloudTimer > 0f)
            {
                manualCloudTimer -= Time.deltaTime;
                if (manualCloudTimer <= 0f)
                {
                    manualCloudTimer = -1f;
                    if (cloudConfig != null) SpawnCloud();
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
