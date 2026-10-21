using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OptimizationTool.UI.Controls
{
    public class ToggleSwitch : Control
    {
        private readonly Tween slide;
        private bool isChecked;

        public event EventHandler? CheckedChanged;

        public bool Checked
        {
            get => isChecked;
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                slide.Go(value ? 1f : 0f);
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public void SetCheckedSilently(bool value)
        {
            isChecked = value;
            slide.Snap(value ? 1f : 0f);
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Size = new Size(44, 24);
            Cursor = Cursors.Hand;
            slide = new Tween(0, 200, () => Invalidate());
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        private static GraphicsPath Pill(Rectangle r)
        {
            int d = r.Height;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 90, 180);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            p.CloseFigure();
            return p;
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

            g.SmoothingMode = SmoothingMode.AntiAlias;
            float a = slide.Value;

            var track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Pill(track))
            using (var brush = new SolidBrush(Theme.Lerp(Color.Black, Theme.Accent, a)))
            {
                g.FillPath(brush, path);

                int outlineAlpha = (int)(70 * (1f - a));
                if (outlineAlpha > 0)
                {
                    using var pen = new Pen(Color.FromArgb(outlineAlpha, 255, 255, 255));
                    g.DrawPath(pen, path);
                }
            }

            int knob = Height - 9;
            float x = 4 + (Width - 8 - knob) * a;
            using var kb = new SolidBrush(Theme.Lerp(Color.White, Color.FromArgb(10, 10, 10), a));
            g.FillEllipse(kb, x, 4, knob, knob);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) slide.Dispose();
            base.Dispose(disposing);
        }
    }
}