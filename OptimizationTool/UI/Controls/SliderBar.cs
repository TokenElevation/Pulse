using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OptimizationTool.UI.Controls
{
    public class SliderBar : Control
    {
        private const int KnobR = 7;
        private int value;
        private bool dragging;

        public int Minimum { get; set; } = 0;
        public int Maximum { get; set; } = 100;

        public event EventHandler? ValueChanged;
        public event EventHandler? ValueCommitted; // raised when the mouse is released

        public int Value
        {
            get => value;
            set
            {
                int v = Math.Max(Minimum, Math.Min(Maximum, value));
                if (v == this.value) return;
                this.value = v;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public SliderBar()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Height = 22;
            Cursor = Cursors.Hand;
        }

        private void SetFromX(int x)
        {
            float frac = (x - KnobR) / (float)Math.Max(1, Width - KnobR * 2);
            frac = Math.Max(0f, Math.Min(1f, frac));
            Value = Minimum + (int)Math.Round(frac * (Maximum - Minimum));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            SetFromX(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) SetFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging) return;
            dragging = false;
            ValueCommitted?.Invoke(this, EventArgs.Empty);
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

            var track = new Rectangle(KnobR, Height / 2 - 2, Math.Max(10, Width - KnobR * 2), 4);
            float frac = (Value - Minimum) / (float)Math.Max(1, Maximum - Minimum);

            using (var trackPath = Pill(track))
            using (var trackBrush = new SolidBrush(Theme.BorderHover))
            {
                g.FillPath(trackBrush, trackPath);
            }

            var fill = new Rectangle(track.X, track.Y, Math.Max(4, (int)(track.Width * frac)), track.Height);
            using (var fillPath = Pill(fill))
            using (var fillBrush = new LinearGradientBrush(fill, Color.White, Theme.Accent, 0f))
            {
                g.FillPath(fillBrush, fillPath);
            }

            float cx = track.X + track.Width * frac;
            using var knob = new SolidBrush(Color.White);
            g.FillEllipse(knob, cx - KnobR, Height / 2f - KnobR, KnobR * 2, KnobR * 2);
        }
    }
}