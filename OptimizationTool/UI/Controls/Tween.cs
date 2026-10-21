using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace OptimizationTool.UI.Controls
{
    public sealed class Tween : IDisposable
    {
        private static bool enabled = true;

        public static bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled == value) return;
                enabled = value;
                if (!value)
                {
                    foreach (var t in Active.ToArray()) t.Snap(t.target);
                }
            }
        }

        private static readonly List<Tween> Active = new();
        private static readonly List<Tween> Scratch = new();

        private const float MaxDt = 0.05f;

        // vsync clock
        [DllImport("dwmapi.dll")]
        private static extern int DwmFlush();

        private static readonly ManualResetEventSlim Gate = new(false);
        private static Thread? vsyncThread;
        private static SynchronizationContext? uiContext;
        private static int framePending;

        // fallback clocl
        private static System.Windows.Forms.Timer? fallbackClock;

        private readonly Action onUpdate;
        private readonly float omega;
        private float value;
        private float velocity;
        private float target;
        private float epsilon;
        private long lastTicks;
        private bool running;
        private bool disposed;

        public static bool AnyAnimating => Active.Count > 0;

        public float Value => value;
        public float Target => target;
        public bool IsAnimating => running;

        public Tween(float initial, int durationMs, Action onUpdate)
        {
            value = target = initial;
            omega = 8f / (Math.Max(1, durationMs) / 1000f);
            this.onUpdate = onUpdate;
        }

        public void Go(float newTarget)
        {
            if (disposed) return;

            if (Math.Abs(newTarget - target) < 0.0001f && (running || Math.Abs(newTarget - value) < 0.0001f)) return;

            if (!enabled)
            {
                Snap(newTarget);
                return;
            }

            target = newTarget;
            epsilon = 0.0025f * Math.Max(1f, Math.Abs(target - value));

            if (running) return;

            lastTicks = Stopwatch.GetTimestamp();
            running = true;
            Active.Add(this);
            EnsureClock();
        }

        public void Snap(float newValue)
        {
            Stop();
            value = target = newValue;
            velocity = 0f;
            Notify();
        }

        private void Stop()
        {
            if (!running) return;
            running = false;
            Active.Remove(this);
        }

        private void Notify()
        {
            if (disposed) return;
            try
            {
                onUpdate();
            }
            catch
            {
                Stop();
            }
        }

        // clock

        private static void EnsureClock()
        {
            uiContext ??= SynchronizationContext.Current;

            if (uiContext == null)
            {
                if (fallbackClock == null)
                {
                    fallbackClock = new System.Windows.Forms.Timer { Interval = 15 };
                    fallbackClock.Tick += (s, e) => TickAll();
                }
                if (!fallbackClock.Enabled) fallbackClock.Start();
                return;
            }

            if (vsyncThread == null)
            {
                vsyncThread = new Thread(VsyncLoop) { IsBackground = true, Name = "TweenVsync" };
                vsyncThread.Start();
            }

            Gate.Set();
        }

        private static void VsyncLoop()
        {
            while (true)
            {
                Gate.Wait();

                int hr;
                try { hr = DwmFlush(); }
                catch { hr = -1; }
                if (hr != 0) Thread.Sleep(15);

                if (Interlocked.Exchange(ref framePending, 1) == 0)
                {
                    uiContext!.Post(_ =>
                    {
                        Interlocked.Exchange(ref framePending, 0);
                        TickAll();
                    }, null);
                }
            }
        }

        private static void TickAll()
        {
            if (Active.Count == 0)
            {
                Gate.Reset();
                fallbackClock?.Stop();
                return;
            }

            long now = Stopwatch.GetTimestamp();

            Scratch.Clear();
            Scratch.AddRange(Active);
            foreach (var tween in Scratch) tween.Step(now);
            Scratch.Clear();

            if (Active.Count == 0)
            {
                Gate.Reset();
                fallbackClock?.Stop();
            }
        }

        private void Step(long now)
        {
            if (!running) return;

            float dt = (float)((now - lastTicks) / (double)Stopwatch.Frequency);
            lastTicks = now;
            if (dt <= 0f) return;
            if (dt > MaxDt) dt = MaxDt;

            float x = value - target;
            float e = MathF.Exp(-omega * dt);
            float k = velocity + omega * x;
            float newX = (x + k * dt) * e;
            float newV = (velocity - omega * k * dt) * e;

            bool crossed = (x > 0f && newX < 0f) || (x < 0f && newX > 0f);
            bool settled = Math.Abs(newX) < epsilon && Math.Abs(newV) < epsilon * omega;

            if (crossed || settled)
            {
                value = target;
                velocity = 0f;
                Stop();
            }
            else
            {
                value = target + newX;
                velocity = newV;
            }

            Notify();
        }

        public void Dispose()
        {
            Stop();
            disposed = true;
        }
    }
}