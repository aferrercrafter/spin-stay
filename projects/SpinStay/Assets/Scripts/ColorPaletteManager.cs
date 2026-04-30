using System.Collections.Generic;
using UnityEngine;

namespace SpinStay
{
    /// <summary>
    /// Runtime-only palette swap. Caches the original config colors on Start,
    /// mutates RouletteConfig / Pendulum config + BalanceBarUI to apply a preset,
    /// and restores the originals on destroy so the config .asset files stay clean.
    /// Cycled from the top-left GUI button in <see cref="GameManager"/>.
    /// </summary>
    public class ColorPaletteManager : MonoBehaviour
    {
        [SerializeField] RouletteConfig rouletteConfig;
        [SerializeField] RouletteConfig pendulumConfig;
        [SerializeField] Roulette roulette;
        [SerializeField] Pendulum pendulum;
        [SerializeField] BalanceBarUI balanceBar;

        struct ConfigSnap { public Color rim, divider, center; public Color[] opts; }
        struct BarSnap    { public Color edge, mid, center; }

        ConfigSnap originalRoulette;
        ConfigSnap originalPendulum;
        BarSnap    originalBar;
        bool       capturedPendulum;
        bool       capturedBar;
        bool       initialized;

        List<ColorPalette> palettes;
        int idx;

        public string CurrentPaletteName => palettes != null && palettes.Count > 0 ? palettes[idx].name : "-";
        public string NextPaletteName    => palettes != null && palettes.Count > 0
            ? palettes[(idx + 1) % palettes.Count].name
            : "-";

        public void Configure(RouletteConfig rouletteCfg, RouletteConfig pendulumCfg,
                              Roulette roul, Pendulum pend, BalanceBarUI bar)
        {
            rouletteConfig = rouletteCfg;
            pendulumConfig = pendulumCfg;
            roulette       = roul;
            pendulum       = pend;
            balanceBar     = bar;
        }

        void Start()
        {
            if (initialized) return;
            initialized = true;

            originalRoulette = CaptureConfig(rouletteConfig);
            if (pendulumConfig != null && pendulumConfig != rouletteConfig)
            {
                originalPendulum = CaptureConfig(pendulumConfig);
                capturedPendulum = true;
            }
            if (balanceBar != null)
            {
                originalBar = new BarSnap
                {
                    edge   = balanceBar.colorEdge,
                    mid    = balanceBar.colorMid,
                    center = balanceBar.colorCenter,
                };
                capturedBar = true;
            }

            palettes = new List<ColorPalette> { new ColorPalette { name = "Original" } };
            foreach (var p in ColorPalettes.Presets) palettes.Add(p);
            idx = 0;
        }

        void OnDestroy()
        {
            RestoreOriginal();
        }

        public void CycleNext()
        {
            if (palettes == null || palettes.Count == 0) return;
            idx = (idx + 1) % palettes.Count;
            if (idx == 0) RestoreOriginal();
            else          ApplyPreset(palettes[idx]);

            if (roulette != null) roulette.RebuildVisuals();
            if (pendulum != null) pendulum.RebuildVisuals();
        }

        void RestoreOriginal()
        {
            if (!initialized) return;
            RestoreConfig(rouletteConfig, originalRoulette);
            if (capturedPendulum) RestoreConfig(pendulumConfig, originalPendulum);
            if (capturedBar && balanceBar != null)
                balanceBar.ApplyColors(originalBar.edge, originalBar.mid, originalBar.center);
        }

        void ApplyPreset(ColorPalette p)
        {
            ApplyConfig(rouletteConfig, p);
            if (pendulumConfig != null && pendulumConfig != rouletteConfig)
                ApplyConfig(pendulumConfig, p);
            if (balanceBar != null) balanceBar.ApplyColors(p.barEdge, p.barMid, p.barCenter);
        }

        static ConfigSnap CaptureConfig(RouletteConfig cfg)
        {
            var snap = new ConfigSnap();
            if (cfg == null) return snap;
            snap.rim     = cfg.rimColor;
            snap.divider = cfg.dividerColor;
            snap.center  = cfg.centerColor;
            int n = cfg.options != null ? cfg.options.Length : 0;
            snap.opts = new Color[n];
            for (int i = 0; i < n; i++) snap.opts[i] = cfg.options[i].color;
            return snap;
        }

        static void RestoreConfig(RouletteConfig cfg, ConfigSnap snap)
        {
            if (cfg == null) return;
            cfg.rimColor     = snap.rim;
            cfg.dividerColor = snap.divider;
            cfg.centerColor  = snap.center;
            if (cfg.options != null && snap.opts != null)
            {
                int n = Mathf.Min(cfg.options.Length, snap.opts.Length);
                for (int i = 0; i < n; i++) cfg.options[i].color = snap.opts[i];
            }
        }

        static void ApplyConfig(RouletteConfig cfg, ColorPalette p)
        {
            if (cfg == null || p == null) return;
            cfg.rimColor     = p.rim;
            cfg.dividerColor = p.divider;
            cfg.centerColor  = p.center;
            if (cfg.options != null && p.optionColors != null && p.optionColors.Length > 0)
            {
                for (int i = 0; i < cfg.options.Length; i++)
                    cfg.options[i].color = p.optionColors[i % p.optionColors.Length];
            }
        }
    }
}
