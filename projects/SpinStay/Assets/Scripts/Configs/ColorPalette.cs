using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpinStay
{
    [Serializable]
    public class ColorPalette
    {
        public string name;
        public Color rim;
        public Color divider;
        public Color center;
        public Color barEdge;
        public Color barMid;
        public Color barCenter;
        public Color[] optionColors;
    }

    public static class ColorPalettes
    {
        static Color H(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        // Option colors are applied to config.options by index (wrapping). Current
        // option order in RouletteConfig.asset: LEFT, RIGHT, RESET, FAST, FALL — so
        // each preset lists five colors in that order.
        public static IReadOnlyList<ColorPalette> Presets { get; } = new[]
        {
            new ColorPalette
            {
                name      = "Casino Royale",
                rim       = H("#E4A700"),
                divider   = H("#FFFFFF"),
                center    = H("#000000"),
                barEdge   = H("#C70000"),
                barMid    = H("#E4A700"),
                barCenter = H("#135638"),
                optionColors = new[]
                {
                    H("#C70000"), // LEFT  — red
                    H("#072475"), // RIGHT — deep blue
                    H("#135638"), // RESET — felt green
                    H("#E4A700"), // FAST  — amber
                    H("#7B1414"), // FALL  — dark red
                },
            },
            new ColorPalette
            {
                name      = "Velvet Lounge",
                rim       = H("#E6A955"),
                divider   = H("#F3EAC3"),
                center    = H("#1A1A1A"),
                barEdge   = H("#74212A"),
                barMid    = H("#E6A955"),
                barCenter = H("#2F4C6B"),
                optionColors = new[]
                {
                    H("#74212A"), // LEFT  — oxblood
                    H("#2F4C6B"), // RIGHT — navy
                    H("#E6A955"), // RESET — warm orange
                    H("#F3EAC3"), // FAST  — cream
                    H("#1A1A1A"), // FALL  — near-black
                },
            },
            new ColorPalette
            {
                name      = "Neon Noir",
                rim       = H("#FF2FA8"),
                divider   = H("#34F3FF"),
                center    = H("#0B0B18"),
                barEdge   = H("#FF2FA8"),
                barMid    = H("#FFD23F"),
                barCenter = H("#34F3FF"),
                optionColors = new[]
                {
                    H("#FF2FA8"), // LEFT  — hot pink
                    H("#34F3FF"), // RIGHT — cyan
                    H("#8AE94C"), // RESET — electric green
                    H("#FFD23F"), // FAST  — yellow
                    H("#5B1A5A"), // FALL  — deep purple
                },
            },
            new ColorPalette
            {
                name      = "Vintage Parlor",
                rim       = H("#BF8A3A"),
                divider   = H("#F0E0B8"),
                center    = H("#2E1A0F"),
                barEdge   = H("#8B2A1E"),
                barMid    = H("#D8A64B"),
                barCenter = H("#3E6B4A"),
                optionColors = new[]
                {
                    H("#8B2A1E"), // LEFT  — burgundy
                    H("#3E6B4A"), // RIGHT — muted green
                    H("#D8A64B"), // RESET — aged gold
                    H("#1F3A5F"), // FAST  — midnight blue
                    H("#2E1A0F"), // FALL  — dark walnut
                },
            },
            new ColorPalette
            {
                name      = "Monochrome",
                rim       = H("#E5E5E5"),
                divider   = H("#FFFFFF"),
                center    = H("#0A0A0A"),
                barEdge   = H("#1A1A1A"),
                barMid    = H("#9A9A9A"),
                barCenter = H("#EAEAEA"),
                optionColors = new[]
                {
                    H("#1C1C1C"), // LEFT
                    H("#D8D8D8"), // RIGHT
                    H("#6E6E6E"), // RESET
                    H("#B4B4B4"), // FAST
                    H("#000000"), // FALL
                },
            },
        };
    }
}
