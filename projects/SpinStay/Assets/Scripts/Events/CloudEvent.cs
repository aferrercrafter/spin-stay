using UnityEngine;
using UnityEngine.UI;

namespace SpinStay
{
    /// <summary>
    /// Spawns a drifting 3D cloud that passes over the walker and fades in/out a
    /// full-screen fog overlay + dims the balance bar to obscure the player's view.
    /// </summary>
    public class CloudEvent : MonoBehaviour
    {
        CloudEventConfig config;
        TightropeWalker walker;
        RawImage overlay;
        BalanceBarUI balanceBar;
        Transform cloudVisual;
        Vector3 from, to;

        float elapsed;
        float cloudTravelTime;

        public void Launch(CloudEventConfig cfg, TightropeWalker w, RawImage screenOverlay, BalanceBarUI bar, Transform cloudObj)
        {
            config = cfg;
            walker = w;
            overlay = screenOverlay;
            balanceBar = bar;
            cloudVisual = cloudObj;

            // Drift the cloud past the walker from one side to the other.
            Vector3 wPos = walker.transform.position;
            Vector3 fwd = walker.transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;
            float range = 25f;
            float height = 3.5f;
            from = wPos + fwd * 2f + side * range + Vector3.up * height;
            to   = wPos + fwd * 2f - side * range + Vector3.up * height;
            cloudTravelTime = Mathf.Max(0.1f, Vector3.Distance(from, to) / Mathf.Max(0.01f, config.cloudSpeed));

            if (cloudVisual != null)
            {
                cloudVisual.gameObject.SetActive(true);
                cloudVisual.position = from;
                cloudVisual.localScale = config.cloudScale;
            }
            if (overlay != null) overlay.color = new Color(config.overlayColor.r, config.overlayColor.g, config.overlayColor.b, 0f);
        }

        void Update()
        {
            if (config == null) { Destroy(gameObject); return; }
            elapsed += Time.deltaTime;

            // Cloud visual drift.
            if (cloudVisual != null)
            {
                float k = Mathf.Clamp01(elapsed / cloudTravelTime);
                cloudVisual.position = Vector3.Lerp(from, to, k);
            }

            // Overlay + balance-bar alpha curve: fade in → hold → fade out.
            float fIn = config.fadeIn;
            float hold = config.duration;
            float fOut = config.fadeOut;
            float total = fIn + hold + fOut;

            float weight;
            if (elapsed < fIn)                  weight = elapsed / Mathf.Max(0.001f, fIn);
            else if (elapsed < fIn + hold)      weight = 1f;
            else if (elapsed < total)           weight = 1f - (elapsed - fIn - hold) / Mathf.Max(0.001f, fOut);
            else                                weight = 0f;

            if (overlay != null)
            {
                var c = config.overlayColor;
                overlay.color = new Color(c.r, c.g, c.b, weight * config.overlayAlpha);
            }
            if (balanceBar != null)
            {
                // Lerp from 1 (clear) toward the configured dim value.
                balanceBar.SetAlphaMultiplier(Mathf.Lerp(1f, config.balanceBarAlpha, weight));
            }

            if (elapsed >= total)
            {
                if (overlay != null) overlay.color = new Color(config.overlayColor.r, config.overlayColor.g, config.overlayColor.b, 0f);
                if (balanceBar != null) balanceBar.SetAlphaMultiplier(1f);
                if (cloudVisual != null) cloudVisual.gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }
    }
}
