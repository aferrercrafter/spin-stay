using UnityEngine;
using UnityEngine.UI;

namespace SpinStay
{
    /// <summary>
    /// A 3D cloud that sweeps in walker-relative space from one side to the other,
    /// guaranteed to cross the walker's column at midpoint. On contact it "explodes"
    /// with a cartoon puff ParticleSystem and triggers a smooth-eased white screen
    /// fade-in / hold / fade-out + balance-bar dim.
    /// </summary>
    public class CloudEvent : MonoBehaviour
    {
        enum Phase { Approach, Fading }

        CloudEventConfig config;
        TightropeWalker walker;
        RawImage overlay;
        BalanceBarUI balanceBar;
        Transform cloudVisual;
        bool ownVisual;            // true if we created the cloud visual ourselves and must destroy it on cleanup

        int sideSign = 1;
        float sweepProgress;       // 0..1 across the sideways sweep
        float sweepDuration;
        Phase phase = Phase.Approach;
        float fadeElapsed;
        bool exploded;

        public void Launch(CloudEventConfig cfg, TightropeWalker w, RawImage screenOverlay, BalanceBarUI bar, Transform cloudObj)
        {
            config = cfg;
            walker = w;
            overlay = screenOverlay;
            balanceBar = bar;
            cloudVisual = cloudObj;

            // Visual selection priority: configured prefab > scene-wired cloudVisual > procedural fallback.
            // The prefab path takes priority so a freshly authored cloud asset is used even when the scene
            // still has the legacy cube CloudVisual hooked up.
            if (config.cloudPrefab != null)
            {
                var inst = Instantiate(config.cloudPrefab);
                inst.name = config.cloudPrefab.name + "(Event)";
                cloudVisual = inst.transform;
                cloudVisual.localScale = config.cloudPrefab.transform.localScale * config.cloudPrefabScale;
                ownVisual = true;
            }
            else if (cloudVisual == null)
            {
                cloudVisual = BuildFallbackCloud(config);
                ownVisual = true;
            }
            else
            {
                cloudVisual.localScale = config.cloudScale;
            }

            sideSign = Random.value < 0.5f ? -1 : 1;
            sweepProgress = 0f;
            sweepDuration = Mathf.Max(0.4f, (config.sidewaysRange * 2f) / Mathf.Max(0.05f, config.cloudSpeed));

            cloudVisual.gameObject.SetActive(true);
            cloudVisual.position = ComputeWorldPos(0f);

            if (overlay != null)
                overlay.color = new Color(config.overlayColor.r, config.overlayColor.g, config.overlayColor.b, 0f);
            if (balanceBar != null)
                balanceBar.SetAlphaMultiplier(1f);

            Debug.Log(string.Format(
                "[CloudEvent] launched  sweepDuration={0:F2}s  hitRadius={1:F1}m  side={2}",
                sweepDuration, config.hitRadius, sideSign));
        }

        Vector3 ComputeWorldPos(float t)
        {
            // Walker-relative sweep: at t=0 cloud is on one side, t=0.5 it's at the walker, t=1 it's on the other side.
            // Tracking the walker's CURRENT position guarantees the cloud crosses the walker's column even though the
            // walker is constantly moving forward.
            Vector3 wPos = walker.transform.position;
            Vector3 fwd = walker.transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;
            float lateral = Mathf.Lerp(config.sidewaysRange, -config.sidewaysRange, t) * sideSign;
            return wPos
                + fwd * config.aheadOffset
                + side * lateral
                + Vector3.up * config.verticalOffset;
        }

        void Update()
        {
            if (config == null) { Cleanup(); return; }

            if (phase == Phase.Approach)
            {
                sweepProgress += Time.deltaTime / sweepDuration;
                float t = Mathf.Clamp01(sweepProgress);
                if (cloudVisual != null) cloudVisual.position = ComputeWorldPos(t);

                bool hit = false;
                if (!exploded && walker != null && !walker.IsFallen && cloudVisual != null)
                {
                    Vector3 walkerHead = walker.transform.position + Vector3.up * config.verticalOffset;
                    if (Vector3.Distance(cloudVisual.position, walkerHead) <= config.hitRadius)
                        hit = true;
                }

                if (hit)
                {
                    exploded = true;
                    // Centre the puff burst on the walker, not where the cloud's pivot happened to be on contact.
                    Vector3 boomPos = walker.transform.position + Vector3.up * config.verticalOffset;
                    SpawnExplosion(boomPos);
                    HideCloudVisual();
                    Debug.Log("[CloudEvent] HIT walker — explosion + screen fade started.");
                    phase = Phase.Fading;
                    fadeElapsed = 0f;
                    return;
                }

                if (sweepProgress >= 1f)
                {
                    Debug.Log("[CloudEvent] missed walker (sweep finished without contact).");
                    Cleanup();
                }
                return;
            }

            // Fading: smooth-eased fade in → hold → fade out for a more gradual ramp at both ends.
            fadeElapsed += Time.deltaTime;
            float fIn  = Mathf.Max(0.001f, config.fadeIn);
            float hold = Mathf.Max(0f,    config.duration);
            float fOut = Mathf.Max(0.001f, config.fadeOut);
            float total = fIn + hold + fOut;

            float linear;
            if      (fadeElapsed < fIn)         linear = fadeElapsed / fIn;
            else if (fadeElapsed < fIn + hold)  linear = 1f;
            else if (fadeElapsed < total)       linear = 1f - (fadeElapsed - fIn - hold) / fOut;
            else                                linear = 0f;

            float weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(linear));

