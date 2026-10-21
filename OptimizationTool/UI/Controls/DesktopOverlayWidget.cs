using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OptimizationTool.UI.Controls
{
    public class DesktopOverlayWidget : Form
    {
        // layout
        private const int Pad = 12;
        private const int PanelW = 340;
        private const int PanelH = 210;
        private const int MaxHistory = 50;
        private const int KeepSamples = MaxHistory + 2;

        // palette 
        private static readonly Color Violet = Color.FromArgb(167, 139, 250);
        private static readonly Color Cyan = Color.FromArgb(56, 189, 248);
        private static Color Accent => Theme.Accent;

        // data
        private readonly List<float> cpuHistory = new();
        private readonly List<float> ramHistory = new();
        private readonly List<float> netHistory = new();   // raw bytes/sec

        private PerformanceCounter? cpuCounter;
        private PerformanceCounter? ramCounter;
        private PerformanceCounterCategory? netCategory;
        private PerformanceCounter[]? netCounters;

        private float totalRamMb, usedRamMb;
        private float cpuTarget, ramTarget, netBps;

        // animated displayed values
        private float cpuShown, ramShown, netBpsShown;
        private float netMaxShown = 1_000_000f;
        private float hoverShown;
        private bool hovering;

        // timers/rendering
        private readonly System.Windows.Forms.Timer pollTimer;
        private readonly System.Windows.Forms.Timer renderTimer;
        private readonly Stopwatch sinceSample = Stopwatch.StartNew();
        private readonly Stopwatch frameClock = Stopwatch.StartNew();
        private readonly Bitmap frame;
        private readonly Graphics fg;

        private readonly Font fontHeader = new("Segoe UI Semibold", 11f, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font fontBig = new("Segoe UI Semibold", 19f, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font fontUnit = new("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font fontLabel = new("Segoe UI Semibold", 11f, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font fontSub = new("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Pixel);

        private readonly ContextMenuStrip menu;

        // dragging
        private Point dragOffset;
        private bool isDragging;

        public DesktopOverlayWidget()
        {
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(PanelW + Pad * 2, PanelH + Pad * 2);

            var wa = Screen.PrimaryScreen!.WorkingArea;
            Location = new Point(wa.Right - PanelW - 20 - Pad, wa.Top + 40 - Pad);

            frame = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            fg = Graphics.FromImage(frame);

            for (int i = 0; i < KeepSamples; i++)
            {
                cpuHistory.Add(0f);
                ramHistory.Add(0f);
                netHistory.Add(0f);
            }

            InitializeCounters();

            // right click menu
            menu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                BackColor = Color.FromArgb(28, 30, 40),
                ForeColor = Color.White
            };
            var topItem = new ToolStripMenuItem("Always on top") { Checked = true, CheckOnClick = true };
            topItem.CheckedChanged += (s, e) => TopMost = topItem.Checked;
            menu.Items.Add(topItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Close widget", null, (s, e) => Close());
            ContextMenuStrip = menu;

            pollTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            pollTimer.Tick += (s, e) => PollMetrics();
            pollTimer.Start();

            renderTimer = new System.Windows.Forms.Timer { Interval = 16 };
            renderTimer.Tick += (s, e) => RenderTick();
            renderTimer.Start();

            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseEnter += (s, e) => hovering = true;
            MouseLeave += (s, e) => hovering = false;
        }

        //  window plumbing

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00080000;  // WS_EX_LAYERED
                cp.ExStyle |= 0x00000080;  // WS_EX_TOOLWINDOW (hidden from Alt+Tab)
                cp.ExStyle |= 0x08000000;  // WS_EX_NOACTIVATE (never steals focus)
                return cp;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize,
            IntPtr hdcSrc, ref Point pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

        private void Present(byte constantAlpha)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBmp = IntPtr.Zero, oldBmp = IntPtr.Zero;
            try
            {
                hBmp = frame.GetHbitmap(Color.FromArgb(0));
                oldBmp = SelectObject(memDc, hBmp);

                var size = new Size(Width, Height);
                var src = Point.Empty;
                var dst = new Point(Left, Top);
                var blend = new BLENDFUNCTION
                {
                    BlendOp = 0,               // AC_SRC_OVER
                    BlendFlags = 0,
                    SourceConstantAlpha = constantAlpha,
                    AlphaFormat = 1            // AC_SRC_ALPHA
                };
                UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, 2 /* ULW_ALPHA */);
            }
            finally
            {
                if (oldBmp != IntPtr.Zero) SelectObject(memDc, oldBmp);
                if (hBmp != IntPtr.Zero) DeleteObject(hBmp);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        //  metrics

        private void InitializeCounters()
        {
            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                ramCounter = new PerformanceCounter("Memory", "Available MBytes");

                totalRamMb = (float)(new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory / (1024 * 1024));

                if (PerformanceCounterCategory.Exists("Network Interface"))
                {
                    netCategory = new PerformanceCounterCategory("Network Interface");
                    var counters = new List<PerformanceCounter>();
                    foreach (var name in netCategory.GetInstanceNames())
                        counters.Add(new PerformanceCounter("Network Interface", "Bytes Total/sec", name));
                    netCounters = counters.ToArray();
                }
            }
            catch { }
        }

        private void PollMetrics()
        {
            float cpu = 0f, ram = 0f, bps = 0f;

            try
            {
                if (cpuCounter != null) cpu = Math.Clamp(cpuCounter.NextValue(), 0f, 100f);

                if (ramCounter != null && totalRamMb > 0)
                {
                    float freeMb = ramCounter.NextValue();
                    usedRamMb = Math.Clamp(totalRamMb - freeMb, 0f, totalRamMb);
                    ram = Math.Clamp(usedRamMb / totalRamMb * 100f, 0f, 100f);
                }

                if (netCounters != null)
                {
                    foreach (var c in netCounters)
                    {
                        try { bps += c.NextValue(); } catch { }
                    }
                }
            }
            catch { }

            cpuTarget = cpu;
            ramTarget = ram;
            netBps = bps;

            Push(cpuHistory, cpu);
            Push(ramHistory, ram);
            Push(netHistory, bps);

            sinceSample.Restart();   // drives the smooth graph scroll
        }

        private static void Push(List<float> list, float v)
        {
            list.Add(v);
            while (list.Count > KeepSamples) list.RemoveAt(0);
        }

        //  frame loop

        private void RenderTick()
        {
            if (!IsHandleCreated || IsDisposed || !Visible) return;

            float dt = Math.Min(0.25f, (float)frameClock.Elapsed.TotalSeconds);
            frameClock.Restart();

            float k = 1f - MathF.Exp(-dt * 7f);
            cpuShown += (cpuTarget - cpuShown) * k;
            ramShown += (ramTarget - ramShown) * k;
            netBpsShown += (netBps - netBpsShown) * k;

            float peak = Math.Max(1_000_000f, netHistory.Max());
            netMaxShown += (peak - netMaxShown) * (1f - MathF.Exp(-dt * 2.5f));

            hoverShown += ((hovering || isDragging ? 1f : 0f) - hoverShown) * (1f - MathF.Exp(-dt * 10f));

            Draw(fg);
            Present((byte)(220 + 35 * hoverShown));
        }

        //  drawing
        private void Draw(Graphics g)
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            var panel = new RectangleF(Pad, Pad, PanelW, PanelH);
            var accent = Accent;

            // soft drop shadow
            for (int i = 7; i >= 1; i--)
            {
                var r = RectangleF.Inflate(panel, i, i);
                r.Offset(0, 4);
                using var sp = RoundRect(r, 18 + i);
                using var sb = new SolidBrush(Color.FromArgb(9, 0, 0, 0));
                g.FillPath(sb, sp);
            }

            using var panelPath = RoundRect(panel, 18);

            // glass body
            using (var bg = new LinearGradientBrush(panel, Color.FromArgb(228, 26, 28, 40), Color.FromArgb(228, 10, 11, 17), LinearGradientMode.Vertical))
                g.FillPath(bg, panelPath);

            // accent tint bleeding from the top
            g.SetClip(panelPath);
            var tint = new RectangleF(panel.X, panel.Y, panel.Width, 90);
            using (var tb = new LinearGradientBrush(tint, A(accent, 34), A(accent, 0), LinearGradientMode.Vertical))
                g.FillRectangle(tb, tint);
            g.ResetClip();

            // gradient border 
            using (var bb = new LinearGradientBrush(panel, Color.FromArgb(120, 255, 255, 255), Color.FromArgb(16, 255, 255, 255), LinearGradientMode.Vertical))
            using (var bp = new Pen(bb, 1f))
                g.DrawPath(bp, panelPath);

            // glowing accent line along the top edge
            var glowRect = new RectangleF(panel.X + 24, panel.Y, panel.Width - 48, 2);
            using (var gb = new LinearGradientBrush(glowRect, A(accent, 0), A(accent, 0), LinearGradientMode.Horizontal))
            {
                gb.InterpolationColors = new ColorBlend
                {
                    Colors = new[] { A(accent, 0), A(accent, 230), A(accent, 0) },
                    Positions = new[] { 0f, 0.5f, 1f }
                };
                g.FillRectangle(gb, glowRect);
            }

            // header
            double sec = Environment.TickCount64 / 1000.0;
            int pulse = (int)(150 + 100 * Math.Sin(sec * 2.5));
            float hy = panel.Y + 22;
            using (var halo = new SolidBrush(A(accent, pulse / 5)))
                g.FillEllipse(halo, panel.X + 14, hy - 7.5f, 15, 15);
            using (var dot = new SolidBrush(A(accent, pulse)))
                g.FillEllipse(dot, panel.X + 18, hy - 3.5f, 7, 7);

            DrawText(g, "SYSTEM MONITOR", fontHeader, Color.FromArgb(205, 255, 255, 255), panel.X + 33, hy, StringAlignment.Near);
            DrawText(g, DateTime.Now.ToString("HH:mm"), fontHeader, Color.FromArgb(110, 255, 255, 255), panel.Right - 18, hy, StringAlignment.Far);

            // gauges
            float colW = panel.Width / 3f;
            float cy = panel.Y + 78;

            var cpuCol = Heat(accent, cpuShown);
            var ramCol = Heat(Violet, ramShown);
            var netCol = Cyan;

            float netPct = Math.Clamp(netBpsShown / netMaxShown * 100f, 0f, 100f);
            var (netVal, netUnit) = FormatSpeed(netBpsShown);

            DrawRing(g, panel.X + colW * 0.5f, cy, 62f, cpuShown, cpuCol, ((int)MathF.Round(cpuShown)).ToString(), "%");
            DrawRing(g, panel.X + colW * 1.5f, cy, 62f, ramShown, ramCol, ((int)MathF.Round(ramShown)).ToString(), "%");
            DrawRing(g, panel.X + colW * 2.5f, cy, 62f, netPct, netCol, netVal, netUnit);

            string[] names = { "CPU", "RAM", "NET" };
            string[] subs =
            {
                $"{Environment.ProcessorCount} threads",
                totalRamMb > 0 ? $"{usedRamMb / 1024f:0.0} / {totalRamMb / 1024f:0.0} GB" : "n/a",
                "all adapters"
            };
            Color[] cols = { cpuCol, ramCol, netCol };

            for (int i = 0; i < 3; i++)
            {
                float x = panel.X + colW * (i + 0.5f);
                DrawText(g, names[i], fontLabel, A(cols[i], 235), x, cy + 50);
                DrawText(g, subs[i], fontSub, Color.FromArgb(95, 255, 255, 255), x, cy + 65);
            }

            // history graph
            var graph = new RectangleF(panel.X + 18, panel.Y + 158, panel.Width - 36, 38);
            DrawGraph(g, graph, cpuCol, ramCol, netCol);
        }

        private void DrawRing(Graphics g, float cx, float cy, float d, float value, Color color, string main, string unit)
        {
            var rect = new RectangleF(cx - d / 2f, cy - d / 2f, d, d);

            using (var track = new Pen(Color.FromArgb(30, 255, 255, 255), 7f))
                g.DrawEllipse(track, rect);

            float sweep = 360f * Math.Clamp(value, 0f, 100f) / 100f;
            if (sweep > 1.5f)
            {
                using (var glow = new Pen(A(color, 38), 15f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawArc(glow, rect, -90f, sweep);
                using (var pen = new Pen(color, 7f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawArc(pen, rect, -90f, sweep);
            }

            DrawText(g, main, fontBig, Color.White, cx, cy - 4f);
            DrawText(g, unit, fontUnit, Color.FromArgb(120, 255, 255, 255), cx, cy + 12f);
        }

        private void DrawGraph(Graphics g, RectangleF r, Color cpuCol, Color ramCol, Color netCol)
        {
            float t = Math.Min(1f, (float)sinceSample.Elapsed.TotalMilliseconds / 1000f);

            using (var gp = new Pen(Color.FromArgb(18, 255, 255, 255), 1f) { DashStyle = DashStyle.Dot })
            {
                for (int i = 0; i <= 2; i++)
                {
                    float y = r.Y + r.Height * i / 2f;
                    g.DrawLine(gp, r.X, y, r.Right, y);
                }
            }

            g.SetClip(new RectangleF(r.X, r.Y - 4, r.Width + 4, r.Height + 8));
            DrawSeries(g, netHistory, netMaxShown, r, t, netCol, 1.5f, false);
            DrawSeries(g, ramHistory, 100f, r, t, ramCol, 1.5f, false);
            DrawSeries(g, cpuHistory, 100f, r, t, cpuCol, 2f, true);
            g.ResetClip();
        }

        private static void DrawSeries(Graphics g, List<float> h, float max, RectangleF r, float t, Color color, float width, bool fill)
        {
            if (h.Count < 2) return;

            float step = r.Width / (MaxHistory - 1);
            float YOf(float v) => r.Bottom - Math.Clamp(v / max, 0f, 1f) * (r.Height - 3f) - 1f;

            var pts = new List<PointF>(h.Count + 1);

            for (int i = 0; i < h.Count - 1; i++)
            {
                float x = r.Right - (h.Count - 1 - i) * step + (1f - t) * step;
                pts.Add(new PointF(x, YOf(h[i])));
            }

            float edgeV = h[^2] + (h[^1] - h[^2]) * t;
            if (t * step > 0.5f) pts.Add(new PointF(r.Right, YOf(edgeV)));

            var arr = pts.ToArray();

            if (fill)
            {
                using var path = new GraphicsPath();
                path.AddCurve(arr, 0.35f);
                path.AddLine(arr[^1].X, r.Bottom + 4, arr[0].X, r.Bottom + 4);
                path.CloseFigure();
                using var fb = new LinearGradientBrush(new RectangleF(r.X, r.Y, r.Width, r.Height + 4), A(color, 95), A(color, 0), LinearGradientMode.Vertical);
                g.FillPath(fb, path);
            }

            using (var glow = new Pen(A(color, 36), width + 3f) { LineJoin = LineJoin.Round })
                g.DrawCurve(glow, arr, 0.35f);
            using (var pen = new Pen(color, width) { LineJoin = LineJoin.Round })
                g.DrawCurve(pen, arr, 0.35f);

            // live dot on the newest value
            var e = arr[^1];
            using (var hb = new SolidBrush(A(color, 55)))
                g.FillEllipse(hb, e.X - 6f, e.Y - 6f, 12f, 12f);
            using (var db = new SolidBrush(color))
                g.FillEllipse(db, e.X - 2.5f, e.Y - 2.5f, 5f, 5f);
        }

        //  helpers

        private static void DrawText(Graphics g, string s, Font f, Color c, float x, float y, StringAlignment h = StringAlignment.Center)
        {
            using var br = new SolidBrush(c);
            using var sf = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = h,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            };
            g.DrawString(s, f, br, x, y, sf);
        }

        private static Color A(Color c, int alpha) => Color.FromArgb(Math.Clamp(alpha, 0, 255), c);

        private static Color Mix(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        private static Color Heat(Color baseColor, float v)
        {
            if (v < 65f) return baseColor;
            var warn = Color.FromArgb(255, 178, 64);
            var crit = Color.FromArgb(255, 76, 92);
            if (v < 85f) return Mix(baseColor, warn, (v - 65f) / 20f);
            return Mix(warn, crit, (v - 85f) / 12f);
        }

        private static (string value, string unit) FormatSpeed(float bytesPerSec)
        {
            const float mb = 1024f * 1024f;
            if (bytesPerSec >= mb)
            {
                float v = bytesPerSec / mb;
                return (v >= 100f ? v.ToString("0") : v.ToString("0.0"), "MB/s");
            }
            if (bytesPerSec >= 1024f) return ((bytesPerSec / 1024f).ToString("0"), "KB/s");
            return (bytesPerSec.ToString("0"), "B/s");
        }

        private static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            float d = radius * 2f;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        //  dragging (with edge snapping)

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            isDragging = true;
            dragOffset = new Point(Cursor.Position.X - Left, Cursor.Position.Y - Top);
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (!isDragging) return;
            var cur = Cursor.Position;
            int x = cur.X - dragOffset.X;
            int y = cur.Y - dragOffset.Y;
            SnapToEdges(ref x, ref y);
            Location = new Point(x, y);
        }

        private void OnMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) isDragging = false;
        }

        private void SnapToEdges(ref int x, ref int y)
        {
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            const int snap = 18, gap = 12;

            int l = x + Pad, t = y + Pad;
            int r = l + PanelW, b = t + PanelH;

            if (Math.Abs(l - (wa.Left + gap)) < snap) x = wa.Left + gap - Pad;
            else if (Math.Abs(r - (wa.Right - gap)) < snap) x = wa.Right - gap - PanelW - Pad;

            if (Math.Abs(t - (wa.Top + gap)) < snap) y = wa.Top + gap - Pad;
            else if (Math.Abs(b - (wa.Bottom - gap)) < snap) y = wa.Bottom - gap - PanelH - Pad;
        }

        //  Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pollTimer.Dispose();
                renderTimer.Dispose();
                cpuCounter?.Dispose();
                ramCounter?.Dispose();
                if (netCounters != null)
                {
                    foreach (var c in netCounters) c.Dispose();
                }
                fg.Dispose();
                frame.Dispose();
                menu.Dispose();
                fontHeader.Dispose();
                fontBig.Dispose();
                fontUnit.Dispose();
                fontLabel.Dispose();
                fontSub.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}