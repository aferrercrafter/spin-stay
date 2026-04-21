using UnityEngine;

namespace SpinStay
{
    /// <summary>
    /// Generic drifting object (ship, cloud, bird). Moves forever along `velocity`
    /// in world space, optionally bobbing vertically, and teleports back when it
    /// drifts too far from the walker so we get an endless feeling without pooling.
    /// </summary>
    public class AmbientDrifter : MonoBehaviour
    {
        public Transform followTarget;
        public Vector3 velocity = new Vector3(2f, 0f, 0f);
        public Vector3 bobAmplitude;
        public float bobFrequency = 0.5f;

        [Header("Recycling")]
        public float recycleRadius = 150f;
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

            if (followTarget != null)
            {
                Vector3 d = transform.position - followTarget.position;
                if (d.magnitude > recycleRadius)
                {
                    basePos = followTarget.position + recycleOffset + new Vector3(
                        Random.Range(-10f, 10f),
                        Random.Range(-1f, 2f),
                        Random.Range(-15f, 15f));
                }
            }
        }
    }
}
