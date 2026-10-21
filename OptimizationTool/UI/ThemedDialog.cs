using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace OptimizationTool.UI.Controls
{
    public static class ThemedMessageBox
    {
        public static DialogResult Show(IWin32Window? owner, string text, string title = "",
            MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
        {
            var ownerForm = owner as Form ?? Form.ActiveForm;

            ScrimForm? scrim = null;
            if (ownerForm != null && ownerForm.Visible && ownerForm.WindowState != FormWindowState.Minimized)
            {
                scrim = new ScrimForm(ownerForm);
                scrim.Show(ownerForm);
            }

            PulseClock.Pause();
            try
            {
                using var dialog = new ThemedDialog(text, title, buttons, icon);
                return dialog.ShowDialog((IWin32Window?)scrim ?? ownerForm);
            }
            finally
            {
                PulseClock.Resume();
                scrim?.Close();
                scrim?.Dispose();
                ownerForm?.Activate();
            }
        }
        public static void Prewarm()
        {
            try
            {
                _ = PulseLogo.Image;

                foreach (var icon in new[] { MessageBoxIcon.Warning, MessageBoxIcon.Information })
                {
                    using var d = new ThemedDialog("Prewarm", "Prewarm", MessageBoxButtons.YesNo, icon);
                    using var bmp = new Bitmap(d.Width, d.Height);
                    d.DrawToBitmap(bmp, new Rectangle(Point.Empty, d.Size)); // runs the real paint code
                }
            }
            catch { /* prewarming is best-effort */ }
        }
    }

    internal static class FlashGuard
    {
        public const double StartOpacity = 0.01;
        public const double MaxOpacity = 0.999;

        private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void DisableDwmTransitions(IntPtr handle)
        {
            try
            {
                int v = 1;
                DwmSetWindowAttribute(handle, DWMWA_TRANSITIONS_FORCEDISABLED, ref v, sizeof(int));
            }
            catch { /* dwmapi unavailable */ }
        }

        public static double Lerp(float t) => StartOpacity + (MaxOpacity - StartOpacity) * Math.Clamp(t, 0f, 1f);
    }

    internal sealed class ScrimForm : Form
    {
        private readonly Tween fade;

        public ScrimForm(Form owner)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Lerp(Color.Black, Theme.GlowDeep, 0.07f);
            Opacity = FlashGuard.StartOpacity;
            Bounds = owner.Bounds;

            fade = new Tween(0f, 160, () => Opacity = Math.Max(FlashGuard.StartOpacity, 0.58 * fade!.Value));
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            FlashGuard.DisableDwmTransitions(Handle);
            Native.RoundOnly(Handle);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            fade.Go(1f);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            fade.Dispose();
            base.OnFormClosed(e);
        }
    }

    internal sealed class ThemedDialog : Form
    {
        private const int DialogWidth = 420, Pad = 24, IconSize = 38, ButtonW = 96, ButtonH = 36, ButtonGap = 10;
        private const int SlidePx = 0;

        private static readonly Regex EmojiRx = new(
            @"[\u2600-\u27BF\uFE0F\u200D]|[\uD800-\uDBFF][\uDC00-\uDFFF]");

        private static string Clean(string s) => EmojiRx.Replace(s, "").Trim();

        private readonly string text;
        private readonly string title;
        private readonly MessageBoxIcon icon;
        private readonly Color deep;
        private readonly Color light;
        private readonly Rectangle titleRect;
        private readonly Rectangle messageRect;
        private readonly Rectangle iconRect;
        private readonly Tween fade;
        private int baseTop;

        public ThemedDialog(string text, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            this.text = Clean(text);
            this.title = Clean(title);
            this.icon = icon;

            switch (icon)
            {
                case MessageBoxIcon.Error:
                    deep = Color.FromArgb(180, 40, 55);
                    light = Color.FromArgb(255, 105, 105);
                    break;
                case MessageBoxIcon.Warning:
                    deep = Color.FromArgb(214, 72, 72);
                    light = Color.FromArgb(255, 176, 64);
                    break;
                default:
                    deep = Theme.GlowDeep;
                    light = Theme.GlowLight;
                    break;
            }

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = Theme.Surface;
            DoubleBuffered = true;
            Opacity = FlashGuard.StartOpacity;

            bool hasIcon = icon != MessageBoxIcon.None;
            int textX = hasIcon ? Pad + IconSize + 16 : Pad;
            int textW = DialogWidth - textX - Pad;

            const TextFormatFlags measureFlags = TextFormatFlags.Left | TextFormatFlags.Top |
                                                 TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;

            int titleH = this.title.Length > 0 ? 24 : 0;
            int messageH = this.text.Length > 0
                ? TextRenderer.MeasureText(this.text, UiFonts.Body, new Size(textW, int.MaxValue), measureFlags).Height
                : 0;
            int spacing = titleH > 0 && messageH > 0 ? 6 : 0;
            int contentH = Math.Max(IconSize, titleH + spacing + messageH);

            int top = Pad + 4;
            iconRect = new Rectangle(Pad, top, IconSize, IconSize);
            titleRect = new Rectangle(textX, top, textW, titleH);
            messageRect = new Rectangle(textX, top + titleH + spacing, textW, messageH);
            ClientSize = new Size(DialogWidth, top + contentH + 24 + ButtonH + Pad);

            BuildButtons(buttons);

            fade = new Tween(0f, 170, () =>
            {
                Opacity = FlashGuard.Lerp(fade!.Value);
                if (SlidePx != 0) Top = baseTop + (int)Math.Round((1f - fade.Value) * SlidePx);
            });
        }

        private void BuildButtons(MessageBoxButtons buttons)
        {
            (string Text, DialogResult Result, bool Primary)[] specs = buttons switch
            {
                MessageBoxButtons.OKCancel => new[]
                {
                    ("CANCEL", DialogResult.Cancel, false), ("OK", DialogResult.OK, true)
                },
                MessageBoxButtons.YesNo => new[]
                {
                    ("NO", DialogResult.No, false), ("YES", DialogResult.Yes, true)
                },
                MessageBoxButtons.YesNoCancel => new[]
                {
                    ("CANCEL", DialogResult.Cancel, false), ("NO", DialogResult.No, false), ("YES", DialogResult.Yes, true)
                },
                _ => new[] { ("OK", DialogResult.OK, true) }
            };

            int x = DialogWidth - Pad - ButtonW;
            int y = ClientSize.Height - Pad - ButtonH;
            PillButton? primary = null, cancel = null;

            for (int i = specs.Length - 1; i >= 0; i--)
            {
                var spec = specs[i];
                var b = new PillButton
                {
                    Text = spec.Text,
                    Font = UiFonts.SmallBold,
                    Size = new Size(ButtonW, ButtonH),
                    Location = new Point(x, y),
                    DialogResult = spec.Result
                };

                if (spec.Primary)
                {
                    b.GradientTop = light;
                    b.BackColor = deep;
                    b.ForeColor = Color.White;
                    b.BorderCol = light;
                    primary = b;
                }
                else
                {
                    b.ApplyTabStyle(false, animate: false);
                    if (spec.Result == DialogResult.Cancel || (cancel == null && spec.Result == DialogResult.No))
                        cancel = b;
                }

                Controls.Add(b);
                x -= ButtonW + ButtonGap;
            }

            AcceptButton = primary;           // enter
            CancelButton = cancel ?? primary; // esc
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            FlashGuard.DisableDwmTransitions(Handle);
            Native.RoundCorners(Handle);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            baseTop = Top;
            if (SlidePx != 0) Top = baseTop + SlidePx;

            Refresh();
            BeginInvoke(new Action(() =>
            {
                if (!IsDisposed) fade.Go(1f);
            }));
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            fade.Dispose();
            base.OnFormClosed(e);
        }

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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var full = new Rectangle(0, 0, Width, Height);

            using (var wash = new LinearGradientBrush(new Rectangle(0, 0, Width, 90),
                       Color.FromArgb(46, deep), Color.FromArgb(0, deep), 90f))
                g.FillRectangle(wash, 0, 0, Width, 90);

            using (var edge = new LinearGradientBrush(new Rectangle(0, 0, Width, 2), deep, light, 0f))
                g.FillRectangle(edge, 0, 0, Width, 2);

            using (var path = RoundRect(new Rectangle(1, 1, Width - 3, Height - 3), 7))
            using (var outlineBrush = new LinearGradientBrush(full, Color.FromArgb(150, deep), Color.FromArgb(150, light), 35f))
            using (var outline = new Pen(outlineBrush, 1f))
                g.DrawPath(outline, path);

            if (icon != MessageBoxIcon.None) DrawIcon(g);

            if (titleRect.Height > 0)
                TextRenderer.DrawText(g, title, UiFonts.Heading, titleRect, Theme.TextTitle,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            if (messageRect.Height > 0)
                TextRenderer.DrawText(g, text, UiFonts.Body, messageRect, Theme.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);

            var logo = PulseLogo.Image;
            if (logo != null)
            {
                const int s = 18;
                int y = Height - Pad - ButtonH + (ButtonH - s) / 2;

                var matrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.55f };
                using var attrs = new System.Drawing.Imaging.ImageAttributes();
                attrs.SetColorMatrix(matrix);

                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(logo, new Rectangle(Pad, y, s, s), 0, 0, logo.Width, logo.Height, GraphicsUnit.Pixel, attrs);
            }
        }
        private void DrawIcon(Graphics g)
        {
            var r = iconRect;
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;

            using (var gp = new GraphicsPath())
            {
                gp.AddEllipse(cx - 34, cy - 34, 68, 68);
                using var pg = new PathGradientBrush(gp)
                {
                    CenterColor = Color.FromArgb(70, light),
                    SurroundColors = new[] { Color.FromArgb(0, light) }
                };
                g.FillPath(pg, gp);
            }

            using var fill = new SolidBrush(Color.FromArgb(34, light));
            using var ringBrush = new LinearGradientBrush(r, light, deep, 45f);
            using var ring = new Pen(ringBrush, 1.8f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var glyph = new Pen(Color.White, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var dot = new SolidBrush(Color.White);

            switch (icon)
            {
                case MessageBoxIcon.Warning:
                    {
                        // rounded triangle with an exclamation mark
                        var tri = new[]
                        {
                        new PointF(cx, r.Y + 3f),
                        new PointF(r.Right - 2f, r.Bottom - 5f),
                        new PointF(r.X + 2f, r.Bottom - 5f)
                    };
                        g.FillPolygon(fill, tri);
                        g.DrawPolygon(ring, tri);
                        g.DrawLine(glyph, cx, cy - 6f, cx, cy + 3f);
                        g.FillEllipse(dot, cx - 1.6f, cy + 6f, 3.2f, 3.2f);
                        break;
                    }
                case MessageBoxIcon.Error:
                    g.FillEllipse(fill, r);
                    g.DrawEllipse(ring, r);
                    g.DrawLine(glyph, cx - 6f, cy - 6f, cx + 6f, cy + 6f);
                    g.DrawLine(glyph, cx + 6f, cy - 6f, cx - 6f, cy + 6f);
                    break;
                case MessageBoxIcon.Question:
                    g.FillEllipse(fill, r);
                    g.DrawEllipse(ring, r);
                    TextRenderer.DrawText(g, "?", UiFonts.Heading, r, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    break;
                default:
                    g.FillEllipse(fill, r);
                    g.DrawEllipse(ring, r);
                    g.FillEllipse(dot, cx - 1.8f, cy - 8.5f, 3.6f, 3.6f);
                    g.DrawLine(glyph, cx, cy - 2f, cx, cy + 8f);
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) fade.Dispose();
            base.Dispose(disposing);
        }
    }
}