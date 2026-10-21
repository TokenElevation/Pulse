using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace OptimizationTool.UI.Controls
{
    public static class GradientBackground
    {
        public static void Paint(Control c, Graphics g)
        {
            var clip = Rectangle.Ceiling(g.ClipBounds);
            clip.Intersect(c.ClientRectangle);
            if (clip.IsEmpty) return;

            var form = c.FindForm();
            Bitmap? bg = null;
            if (form != null && form.ClientSize.Width > 0 && form.ClientSize.Height > 0)
                bg = BackgroundManager.GetCache(form.ClientSize);

            if (form == null || bg == null)
            {
                g.Clear(Theme.WindowBg);
                return;
            }

            Point origin = form.PointToClient(c.PointToScreen(Point.Empty));

            var src = new Rectangle(clip.X + origin.X, clip.Y + origin.Y, clip.Width, clip.Height);
            src.Intersect(new Rectangle(0, 0, bg.Width, bg.Height));

            if (src.Width != clip.Width || src.Height != clip.Height)
                g.Clear(Theme.WindowBg);

            if (src.IsEmpty) return;

            var dest = new Rectangle(src.X - origin.X, src.Y - origin.Y, src.Width, src.Height);

            var oldComposite = g.CompositingMode;
            var oldInterp = g.InterpolationMode;

            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(bg, dest, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel);

            g.CompositingMode = oldComposite;
            g.InterpolationMode = oldInterp;
        }
    }
    public sealed class SlideSnapshot : Control
    {
        private Bitmap? image;

        public SlideSnapshot()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.Opaque |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            TabStop = false;
            Visible = false;
        }

        public bool HasImage => image != null;

        public new void Capture(Control source)
        {
            var size = source.Size;
            if (size.Width < 1 || size.Height < 1) return;

            var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppRgb);
            source.DrawToBitmap(bmp, new Rectangle(Point.Empty, size));

            image?.Dispose();
            image = bmp;
            Size = size;
        }

        public void Release()
        {
            image?.Dispose();
            image = null;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { /* the whole surface is the picture */ }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (image == null) return;

            var g = e.Graphics;
            var r = Rectangle.Intersect(e.ClipRectangle, new Rectangle(0, 0, image.Width, image.Height));
            if (r.IsEmpty) return;

            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(image, r, r.X, r.Y, r.Width, r.Height, GraphicsUnit.Pixel);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Release();
            base.Dispose(disposing);
        }
    }
    public class TransparentPanel : Panel
    {
        public TransparentPanel()
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
        }
    }

    public class GradientPanel : Panel
    {
        public GradientPanel()
        {
            DoubleBuffered = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e) => GradientBackground.Paint(this, e.Graphics);
    }
    public class BufferedFlowPanel : FlowLayoutPanel
    {
        public BufferedFlowPanel()
        {
            DoubleBuffered = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e) => GradientBackground.Paint(this, e.Graphics);
    }

    public class SheenLabel : Control
    {
        public SheenLabel()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        private void Fit()
        {
            var sz = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            Size = new Size(sz.Width + 12, sz.Height + 6);
        }

        protected override void OnTextChanged(EventArgs e) { Fit(); Invalidate(); base.OnTextChanged(e); }
        protected override void OnFontChanged(EventArgs e) { Fit(); Invalidate(); base.OnFontChanged(e); }

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

            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using var brush = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height)),
                Theme.SheenStart, Theme.SheenEnd, 0f)
            {
                InterpolationColors = new ColorBlend(3)
                {
                    Colors = new[] { Theme.SheenStart, Theme.SheenMid, Theme.SheenEnd },
                    Positions = new[] { 0f, 0.5f, 1f }
                }
            };

            g.DrawString(Text, Font, brush, 0, 3, StringFormat.GenericTypographic);
        }
    }
}