            if (overlay != null)
            {
                var c = config.overlayColor;
                overlay.color = new Color(c.r, c.g, c.b, weight * config.overlayAlpha);
            }
            if (balanceBar != null)
                balanceBar.SetAlphaMultiplier(Mathf.Lerp(1f, config.balanceBarAlpha, weight));

            if (fadeElapsed >= total) Cleanup();
        }

        void HideCloudVisual()
        {
            if (cloudVisual == null) return;
            if (ownVisual)
            {
                Destroy(cloudVisual.gameObject);
                cloudVisual = null;
            }
            else
            {
                cloudVisual.gameObject.SetActive(false);
            }
        }

        void Cleanup()
        {
            if (overlay != null && config != null)
                overlay.color = new Color(config.overlayColor.r, config.overlayColor.g, config.overlayColor.b, 0f);
            if (balanceBar != null) balanceBar.SetAlphaMultiplier(1f);
            HideCloudVisual();
            Destroy(gameObject);
        }

        static Transform BuildFallbackCloud(CloudEventConfig cfg)
        {
            // Three overlapping spheres for a quick puff silhouette.
            var root = new GameObject("CloudEventVisual_Auto").transform;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            for (int i = 0; i < 3; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = "Puff" + i;
                var col = s.GetComponent<Collider>(); if (col != null) Destroy(col);
                s.transform.SetParent(root, false);
                s.transform.localPosition = new Vector3((i - 1) * 0.6f, i == 1 ? 0.25f : 0f, 0f);
                s.transform.localScale = Vector3.one * (i == 1 ? 1.2f : 1f);
                var mr = s.GetComponent<MeshRenderer>();
                if (mr != null && shader != null)
                {
                    var mat = new Material(shader);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cfg.cloudColor);
                    if (mat.HasProperty("_Color")) mat.color = cfg.cloudColor;
                    mr.sharedMaterial = mat;
                }
            }
            return root;
        }

        static Texture2D s_puffTexture;
        static Material s_puffMaterial;

        static Texture2D GetPuffTexture()
        {
            if (s_puffTexture != null) return s_puffTexture;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float c = (size - 1) * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a); // smoothstep falloff
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            s_puffTexture = tex;
            return tex;
        }

        static Material GetPuffMaterial()
        {
            if (s_puffMaterial != null) return s_puffMaterial;
            // Prefer URP particles shader; fall back to legacy particle shaders, then Sprites/Default.
            // Sprites/Default reliably blends with vertex color × _MainTex alpha in any pipeline.
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Particles/Standard Unlit")
                      ?? Shader.Find("Mobile/Particles/Alpha Blended")
                      ?? Shader.Find("Sprites/Default");
            if (shader == null) return null;
            var mat = new Material(shader);
            if (mat.HasProperty("_MainTex"))  mat.SetTexture("_MainTex", GetPuffTexture());
            if (mat.HasProperty("_BaseMap"))  mat.SetTexture("_BaseMap", GetPuffTexture());
            // URP particles shader: surface type 1 = Transparent, blend mode 0 = Alpha.
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend"))   mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_ZWrite"))  mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = 3000;
            mat.hideFlags = HideFlags.HideAndDontSave;
            s_puffMaterial = mat;
            return mat;
        }

        void SpawnExplosion(Vector3 worldPos)
        {
            var go = new GameObject("CloudExplosion");
            go.transform.position = worldPos;

            var ps = go.AddComponent<ParticleSystem>();
            var pr = go.GetComponent<ParticleSystemRenderer>();

            // Assign a guaranteed-visible material BEFORE configuring modules. Without a valid shader
            // on the renderer, Unity silently culls the puffs and the explosion appears to "do nothing".
            var puffMat = GetPuffMaterial();
            if (puffMat != null) pr.sharedMaterial = puffMat;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.alignment = ParticleSystemRenderSpace.View;

            var emission = ps.emission;
            emission.enabled = false;

            var main = ps.main;
            main.duration = config.explosionDuration;
            main.loop = false;
            main.startLifetime = config.explosionDuration;
            main.startSpeed = config.explosionSpeed;
            main.startSize = config.explosionPuffSize;
            main.startColor = config.explosionColor;
            main.gravityModifier = -0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = config.explosionParticleCount * 2;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.05f, config.explosionPuffSize * 0.5f);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.6f),
                new Keyframe(0.25f, 1.2f),
                new Keyframe(1f, 0.0f));
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(config.explosionColor, 0f), new GradientColorKey(config.explosionColor, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(grad);

            var velOverLife = ps.velocityOverLifetime;
            velOverLife.enabled = true;
            velOverLife.space = ParticleSystemSimulationSpace.World;
            velOverLife.speedModifier = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.05f)));

            ps.Emit(config.explosionParticleCount);

            Debug.Log(string.Format(
                "[CloudEvent] explosion emitted {0} puffs at {1}  (shader={2})",
                config.explosionParticleCount, worldPos, puffMat != null ? puffMat.shader.name : "<null>"));

            Destroy(go, config.explosionDuration + 0.5f);
        }
    }
}
