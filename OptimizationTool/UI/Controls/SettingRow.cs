using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OptimizationTool.UI.Controls
{

    public class SettingRow : Panel
    {
        private readonly Control? right;
        private string description;

        public string Title { get; set; }

        public string Description
        {
            get => description;
            set { description = value; Invalidate(); }
        }

        protected bool TopAligned { get; set; }

        public SettingRow(string title, string description, Control? right = null, int height = 64)
        {
            DoubleBuffered = true;
            Title = title;
            this.description = description;
            this.right = right;
            Height = height;
            if (right != null) Controls.Add(right);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (right != null)
                right.Location = new Point(Width - 18 - right.Width, (Height - right.Height) / 2);
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
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

            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundRect(rect, 12);
            using var fill = new SolidBrush(Color.FromArgb(235, Theme.Surface));
            using var pen = new Pen(Theme.SurfaceBorder);
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            bool hasDesc = description.Length > 0;
            int reserve = right != null ? right.Width + 36 : 18;
            int textW = Math.Max(40, Width - 18 - reserve);
            int blockH = hasDesc ? 40 : 20;
            int y0 = TopAligned ? 14 : (Height - blockH) / 2;

            const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                                          TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;

            TextRenderer.DrawText(e.Graphics, Title, UiFonts.Title,
                new Rectangle(18, y0, textW, 20), Theme.TextTitle, flags);

            if (hasDesc)
                TextRenderer.DrawText(e.Graphics, description, UiFonts.Small,
                    new Rectangle(18, y0 + 22, textW, 18), Theme.TextSecondary, flags);
        }

        internal static GraphicsPath RoundRect(Rectangle r, int radius)
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
    }

    public class ThemePicker : SettingRow
    {
        private const int SwW = 112, SwH = 70, Gap = 14, SwX = 18, SwY = 64;
        private int hover = -1;

        public event EventHandler<ThemePreset>? PresetChosen;

        public ThemePicker() : base("Theme", "Pick a look. It applies instantly.", null, 172)
        {
            TopAligned = true;
        }

        private Rectangle SwatchRect(int i) => new(SwX + i * (SwW + Gap), SwY, SwW, SwH);

        private int HitTest(Point p)
        {
            for (int i = 0; i < Theme.Presets.Length; i++)
                if (SwatchRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitTest(e.Location);
            if (idx == hover) return;
            hover = idx;
            Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int idx = HitTest(e.Location);
            if (idx >= 0) PresetChosen?.Invoke(this, Theme.Presets[idx]);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            for (int i = 0; i < Theme.Presets.Length; i++)
            {
                var p = Theme.Presets[i];
                var r = SwatchRect(i);
                bool selected = p.Name == Theme.Current.Name;

                // mini preview of a card in this theme 
                Color previewBg = Theme.Lerp(Color.FromArgb(16, 16, 16), p.Tint, p.TintAmount * 1.5f);
                Color border = selected ? p.Accent : (i == hover ? Theme.BorderHover : Theme.Border);

                using (var path = RoundRect(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), 10))
                using (var fill = new SolidBrush(previewBg))
                using (var pen = new Pen(border, selected ? 2f : 1f))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                }

                using (var accent = new SolidBrush(p.Accent))
                using (var line = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                {
                    g.FillRectangle(accent, r.X + 12, r.Y + 14, 38, 8);
                    g.FillRectangle(line, r.X + 12, r.Y + 32, 70, 5);
                    g.FillRectangle(line, r.X + 12, r.Y + 44, 48, 5);

                    if (selected)
                        g.FillEllipse(accent, r.Right - 22, r.Y + 10, 10, 10);
                }

                TextRenderer.DrawText(g, p.Name, UiFonts.Small,
                    new Rectangle(r.X, r.Bottom + 6, r.Width, 18),
                    selected ? Theme.TextTitle : Theme.TextSecondary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding);
            }
        }
    }
}