using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OptimizationTool.UI.Controls
{
    public class PillButton : Button
    {
        public Color BorderCol { get; set; } = Theme.Border;
        public Color GradientTop { get; set; } = Color.Empty;

        private readonly Tween hoverT;
        private readonly Tween styleT;

        private bool useStyle;
        private Color fTop, fBottom, fFg, fBorder;
        private Color tTop, tBottom, tFg, tBorder;

        public PillButton()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;

            hoverT = new Tween(0, 150, () => Invalidate());
            styleT = new Tween(1, 220, () => Invalidate());
        }

        protected override bool ShowFocusCues => false;

        protected override void OnMouseEnter(EventArgs e) { hoverT.Go(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hoverT.Go(0); base.OnMouseLeave(e); }

        public void ApplyStyle(Color top, Color bottom, Color fg, Color border, bool animate = true)
        {
            if (useStyle && animate)
            {
                float s = styleT.Value;
                fTop = Theme.Lerp(fTop, tTop, s);
                fBottom = Theme.Lerp(fBottom, tBottom, s);
                fFg = Theme.Lerp(fFg, tFg, s);
                fBorder = Theme.Lerp(fBorder, tBorder, s);

                tTop = top; tBottom = bottom; tFg = fg; tBorder = border;
                styleT.Snap(0);
                styleT.Go(1);
            }
            else
            {
                fTop = tTop = top; fBottom = tBottom = bottom; fFg = tFg = fg; fBorder = tBorder = border;
                styleT.Snap(1);
            }
            useStyle = true;
        }

        public void ApplyTabStyle(bool active, bool animate = true) => ApplyStyle(
            active ? Theme.TabOnTop : Theme.TabOffTop,
            active ? Theme.TabOnBottom : Theme.TabOffBottom,
            active ? Theme.TabOnText : Theme.TabOffText,
            active ? Theme.TabOnBorder : Theme.Border,
            animate);

        private static Color Hover(Color c) =>
            c.GetBrightness() > 0.5f ? ControlPaint.Dark(c, 0.05f) : ControlPaint.Light(c, 0.12f);

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

            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color top, bottom, fg, border;
            bool gradient;
            if (useStyle)
            {
                float s = styleT.Value;
                top = Theme.Lerp(fTop, tTop, s);
                bottom = Theme.Lerp(fBottom, tBottom, s);
                fg = Theme.Lerp(fFg, tFg, s);
                border = Theme.Lerp(fBorder, tBorder, s);
                gradient = true;
            }
            else
            {
                top = GradientTop;
                bottom = BackColor;
                fg = ForeColor;
                border = BorderCol;
                gradient = !GradientTop.IsEmpty;
            }

            float h = hoverT.Value;
            bottom = Theme.Lerp(bottom, Hover(bottom), h);
            if (gradient) top = Theme.Lerp(top, Hover(top), h);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int d = rect.Height;
            using var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 90, 180);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 180);
            path.CloseFigure();

            if (gradient)
            {
                using var gb = new LinearGradientBrush(new Rectangle(0, 0, Width, Math.Max(1, Height)), top, bottom, 90f);
                g.FillPath(gb, path);
            }
            else
            {
                using var sb = new SolidBrush(bottom);
                g.FillPath(sb, path);
            }

            using var pen = new Pen(border);
            g.DrawPath(pen, path);

            TextRenderer.DrawText(g, Text, Font, rect, fg,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                hoverT.Dispose();
                styleT.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}