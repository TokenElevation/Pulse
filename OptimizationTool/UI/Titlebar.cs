using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OptimizationTool.UI.Controls
{
    internal static class PulseLogo
    {
        private static bool tried;
        private static Bitmap? image;

        public static Bitmap? Image
        {
            get
            {
                if (tried) return image;
                tried = true;

                try
                {
                    string path = Path.Combine(AppContext.BaseDirectory, "UI", "Assets", "pulse_logo.png");
                    if (!File.Exists(path)) return null;

                    using var fs = File.OpenRead(path);
                    using var img = System.Drawing.Image.FromStream(fs);
                    image = new Bitmap(img); // copy so the file isnt locked
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Logo] Could not load emblem: {ex.Message}");
                    image = null;
                }
                return image;
            }
        }
    }

    public enum CaptionKind { Help, Settings, Minimize, Close }

    public class CaptionButton : Control
    {
        private readonly CaptionKind kind;
        private readonly Tween hoverT;

        public event EventHandler? Invoked;

        public CaptionButton(CaptionKind kind)
        {
            this.kind = kind;
            hoverT = new Tween(0, 130, () => Invalidate());
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Size = new Size(46, 36);
        }

        protected override void OnMouseEnter(EventArgs e) { hoverT.Go(1f); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hoverT.Go(0f); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);

            if (kind == CaptionKind.Help || kind == CaptionKind.Settings)
            {
                Invoked?.Invoke(this, EventArgs.Empty);
                return;
            }

            var form = FindForm();
            if (form == null) return;
            if (kind == CaptionKind.Minimize) form.WindowState = FormWindowState.Minimized;
            else form.Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;

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

            bool isClose = kind == CaptionKind.Close;
            float h = hoverT.Value;

            Color fillColor = isClose ? Color.FromArgb(196, 43, 28) : Theme.GlowLight;
            int fillAlpha = (int)((isClose ? 255 : 34) * h);
            if (fillAlpha > 0)
            {
                using var fb = new SolidBrush(Color.FromArgb(fillAlpha, fillColor));
                g.FillRectangle(fb, ClientRectangle);
            }

            Color glyph = isClose
                ? Theme.Lerp(Theme.TextPrimary, Color.White, h)
                : Theme.Lerp(Theme.TextPrimary, Color.White, h);
            using var pen = new Pen(glyph, 1f);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float cx = Width / 2f, cy = Height / 2f;
            if (kind == CaptionKind.Minimize)
            {
                g.SmoothingMode = SmoothingMode.None;
                g.DrawLine(pen, (int)cx - 5, (int)cy, (int)cx + 5, (int)cy);
            }
            else if (kind == CaptionKind.Settings)
            {
                DrawGear(g, pen, cx, cy);
            }
            else if (kind == CaptionKind.Help)
            {
                DrawQuestionMark(g, glyph, cx, cy);
            }
            else
            {
                g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                g.DrawLine(pen, cx - 5, cy + 5, cx + 5, cy - 5);
            }

        }

        private static void DrawQuestionMark(Graphics g, Color color, float cx, float cy)
        {
            using var font = new Font("Segoe UI", 10F, FontStyle.Bold);
            TextRenderer.DrawText(g, "?", font,
                new Rectangle((int)cx - 10, (int)cy - 10, 20, 20), color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static void DrawGear(Graphics g, Pen pen, float cx, float cy)
        {
            using var teeth = new Pen(pen.Color, 2.4f) { StartCap = LineCap.Flat, EndCap = LineCap.Flat };
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
                g.DrawLine(teeth, cx + cos * 5f, cy + sin * 5f, cx + cos * 7.5f, cy + sin * 7.5f);
            }
            g.DrawEllipse(pen, cx - 4.5f, cy - 4.5f, 9f, 9f);
            g.DrawEllipse(pen, cx - 1.8f, cy - 1.8f, 3.6f, 3.6f);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) hoverT.Dispose();
            base.Dispose(disposing);
        }
    }

    public class TitleBar : GradientPanel
    {
        private const int LogoSize = 20;

        public string Title { get; set; } = "";

        public event EventHandler? SettingsClicked;

        public event EventHandler? HelpClicked;

        private readonly Font titleFont = new("Segoe UI Semibold", 9F);

        // manual drag state
        private bool dragging;
        private Point dragStartCursor;
        private Point dragStartForm;

        public TitleBar()
        {
            Dock = DockStyle.Top;
            Height = 36;
            Padding = new Padding(0, 0, 0, 1);

            var closeBtn = new CaptionButton(CaptionKind.Close) { Dock = DockStyle.Right };
            var minBtn = new CaptionButton(CaptionKind.Minimize) { Dock = DockStyle.Right };

            var settingsBtn = new CaptionButton(CaptionKind.Settings) { Dock = DockStyle.Right };
            settingsBtn.Invoked += (s, e) => SettingsClicked?.Invoke(this, EventArgs.Empty);

            var helpBtn = new CaptionButton(CaptionKind.Help) { Dock = DockStyle.Right };
            helpBtn.Invoked += (s, e) => HelpClicked?.Invoke(this, EventArgs.Empty);

            Controls.Add(helpBtn);
            Controls.Add(settingsBtn);
            Controls.Add(minBtn);
            Controls.Add(closeBtn);
        }

        // manual window drag

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            var form = FindForm();
            if (form == null) return;

            dragging = true;
            dragStartCursor = Cursor.Position;
            dragStartForm = form.Location;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;

            var form = FindForm();
            if (form == null) return;

            var now = Cursor.Position;
            var target = new Point(
                dragStartForm.X + (now.X - dragStartCursor.X),
                dragStartForm.Y + (now.Y - dragStartCursor.Y));

            if (form.Location != target) form.Location = target;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            EndDrag();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            dragging = false;
        }

        private void EndDrag()
        {
            if (!dragging) return;
            dragging = false;
            Capture = false;
        }

        // paint

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;

            int textX = 16;

            // emblem before the title
            var logo = PulseLogo.Image;
            if (logo != null)
            {
                var old = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(logo, new Rectangle(12, (Height - LogoSize) / 2, LogoSize, LogoSize));
                g.InterpolationMode = old;
                textX = 12 + LogoSize + 8;
            }

            TextRenderer.DrawText(g, Title, titleFont,
                new Rectangle(textX, 0, Math.Max(0, Width - 210 - textX + 16), Height), Theme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            DrawBottomLine(g, 0);
        }
        internal void DrawBottomLine(Graphics g, int offsetX)
        {
            if (Width < 40) return;

            var state = g.Save();
            g.TranslateTransform(-offsetX, 0);
            g.SmoothingMode = SmoothingMode.None;

            int y = Height - 1;

            // base border
            using (var line = new Pen(Theme.Border))
                g.DrawLine(line, 0, y, Width, y);

            var r = new Rectangle(0, y, Width + 1, 1);
            using (var br = new LinearGradientBrush(r, Theme.GlowDeep, Theme.GlowLight, 0f))
            {
                br.WrapMode = WrapMode.TileFlipX;
                br.InterpolationColors = new ColorBlend
                {
                    Positions = new[] { 0f, 0.10f, 0.55f, 1f },
                    Colors = new[]
                    {
                    Color.FromArgb(40,  Theme.GlowDeep),
                    Color.FromArgb(180, Theme.GlowDeep),
                    Color.FromArgb(200, Theme.Lerp(Theme.GlowDeep, Theme.GlowLight, 0.6f)),
                    Color.FromArgb(200, Theme.GlowLight)
                }
                };
                g.FillRectangle(br, new Rectangle(0, y, Width, 1));
            }

            g.Restore(state);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) titleFont.Dispose();
            base.Dispose(disposing);
        }
    }
}