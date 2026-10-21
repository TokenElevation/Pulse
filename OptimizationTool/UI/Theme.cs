using System;
using System.Drawing;

namespace OptimizationTool.UI
{
    public sealed record ThemePreset(string Name, Color Accent, Color Tint, float TintAmount, bool UseAccent,
                                     Color? Accent2 = null);

    public static class Theme
    {
        public static readonly ThemePreset[] Presets =
        {
            new("Pulse",   Color.FromArgb(0, 214, 255),   Color.FromArgb(30, 80, 255),  0.10f, true,
                           Color.FromArgb(24, 70, 255)),
            new("Mono",    Color.FromArgb(160, 160, 160), Color.White,                  0f,    false),
            new("Violet",  Color.FromArgb(139, 92, 246),  Color.FromArgb(139, 92, 246), 0.10f, true),
            new("Ocean",   Color.FromArgb(56, 189, 248),  Color.FromArgb(56, 140, 210), 0.10f, true),
            new("Crimson", Color.FromArgb(239, 68, 68),   Color.FromArgb(200, 50, 60),  0.09f, true),
            new("Emerald", Color.FromArgb(52, 211, 153),  Color.FromArgb(40, 160, 110), 0.09f, true),
        };

        public static ThemePreset Current { get; private set; } = Presets[0];

        public static event EventHandler? Changed;

        // fixed status colors same in every theme
        public static readonly Color Danger = Color.FromArgb(214, 72, 72);
        public static readonly Color Warning = Color.FromArgb(232, 180, 60);

        // window
        public static Color WindowBg { get; private set; }
        public static Color WindowGradientStart { get; private set; }
        public static Color WindowGradientEnd { get; private set; }

        public static Color Accent { get; private set; }
        public static Color GlowDeep { get; private set; }
        public static Color GlowLight { get; private set; }

        // panels (dock, settings rows, dialogs)
        public static Color Surface { get; private set; }
        public static Color SurfaceBorder { get; private set; }

        // modules(cards)
        public static Color Card { get; private set; }
        public static Color CardHover { get; private set; }
        public static Color CardOn { get; private set; }
        public static Color CardOnHover { get; private set; }

        public static Color Border { get; private set; }
        public static Color BorderHover { get; private set; }
        public static Color BorderOn { get; private set; }

        // text
        public static Color TextPrimary { get; private set; }
        public static Color TextTitle { get; private set; }
        public static Color TextSecondary { get; private set; }
        public static Color TextMuted { get; private set; }

        // tabs and secondary buttons
        public static Color TabOffTop { get; private set; }
        public static Color TabOffBottom { get; private set; }
        public static Color TabOffText { get; private set; }
        public static Color TabOnTop { get; private set; }
        public static Color TabOnBottom { get; private set; }
        public static Color TabOnText { get; private set; }
        public static Color TabOnBorder { get; private set; }

        // title sheen
        public static Color SheenStart { get; private set; }
        public static Color SheenMid { get; private set; }
        public static Color SheenEnd { get; private set; }

        static Theme() => Apply(Presets[0], notify: false);

        public static ThemePreset FindPreset(string? name) =>
            Array.Find(Presets, p => p.Name == name) ?? Presets[0];

        public static void Apply(ThemePreset p, bool notify = true)
        {
            Current = p;

            Color N(int g)
            {
                float amount = p.TintAmount * (g < 80 ? 1f : 0.35f);
                return Lerp(Color.FromArgb(g, g, g), p.Tint, amount);
            }

            Color A(Color c, float w) => p.UseAccent ? Lerp(c, p.Accent, w) : c;

            WindowBg = Color.Black;
            WindowGradientStart = N(38);
            WindowGradientEnd = Color.Black;

            Accent = p.Accent;
            GlowLight = p.Accent;
            GlowDeep = p.Accent2 ?? Lerp(p.Accent, Color.Black, p.UseAccent ? 0.40f : 0.55f);

            Surface = N(12);
            SurfaceBorder = N(46);

            Card = N(14);
            CardHover = N(20);
            CardOn = A(N(22), 0.16f);
            CardOnHover = A(N(28), 0.20f);

            Border = N(38);
            BorderHover = N(62);
            BorderOn = p.UseAccent ? Lerp(N(100), p.Accent, 0.8f) : Color.FromArgb(130, 130, 130);

            TextPrimary = Color.FromArgb(255, 255, 255);
            TextTitle = Color.FromArgb(255, 255, 255);
            TextSecondary = Color.FromArgb(215, 220, 230);
            TextMuted = Color.FromArgb(140, 150, 165);

            TabOffTop = N(25);
            TabOffBottom = N(10);
            TabOffText = N(125);

            if (p.Accent2 != null)
            {
                TabOnTop = Lerp(N(30), p.Accent, 0.55f);
                TabOnBottom = Lerp(N(20), p.Accent2.Value, 0.90f);
                TabOnText = Color.White;
                TabOnBorder = Lerp(N(100), p.Accent, 0.85f);

                SheenStart = Color.White;
                SheenMid = Lerp(Color.White, p.Accent, 0.55f);
                SheenEnd = Lerp(p.Accent2.Value, Color.White, 0.20f);
            }
            else
            {
                TabOnTop = A(N(58), 0.45f);
                TabOnBottom = A(N(38), 0.40f);
                TabOnText = A(N(200), 0.30f);
                TabOnBorder = A(N(100), 0.70f);

                SheenStart = Color.White;
                SheenMid = A(N(155), 0.25f);
                SheenEnd = A(N(58), 0.30f);
            }

            if (notify) Changed?.Invoke(null, EventArgs.Empty);
        }

        public static Color Lerp(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }
}