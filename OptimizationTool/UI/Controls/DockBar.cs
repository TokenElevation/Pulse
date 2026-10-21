using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OptimizationTool.UI.Controls
{
    public class DockBar : Panel
    {
        public DockBar()
        {
            DoubleBuffered = true;
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

            if (Width < 40 || Height < 10) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int d = rect.Height;
            using var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 90, 180);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 180);
            path.CloseFigure();

            using var fill = new SolidBrush(Color.FromArgb(228, Theme.Surface));
            using var pen = new Pen(Theme.SurfaceBorder);
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
            var span = new Rectangle(0, 0, Width, Height);
            var blend = new ColorBlend
            {
                Positions = new[] { 0f, 0.5f, 1f },
                Colors = new[]
                {
                    Color.FromArgb(0, Theme.GlowDeep),
                    Color.FromArgb(150, Theme.GlowLight),
                    Color.FromArgb(0, Theme.GlowDeep)
                }
            };

            var state2 = g.Save();
            g.SetClip(path);
            for (int i = 1; i <= 2; i++)
            {
                using var br = new LinearGradientBrush(span, Theme.GlowDeep, Theme.GlowLight, 0f)
                {
                    InterpolationColors = ScaleAlpha(blend, 0.22f / i)
                };
                using var glow = new Pen(br, i * 3f);
                g.DrawPath(glow, path);
            }
            g.Restore(state2);

            using var edgeBrush = new LinearGradientBrush(span, Theme.GlowDeep, Theme.GlowLight, 0f)
            {
                InterpolationColors = blend
            };
            using var edge = new Pen(edgeBrush, 1f);
            g.DrawPath(edge, path);
        }

        private static ColorBlend ScaleAlpha(ColorBlend src, float k)
        {
            var colors = new Color[src.Colors.Length];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = Color.FromArgb((int)(src.Colors[i].A * k), src.Colors[i]);

            return new ColorBlend { Positions = (float[])src.Positions.Clone(), Colors = colors };
        }
    }
}