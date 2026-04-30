using UnityEngine;

namespace SpinStay
{
    /// <summary>
    /// Drifts an object linearly in world space and recycles it when it exits the
    /// camera viewport, teleporting it back to the opposite camera edge on the water
    /// plane so the view always has a ship entering from one side.
    /// </summary>
    public class AmbientDrifter : MonoBehaviour
    {
        public Transform followTarget;
        public Camera cam;
        public AmbientShipsConfig config;

        public Vector3 velocity = new Vector3(2f, 0f, 0f);
        public Vector3 bobAmplitude;
        public float bobFrequency = 0.5f;

        [Header("Camera-edge recycle")]
        [Tooltip("Which camera edge the ship originally spawned at (true = left). Drives which edge we recycle back to.")]
        public bool spawnLeft;
        [Tooltip("Ship height (world Y scale). Used to seat the recycled position on the water plane.")]
        public float shipHeight = 1f;

        [Tooltip("Extra viewport X past 1 / below 0 before recycling, so ships fully exit the view first.")]
        [Range(0f, 0.5f)] public float viewportExitBuffer = 0.12f;

        [Header("Legacy fallback (if cam / config are null)")]
        public float recycleRadius = 200f;
        public Vector3 recycleOffset = new Vector3(-120f, 0f, 0f);

        Vector3 basePos;
        float phase;

        void Start()
        {
            basePos = transform.position;
            phase = Random.Range(0f, 10f);
        }

        void Update()
        {
            basePos += velocity * Time.deltaTime;

            float s = Mathf.Sin((Time.time + phase) * bobFrequency * Mathf.PI * 2f);
            transform.position = basePos + new Vector3(bobAmplitude.x * s, bobAmplitude.y * s, bobAmplitude.z * s);

            if (ShouldRecycle()) Recycle();
        }

        bool ShouldRecycle()
        {
            if (cam != null && config != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(transform.position);
                if (vp.z < 0f) return true; // behind camera
                // Ship has crossed to the opposite side and past it by the buffer.
                if (spawnLeft  && vp.x > 1f + viewportExitBuffer) return true;
                if (!spawnLeft && vp.x < 0f - viewportExitBuffer) return true;
                return false;
            }

            if (followTarget == null) return false;
            return (transform.position - followTarget.position).magnitude > recycleRadius;
        }

        void Recycle()
        {
            if (cam != null && config != null)
            {
                basePos = AmbientShipsSpawner.ComputeEdgeSpawnWorldPos(cam, config, spawnLeft, shipHeight);
                transform.position = basePos;
                return;
            }
            if (followTarget != null)
            {
                basePos = followTarget.position + recycleOffset + new Vector3(
                    Random.Range(-10f, 10f),
                    Random.Range(-1f, 2f),
                    Random.Range(-15f, 15f));
                transform.position = basePos;
            }
        }
    }
}
