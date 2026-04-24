using UnityEngine;

namespace SpinStay
{
    /// <summary>
    /// One-shot bird that flies across in front of the walker and applies a balance impulse
    /// when it passes through the walker's area.
    /// </summary>
    public class BirdEvent : MonoBehaviour
    {
        BirdEventConfig config;
        TightropeWalker walker;
        Vector3 from, to;
        float t;
        float totalTime;
        bool dealtDamage;
        int signDir;

        public void Launch(BirdEventConfig cfg, TightropeWalker w)
        {
            config = cfg;
            walker = w;
            signDir = Random.value < 0.5f ? -1 : 1;
            // Spawn off to one side at walker's height + offset, cross to the opposite side.
            Vector3 wPos = walker.transform.position;
            Vector3 fwd = walker.transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;
            float ahead = 3f;
            from = wPos + fwd * ahead + side * config.sidewaysRange * signDir + Vector3.up * config.verticalOffset;
            to   = wPos + fwd * ahead - side * config.sidewaysRange * signDir + Vector3.up * config.verticalOffset;
            totalTime = Mathf.Max(0.1f, Vector3.Distance(from, to) / Mathf.Max(0.1f, config.speed));
            transform.position = from;
            transform.forward = (to - from).normalized;
        }

        void Update()
        {
            if (config == null || walker == null) { Destroy(gameObject); return; }
            t += Time.deltaTime;
            float k = t / totalTime;
            transform.position = Vector3.Lerp(from, to, k);

            if (!dealtDamage && walker != null && !walker.IsFallen)
            {
                float dist = Vector3.Distance(transform.position, walker.transform.position + Vector3.up * config.verticalOffset);
                if (dist < config.hitRadius)
                {
                    dealtDamage = true;
                    float mag = Random.Range(config.impulseMin, config.impulseMax);
                    // Raw disturbance: bird strike adds a sudden angular velocity kick.
                    float dir = Random.value < 0.5f ? -1f : 1f;
                    walker.ApplyAngularImpulse(dir * mag);
                }
            }

            if (k >= 1f) Destroy(gameObject);
        }
    }
}
