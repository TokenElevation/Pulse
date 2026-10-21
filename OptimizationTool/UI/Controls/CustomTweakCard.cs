using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.UI.Controls
{
    public enum ActionState { Idle, Running, Success, Failed }
    internal static class PulseClock
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmFlush();

        private static readonly Stopwatch Sw = Stopwatch.StartNew();
        private static readonly ManualResetEventSlim Gate = new(false);
        private static Thread? thread;
        private static SynchronizationContext? ui;
        private static int pending;
        private static EventHandler? tick;
        private static int pauseCount;

        private static System.Windows.Forms.Timer? fallback;

        public static float Seconds => (float)Sw.Elapsed.TotalSeconds;

        public static void Pause() => Interlocked.Increment(ref pauseCount);

        public static void Resume()
        {
            if (Volatile.Read(ref pauseCount) > 0) Interlocked.Decrement(ref pauseCount);
        }

        public static event EventHandler? Tick
        {
            add { tick += value; Start(); }
            remove
            {
                tick -= value;
                if (tick == null)
                {
                    Gate.Reset();
                    fallback?.Stop();
                }
            }
        }

        private static void Start()
        {
            ui ??= SynchronizationContext.Current;

            if (ui == null)
            {
                if (fallback == null)
                {
                    fallback = new System.Windows.Forms.Timer { Interval = 8 };
                    fallback.Tick += (s, e) => { if (Volatile.Read(ref pauseCount) == 0) tick?.Invoke(null, EventArgs.Empty); };
                }
                if (!fallback.Enabled) fallback.Start();
                return;
            }

            if (thread == null)
            {
                thread = new Thread(Loop) { IsBackground = true, Name = "PulseVsync" };
                thread.Start();
            }
            Gate.Set();
        }

        private static void Loop()
        {
            while (true)
            {
                Gate.Wait();

                int hr;
                try { hr = DwmFlush(); }
                catch { hr = -1; }
                if (hr != 0) Thread.Sleep(8);

                if (Interlocked.Exchange(ref pending, 1) == 0)
                {
                    ui!.Post(_ =>
                    {
                        Interlocked.Exchange(ref pending, 0);
                        if (Volatile.Read(ref pauseCount) == 0) tick?.Invoke(null, EventArgs.Empty);
                    }, null);
                }
            }
        }
    }
    public class CustomTweakCard : Control
    {
        private const int Pad = 16;
        private const int DescTop = 60;

        // risk tag drawn right after the title of risky modules
        private const string RiskText = "(Risk)";
        private const int RiskGap = 5;
        private static readonly Color RiskColor = Color.FromArgb(255, 107, 107);

        private static Color PulseCyan => Theme.GlowLight;
        private static Color PulseBlue => Theme.GlowDeep;
        private static readonly Color FlatLine = Color.FromArgb(120, 140, 200);
        private static readonly Color RiskDeep = Color.FromArgb(200, 36, 60);
        private static readonly Color RiskLight = Color.FromArgb(255, 120, 110);
        private static readonly Color OkDeep = Color.FromArgb(22, 150, 95);
        private static readonly Color OkLight = Color.FromArgb(70, 230, 160);
        private static readonly Color ErrDeep = Color.FromArgb(180, 40, 55);
        private static readonly Color ErrLight = Color.FromArgb(255, 105, 105);
        private static readonly Color SwitchOffTrack = Color.FromArgb(24, 30, 44);
        private static readonly Color SwitchOffKnob = Color.FromArgb(150, 160, 182);

        // timing
        private const float BeatPeriod = 1.6f;
        private const float EcgPeriod = 3.6f;
        private const float RunningPeriod = 1.3f;
        private const float BlipWidth = 120f;
        private const float BlipAmp = 22f;

        public string CardNumber { get; set; } = "01";
        public ITweak? AssociatedTweak { get; private set; }
        public event EventHandler? TweakClicked;

        private bool isApplied;
        public bool IsApplied
        {
            get => isApplied;
            set
            {
                if (AssociatedTweak?.IsAction == true) return;
                isApplied = value;
                appliedT.Go(value ? 1f : 0f);
                if (value) SubscribeClock();
            }
        }

        private bool isRisky;
        public bool IsRisky
        {
            get => isRisky;
            set
            {
                if (isRisky == value) return;
                isRisky = value;
                Invalidate();
            }
        }

        private string title = "";
        public string Title
        {
            get => title;
            set { title = value; Invalidate(); }
        }

        private string description = "";
        public string Description
        {
            get => description;
            set { description = value; Invalidate(); }
        }

        private ActionState actionState = ActionState.Idle;
        public ActionState State => actionState;

        private static Font numberFont => UiFonts.SmallBold;
        private static Font titleFont => UiFonts.Title;
        private static Font descFont => UiFonts.Small;
        private static Font actionFont => UiFonts.SmallBold;

        private readonly Tween hoverT;
        private readonly Tween appliedT;
        private readonly Tween revealT;
        private readonly Tween stateT;
        private System.Windows.Forms.Timer? entranceTimer;
        private System.Windows.Forms.Timer? resetTimer;

        private Color stateDeep = PulseBlue;
        private Color stateLight = PulseCyan;

        private readonly float phaseOffset;
        private bool clockSubscribed;
        private static readonly Random Rng = new();

        private const TextFormatFlags DescFlags =
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
        private static float Now => Tween.Enabled ? PulseClock.Seconds : 1.2f;
        private const float OneShotDur = 0.9f;
        private float oneShotStart = -100f;

        private float OneShotT => Math.Clamp((Now - oneShotStart) / OneShotDur, 0f, 1f);
        private bool OneShotActive => Tween.Enabled && Now - oneShotStart < OneShotDur;

        private void StartOneShot()
        {
            if (!Tween.Enabled) return;
            oneShotStart = Now;
            SubscribeClock();
        }

        private bool IsActionCard => AssociatedTweak?.IsAction == true;
        private Color Deep =>
            IsActionCard && actionState != ActionState.Idle ? stateDeep :
            isRisky ? RiskDeep : PulseBlue;

        private Color Light =>
            IsActionCard && actionState != ActionState.Idle ? stateLight :
            isRisky ? RiskLight : PulseCyan;

        public CustomTweakCard()
        {
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Size = new Size(260, 112);
            Cursor = Cursors.Hand;

            phaseOffset = (float)Rng.NextDouble() * EcgPeriod;

            hoverT = new Tween(0, 180, () => Invalidate());
            appliedT = new Tween(0, 280, () => Invalidate());
            revealT = new Tween(0, 250, () => Invalidate());
            stateT = new Tween(0, 220, () => Invalidate());
        }

        // clock

        private bool NeedsClock =>
            isApplied || appliedT.Value > 0.01f || actionState == ActionState.Running || OneShotActive;

        private void SubscribeClock()
        {
            if (clockSubscribed || !Tween.Enabled) return;
            clockSubscribed = true;
            PulseClock.Tick += OnClockTick;
        }

        private void UnsubscribeClock()
        {
            if (!clockSubscribed) return;
            clockSubscribed = false;
            PulseClock.Tick -= OnClockTick;
        }

        private void OnClockTick(object? s, EventArgs e)
        {
            if (IsDisposed) { UnsubscribeClock(); return; }
            if (!NeedsClock) { UnsubscribeClock(); return; }
            if (Visible && Parent != null) Invalidate();
        }

        // binding

        public void BindTweak(ITweak tweak, string number)
        {
            AssociatedTweak = tweak;
            CardNumber = number;
            title = ReadString(tweak, "Name") ?? PrettifyName(tweak.GetType().Name);
            description = ReadString(tweak, "Description") ?? "";
            Invalidate();
        }

        private static string? ReadString(object o, string prop) =>
            o.GetType().GetProperty(prop)?.GetValue(o) as string;

        private static string PrettifyName(string raw)
        {
            if (raw.EndsWith("Tweak") && raw.Length > 5) raw = raw[..^5];
            string spaced = Regex.Replace(raw, "(?<=[a-z])(?=[A-Z])", " ");
            return spaced.Length > 1 ? spaced[0] + spaced[1..].ToLowerInvariant() : spaced;
        }

        public int GetPreferredHeight(int width)
        {
            int textW = Math.Max(40, width - Pad * 2);
            int descH = description.Length == 0
                ? 0
                : TextRenderer.MeasureText(description, descFont, new Size(textW, int.MaxValue), DescFlags).Height;
            return Math.Max(100, DescTop + descH + Pad);
        }

        // entrance
        public void PlayEntrance(int delayMs)
        {
            entranceTimer?.Dispose();
            entranceTimer = null;
            revealT.Snap(0f);

            if (delayMs <= 0)
            {
                revealT.Go(1f);
                return;
            }

            entranceTimer = new System.Windows.Forms.Timer { Interval = delayMs };
            entranceTimer.Tick += (s, e) =>
            {
                entranceTimer?.Stop();
                revealT.Go(1f);
            };
            entranceTimer.Start();
        }
        public void ShowInstantly()
        {
            entranceTimer?.Dispose();
            entranceTimer = null;
            revealT.Snap(1f);
            Invalidate();
        }

        // action state
        public void SetActionState(ActionState state)
        {
            if (!IsActionCard || IsDisposed) return;

            resetTimer?.Stop();
            actionState = state;

            switch (state)
            {
                case ActionState.Running:
                    stateDeep = PulseBlue; stateLight = PulseCyan;
                    SubscribeClock();
                    break;
                case ActionState.Success:
                    stateDeep = OkDeep; stateLight = OkLight;
                    StartOneShot();
                    ScheduleReset(1800);
                    break;
                case ActionState.Failed:
                    stateDeep = ErrDeep; stateLight = ErrLight;
                    StartOneShot();
                    ScheduleReset(2600);
                    break;
            }

            stateT.Go(state == ActionState.Idle ? 0f : 1f);
            Invalidate();
        }

        private void ScheduleReset(int ms)
        {
            if (resetTimer == null)
            {
                resetTimer = new System.Windows.Forms.Timer();
                resetTimer.Tick += (s, e) =>
                {
                    resetTimer?.Stop();
                    if (!IsDisposed) SetActionState(ActionState.Idle);
                };
            }
            resetTimer.Interval = ms;
            resetTimer.Start();
        }

        // input

        protected override void OnMouseEnter(EventArgs e)
        {
            hoverT.Go(1f);
            if (!(IsActionCard && actionState != ActionState.Idle))
            {
                StartOneShot();
            }
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hoverT.Go(0f); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            // ignore clicks while an action is still running
            if (IsActionCard && actionState == ActionState.Running) return;
            TweakClicked?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (NeedsClock) SubscribeClock();
        }

        // helpers

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static int A(float alpha) => Math.Clamp((int)alpha, 0, 255);

        private static Color Mix(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        private static float G(float u, float c, float w)
        {
            float d = (u - c) / w;
            return (float)Math.Exp(-d * d);
        }
        private static float Ecg(float u) =>
            0.10f * G(u, -0.30f, 0.06f)
            - 0.14f * G(u, -0.07f, 0.020f)
            + 1.00f * G(u, 0.00f, 0.022f)
            - 0.30f * G(u, 0.05f, 0.022f)
            + 0.20f * G(u, 0.30f, 0.07f);
        private float Heartbeat()
        {
            float t = (Now + phaseOffset) % BeatPeriod / BeatPeriod;
            return Math.Min(1f, G(t, 0.08f, 0.07f) + 0.65f * G(t, 0.26f, 0.08f));
        }
        private LinearGradientBrush HeadBrush(float cx, float span, Color endC, Color peakC, float aEnd, float aPeak)
        {
            float big = Math.Max(span, Width + 120f);
            var rect = new RectangleF(cx - big, 0, big * 2f, Math.Max(1, Height));
            var br = new LinearGradientBrush(rect, endC, peakC, 0f);
            Color mid = Mix(endC, peakC, 0.5f);
            Color midA = Color.FromArgb(A((aEnd + aPeak) * 0.5f), mid);
            float p = span / (big * 2f);
            br.InterpolationColors = new ColorBlend
            {
                Positions = new[] { 0f, 0.5f - p, 0.5f - 0.3f * p, 0.5f, 0.5f + 0.3f * p, 0.5f + p, 1f },
                Colors = new[]
                {
                    Color.FromArgb(A(aEnd), endC),
                    Color.FromArgb(A(aEnd), endC),
                    midA,
                    Color.FromArgb(A(aPeak), peakC),
                    midA,
                    Color.FromArgb(A(aEnd), endC),
                    Color.FromArgb(A(aEnd), endC)
                }
            };
            return br;
        }
        private LinearGradientBrush TrailBrush(float cx, Color tailC, Color headC, float aHead)
        {
            const float tail = 240f, lead = 70f;
            float L = tail + Width + 140f;
            float R = lead + Width + 140f;
            float T = L + R;
            var rect = new RectangleF(cx - L, 0, T, Math.Max(1, Height));
            var br = new LinearGradientBrush(rect, tailC, headC, 0f);
            br.InterpolationColors = new ColorBlend
            {
                Positions = new[]
                {
                    0f,
                    (L - tail) / T,
                    (L - tail * 0.45f) / T,
                    L / T,
                    (L + lead * 0.5f) / T,
                    (L + lead) / T,
                    1f
                },
                Colors = new[]
                {
                    Color.FromArgb(0, tailC),
                    Color.FromArgb(0, tailC),
                    Color.FromArgb(A(aHead * 0.35f), Mix(tailC, headC, 0.4f)),
                    Color.FromArgb(A(aHead), headC),
                    Color.FromArgb(A(aHead * 0.6f), headC),
                    Color.FromArgb(0, headC),
                    Color.FromArgb(0, headC)
                }
            };
            return br;
        }

        // layers
        private void DrawBaseDetail(Graphics g, GraphicsPath path, Rectangle rect, float r)
        {
            Color deep = Deep, light = Light;
            var state = g.Save();
            g.SetClip(path);

            using (var sheen = new LinearGradientBrush(rect, Color.FromArgb(A(16 * r), Color.White),
                       Color.FromArgb(0, Color.White), 90f))
                g.FillRectangle(sheen, rect);

            using (var streak = new Pen(Color.FromArgb(A(11 * r), light), 1f))
            {
                for (int i = 0; i < 4; i++)
                {
                    float ox = Width - 18 - i * 14;
                    g.DrawLine(streak, ox + 70, Height - 70, ox - 40, Height + 10);
                }
            }

            using (var gp = new GraphicsPath())
            {
                gp.AddEllipse(Width - 120, -70, 190, 130);
                using var pg = new PathGradientBrush(gp)
                {
                    CenterColor = Color.FromArgb(A(20 * r), deep),
                    SurroundColors = new[] { Color.FromArgb(0, deep) }
                };
                g.FillPath(pg, gp);
            }

            var hl = new RectangleF(10, 1, Math.Max(2, Width - 20), 1);
            using (var hb = new LinearGradientBrush(hl, Color.FromArgb(0, Color.White), Color.FromArgb(0, Color.White), 0f))
            {
                hb.InterpolationColors = new ColorBlend
                {
                    Positions = new[] { 0f, 0.5f, 1f },
                    Colors = new[]
                    {
                        Color.FromArgb(0, Color.White),
                        Color.FromArgb(A(34 * r), Color.White),
                        Color.FromArgb(0, Color.White)
                    }
                };
                g.FillRectangle(hb, hl);
            }

            g.Restore(state);
        }
        private void DrawSweep(Graphics g, GraphicsPath path, float t, float r)
        {
            if (t <= 0.001f || t >= 0.999f) return;

            float ease = 1f - (float)Math.Pow(1f - t, 3);
            float bandW = 80f, slant = 34f;
            float x = -bandW - slant + (Width + bandW * 2 + slant) * ease;
            float fade = (float)Math.Sin(t * Math.PI);
            Color light = Light;

            var state = g.Save();
            g.SetClip(path);

            var poly = new[]
            {
                new PointF(x + slant, 0), new PointF(x + slant + bandW, 0),
                new PointF(x + bandW, Height), new PointF(x, Height)
            };

            using var br = new LinearGradientBrush(
                new RectangleF(x, 0, bandW + slant, Height), Color.Transparent, Color.Transparent, 0f);
            br.InterpolationColors = new ColorBlend
            {
                Positions = new[] { 0f, 0.5f, 1f },
                Colors = new[]
                {
                    Color.FromArgb(0, light),
                    Color.FromArgb(A(26f * fade * r), light),
                    Color.FromArgb(0, light)
                }
            };
            g.FillPolygon(br, poly);
            g.Restore(state);
        }
        private void DrawPulseLayer(Graphics g, GraphicsPath path, Rectangle rect,
                                    float cx, float strength, float on, float r)
        {
            Color deep = Deep, light = Light;
            float baseY = Height - 24f;
            var state = g.Save();
            g.SetClip(path);

            if (strength > 0.01f)
            {
                float breath = 0.5f + 0.5f * (float)Math.Sin((Now + phaseOffset) * 2.2f);
                using var wash = new LinearGradientBrush(rect,
                    Color.FromArgb(0, deep),
                    Color.FromArgb(A((14f + 10f * breath) * strength * r), deep), 90f);
                g.FillRectangle(wash, rect);

                using var gp = new GraphicsPath();
                gp.AddEllipse(cx - 130, baseY - Height * 0.95f, 260, Height * 1.9f);
                using var pg = new PathGradientBrush(gp)
                {
                    CenterColor = Color.FromArgb(A(48f * strength * r), light),
                    SurroundColors = new[] { Color.FromArgb(0, light) }
                };
                g.FillPath(pg, gp);
            }

            using (var flat = new Pen(Color.FromArgb(A((30f + 40f * Math.Max(on, strength)) * r), FlatLine), 1f))
                g.DrawLine(flat, 12, baseY, Width - 12, baseY);

            if (strength > 0.01f)
            {
                var pts = new List<PointF>(Width / 4 + 140);
                float x = 12f, endX = Width - 12f;
                while (x <= endX)
                {
                    float u = (x - cx) / BlipWidth;
                    float y = baseY - BlipAmp * strength * Ecg(u);
                    pts.Add(new PointF(x, y));
                    x += Math.Abs(x - cx) < 70f ? 1f : 10f;
                }
                if (pts.Count > 1)
                {
                    var arr = pts.ToArray();
                    float aa = strength * r;

                    using (var b = TrailBrush(cx, deep, light, 70f * aa))
                    using (var glow = new Pen(b, 6f) { LineJoin = LineJoin.Round })
                        g.DrawLines(glow, arr);
                    using (var b = TrailBrush(cx, deep, light, 190f * aa))
                    using (var mid = new Pen(b, 2.4f) { LineJoin = LineJoin.Round })
                        g.DrawLines(mid, arr);
                    using (var b = TrailBrush(cx, light, Color.White, 255f * aa))
                    using (var core = new Pen(b, 1.1f) { LineJoin = LineJoin.Round })
                        g.DrawLines(core, arr);
                }
            }

            g.Restore(state);
        }
        private void DrawNeonEdge(Graphics g, GraphicsPath path, float cx, float strength, float r)
        {
            if (strength < 0.01f) return;
            float s = strength * r;
            Color deep = Deep, light = Light;

            var state = g.Save();
            g.SetClip(path);
            for (int i = 1; i <= 3; i++)
            {
                using var br = HeadBrush(cx, 170f, deep, light, 22f * s / i, 85f * s / i);
                using var pen = new Pen(br, i * 3.4f) { LineJoin = LineJoin.Round };
                g.DrawPath(pen, path);
            }
            g.Restore(state);

            using var edgeBrush = HeadBrush(cx, 170f, deep, light, 110f * s, 255f * s);
            using var edge = new Pen(edgeBrush, 1.3f);
            g.DrawPath(edge, path);
        }

        // action button

        private void DrawActionButton(Graphics g, Func<Color, Color> fade, float h, float st)
        {
            var btn = new Rectangle(Width - Pad - 86, Pad - 6, 86, 24);
            Color idle = Color.FromArgb(40, 40, 48);
            Color idleDeep = isRisky ? RiskDeep : PulseBlue;
            Color idleLight = isRisky ? RiskLight : PulseCyan;

            Color from = Theme.Lerp(Theme.Lerp(idle, idleDeep, h), stateDeep, st);
            Color to = Theme.Lerp(Theme.Lerp(idle, idleLight, h), stateLight, st);
            Color edge = Theme.Lerp(Theme.Lerp(Theme.BorderHover, idleLight, h), stateLight, st);

            using var btnPath = RoundRect(btn, 12);
            using var btnBrush = new LinearGradientBrush(btn, fade(from), fade(to), 0f);
            using var btnPen = new Pen(fade(edge));
            g.FillPath(btnBrush, btnPath);
            g.DrawPath(btnPen, btnPath);

            float ix = btn.X + 14f, iy = btn.Y + 12f;
            using var iconPen = new Pen(fade(Color.White), 1.8f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };

            string label;
            switch (actionState)
            {
                case ActionState.Running:
                    {
                        label = "RUNNING";
                        float ang = Now * 420f % 360f;
                        g.DrawArc(iconPen, ix - 5f, iy - 5f, 10f, 10f, ang, 250f);
                        break;
                    }
                case ActionState.Success:
                    label = "DONE";
                    g.DrawLines(iconPen, new[]
                    {
                        new PointF(ix - 4.5f, iy + 0.5f), new PointF(ix - 1.5f, iy + 3.5f), new PointF(ix + 4.5f, iy - 3f)
                    });
                    break;
                case ActionState.Failed:
                    label = "FAILED";
                    g.DrawLine(iconPen, ix - 3.5f, iy - 3.5f, ix + 3.5f, iy + 3.5f);
                    g.DrawLine(iconPen, ix + 3.5f, iy - 3.5f, ix - 3.5f, iy + 3.5f);
                    break;
                default:
                    {
                        label = "EXECUTE";
                        var bolt = new[]
                        {
                        new PointF(ix + 2f, iy - 6.5f), new PointF(ix - 3.5f, iy + 1f), new PointF(ix - 0.5f, iy + 1f),
                        new PointF(ix - 2f, iy + 6.5f), new PointF(ix + 3.5f, iy - 1.5f), new PointF(ix + 0.5f, iy - 1.5f)
                    };
                        using var boltBrush = new SolidBrush(fade(Mix(Color.White, PulseCyan, 1f - h * 0.6f)));
                        g.FillPolygon(boltBrush, bolt);
                        break;
                    }
            }

            var textRect = new Rectangle(btn.X + 24, btn.Y, btn.Width - 28, btn.Height);
            TextRenderer.DrawText(g, label, actionFont, textRect, fade(Color.White),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        // paint

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;

            if (Width < 40 || Height < 40) return;

            if (Parent != null)
            {
                var state = g.Save();
                g.TranslateTransform(-Left, -Top);
                InvokePaintBackground(Parent, new PaintEventArgs(g, new Rectangle(Left, Top, Width, Height)));
                g.Restore(state);
            }
            else
            {
                g.Clear(Theme.WindowBg);
            }

            float h = hoverT.Value;
            float a = appliedT.Value;
            float r = revealT.Value;
            float st = stateT.Value;

            Color fadeBg = Color.FromArgb(10, 10, 10);
            Color Fade(Color c) => Theme.Lerp(fadeBg, c, r);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(0, (1f - r) * 14f);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            const int radius = 12;

            bool isAction = IsActionCard;
            float on = isAction ? 0f : a;
            Color deep = Deep, light = Light;

            float cx, strength;
            if (isAction)
            {
                if (actionState == ActionState.Running)
                {
                    float prog = (Now % RunningPeriod) / RunningPeriod;
                    cx = -70f + (Width + 140f) * prog;
                    strength = 1f;
                }
                else
                {
                    float t = OneShotT;
                    bool playing = t > 0.001f && t < 0.999f;
                    cx = -70f + (Width + 140f) * t;
                    strength = playing ? 1f : 0f;
                }
            }
            else
            {
                float prog = ((Now + phaseOffset) % EcgPeriod) / EcgPeriod;
                cx = -70f + (Width + 140f) * prog;
                strength = on;
            }

            Color fill = Theme.Lerp(Theme.Lerp(Theme.Card, Theme.CardHover, h),
                                    Theme.Lerp(Theme.CardOn, Theme.CardOnHover, h), on);
            Color border = Theme.Lerp(Theme.Lerp(Theme.Border, Theme.BorderHover, h), Theme.BorderOn, on);

            if (isRisky) border = Theme.Lerp(border, RiskDeep, 0.45f * (1f - on * 0.5f));

            using (var path = RoundRect(rect, radius))
            {
                using (var brush = new SolidBrush(Fade(fill)))
                using (var pen = new Pen(Fade(border)))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(pen, path);
                }

                DrawBaseDetail(g, path, rect, r);

                using (var hoverPen = new Pen(Color.FromArgb(A(80 * h * (1f - on) * r), light)))
                    g.DrawPath(hoverPen, path);

                if (!isAction) DrawSweep(g, path, OneShotT, r);

                // the pulse under all text
                DrawPulseLayer(g, path, rect, cx, strength, on, r);
                DrawNeonEdge(g, path, cx, strength, r);
            }

            // number chip
            const int slant = 5;
            int numW = TextRenderer.MeasureText(CardNumber, numberFont, new Size(100, 20),
                TextFormatFlags.NoPadding).Width;
            int cx0 = Pad - 8, cy0 = Pad - 6, cw = numW + 16 + slant, ch = 20;
            var chip = new[]
            {
                new Point(cx0 + slant, cy0), new Point(cx0 + cw, cy0),
                new Point(cx0 + cw - slant, cy0 + ch), new Point(cx0, cy0 + ch)
            };
            using (var chipFill = new SolidBrush(Color.FromArgb(A((10 + 24 * on) * r), Mix(Color.White, light, on))))
            using (var chipPen = new Pen(Color.FromArgb(A((26 + 90 * on) * r), Mix(Color.White, light, on))))
            {
                g.FillPolygon(chipFill, chip);
                g.DrawPolygon(chipPen, chip);
            }

            TextRenderer.DrawText(g, CardNumber, numberFont, new Point(Pad, Pad - 3),
                Fade(Theme.Lerp(Theme.TextMuted, light, on)), TextFormatFlags.NoPadding);

            if (isAction)
            {
                DrawActionButton(g, Fade, h, st);
            }
            else
            {
                var sw = new Rectangle(Width - Pad - 34, Pad - 4, 34, 18);
                const int knob = 12;
                using (var swPath = RoundRect(sw, 9))
                {
                    using (var swBrush = new LinearGradientBrush(sw,
                        Fade(Theme.Lerp(SwitchOffTrack, deep, a)),
                        Fade(Theme.Lerp(SwitchOffTrack, light, a)), 0f))
                        g.FillPath(swBrush, swPath);

                    int outlineAlpha = (int)(80 * (1f - a));
                    if (outlineAlpha > 0)
                    {
                        using var swPen = new Pen(Color.FromArgb(outlineAlpha, 120, 140, 190));
                        g.DrawPath(swPen, swPath);
                    }

                    if (a > 0.01f)
                    {
                        float glow = a * r * (0.25f + 0.75f * Heartbeat());
                        using var glowPen = new Pen(Color.FromArgb(A(95 * glow), light), 3f);
                        g.DrawPath(glowPen, swPath);
                    }
                }
                float kx = sw.X + 3 + (sw.Width - knob - 6) * a;
                using (var kb = new SolidBrush(Fade(Theme.Lerp(SwitchOffKnob, Color.FromArgb(10, 10, 10), a))))
                    g.FillEllipse(kb, kx, sw.Y + 3, knob, knob);
            }

            DrawTitle(g, Fade);

            if (description.Length > 0)
            {
                Color descColor = Mix(Theme.TextSecondary, Color.White, 0.18f);
                var descRect = new Rectangle(Pad, DescTop, Width - Pad * 2, Height - DescTop - 4);
                TextRenderer.DrawText(g, description, descFont, descRect, Fade(descColor), DescFlags);
            }
        }

        private void DrawTitle(Graphics g, Func<Color, Color> fade)
        {
            const TextFormatFlags measureFlags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            var unbounded = new Size(int.MaxValue, 20);

            int fullW = Width - Pad * 2;

            int riskW = 0;
            if (isRisky)
                riskW = RiskGap + TextRenderer.MeasureText(g, RiskText, titleFont, unbounded, measureFlags).Width;

            int titleMaxW = Math.Max(20, fullW - riskW);
            int titleW = Math.Min(titleMaxW,
                TextRenderer.MeasureText(g, title, titleFont, unbounded, measureFlags).Width);

            var titleRect = new Rectangle(Pad, 38, titleMaxW, 20);
            TextRenderer.DrawText(g, title, titleFont, titleRect, fade(Theme.TextTitle),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            if (!isRisky) return;

            var riskRect = new Rectangle(Pad + titleW + RiskGap, 38, riskW, 20);
            TextRenderer.DrawText(g, RiskText, titleFont, riskRect, fade(RiskColor),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnsubscribeClock();
                entranceTimer?.Dispose();
                resetTimer?.Dispose();
                hoverT.Dispose();
                appliedT.Dispose();
                revealT.Dispose();
                stateT.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}