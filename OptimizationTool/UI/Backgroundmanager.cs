using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace OptimizationTool.UI
{
    public static class BackgroundManager
    {
        private static readonly string[] Allowed = { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };
        private static readonly float[] Scales = { 1f, 0.85f, 0.75f, 0.6f, 0.5f, 0.4f, 0.3f, 0.25f };

        private const int MaxComposites = 6;
        private const bool PauseGifDuringAnimations = true;
        private const long GifBudgetBytes = 256L * 1024 * 1024;
        private const int RebakeDelayMs = 350;
        private const int IdlePollMs = 100;
        private const InterpolationMode BakeQuality = InterpolationMode.HighQualityBilinear;

        private sealed class Entry
        {
            public Bitmap Bmp = null!;
            public int Version = -1;
        }

        private static readonly Dictionary<Size, Entry> composites = new();
        private static readonly Queue<Size> order = new();
        private static int version;

        // source image
        private static Image? source;
        private static MemoryStream? stream;
        private static float dim = 0.55f;
        private static Bitmap? staticBase;
        private static Size staticBaseSize;

        // gif playback
        private sealed class GifData : IDisposable
        {
            public Bitmap[] Frames = Array.Empty<Bitmap>();
            public int[] EndTimes = Array.Empty<int>();
            public int Total = 1;
            public int Generation;
            public Size Target;
            public long Epoch;

            public int FrameAt(long t)
            {
                int lo = 0, hi = EndTimes.Length - 1;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    if (t < EndTimes[mid]) hi = mid; else lo = mid + 1;
                }
                return lo;
            }

            public void Dispose()
            {
                foreach (var f in Frames) f.Dispose();
                Frames = Array.Empty<Bitmap>();
            }
        }

        private static SynchronizationContext? ui;
        private static byte[]? gifBytes;
        private static GifData? gif;
        private static int generation;
        private static bool animated;
        private static int frameIndex;
        private static Size lastSeenTarget;
        private static int tickPending;
        private static int idle;
        private static CancellationTokenSource? playCts;
        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static System.Windows.Forms.Timer? rebakeTimer;

        private static bool moving;
        private static MoveHook? hook;
        private const int CalmMs = 150;
        private static Rectangle lastBounds;
        private static long calmAt;
        private static int maxFps = 60;
        public static int MaxFps
        {
            get => Volatile.Read(ref maxFps);
            set => Volatile.Write(ref maxFps, Math.Clamp(value, 5, 144));
        }
        private static double renderMs;
        private static int renders, evictions, presents;
        private static long statsAt;
        private static string stats = "";
        public static string DebugInfo => stats;

        private static void RollStats()
        {
            long now = clock.ElapsedMilliseconds;
            if (now - statsAt < 1000) return;
            stats = $"{presents} present/s | {renders} renders/s | {renderMs:0.0} ms/render | {evictions} evictions/s | cap {MaxFps}";
            statsAt = now;
            presents = renders = evictions = 0;
        }
        private sealed class MoveHook : NativeWindow
        {
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x0231) SetInteracting(true);        // enter
                else if (m.Msg == 0x0232) SetInteracting(false);  // exit
                base.WndProc(ref m);
            }
        }
        public static void SetInteracting(bool on)
        {
            if (moving == on) return;
            moving = on;
            Volatile.Write(ref idle, on ? 1 : 0);

            if (!on)
            {
                version++;
                Host?.Invalidate(true);
                RaiseTick();
            }
        }

        [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint ms);

        static BackgroundManager()
        {
            Theme.Changed += (s, e) => version++;
        }

        public static Control? Host { get; set; }
        public static string? CurrentPath { get; private set; }
        public static bool HasImage => source != null;

        public static event EventHandler? Changed;

        public static float Dim
        {
            get => dim;
            set
            {
                float clamped = Math.Clamp(value, 0f, 0.95f);
                if (Math.Abs(dim - clamped) > 0.001f)
                {
                    dim = clamped;
                    version++;
                    Changed?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        // loading

        public static bool Load(string path, out string error)
        {
            error = "";
            MemoryStream? ms = null;
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (Array.IndexOf(Allowed, ext) < 0)
                {
                    error = "Unsupported file type. Use png, jpg, gif or bmp.";
                    return false;
                }

                byte[] bytes = File.ReadAllBytes(path);
                ms = new MemoryStream(bytes);

                Image img;
                try
                {
                    img = Image.FromStream(ms, false, true);
                }
                catch
                {
                    ms.Dispose();
                    error = "Couldn't read that image format.";
                    return false;
                }

                Clear(notify: false);

                ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

                stream = ms;
                source = img;
                CurrentPath = path;
                BeginGif(img, bytes);

                version++;
                Changed?.Invoke(null, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                ms?.Dispose();
                error = ex.Message;
                return false;
            }
        }

        public static void Clear(bool notify = true)
        {
            Interlocked.Increment(ref generation);

            StopPlayback();
            StopRebakeTimer();
            hook?.ReleaseHandle();
            moving = false;

            var old = gif;
            Volatile.Write(ref gif, null);
            old?.Dispose();
            gifBytes = null;

            staticBase?.Dispose();
            staticBase = null;

            source?.Dispose();
            source = null;

            stream?.Dispose();
            stream = null;

            CurrentPath = null;
            animated = false;
            frameIndex = 0;
            version++;

            if (notify) Changed?.Invoke(null, EventArgs.Empty);
        }

        // gif

        private static void BeginGif(Image img, byte[] bytes)
        {
            animated = false;
            try
            {
                if (img.FrameDimensionsList.Length == 0) return;
                var fd = new FrameDimension(img.FrameDimensionsList[0]);
                if (img.GetFrameCount(fd) <= 1) return;
            }
            catch { return; }

            animated = true;
            frameIndex = 0;
            gifBytes = bytes;
            lastSeenTarget = TargetSize();

            StartPreRender(Volatile.Read(ref generation));
        }

        private static void StartPreRender(int gen)
        {
            var bytes = gifBytes;
            if (bytes == null) return;

            Size target = TargetSize();
            var post = ui;

            new Thread(() => PreRender(bytes, target, gen, post))
            {
                IsBackground = true,
                Name = "GifPreRender",
                Priority = ThreadPriority.BelowNormal
            }.Start();
        }

        private static void ScheduleRebake()
        {
            if (!animated || gifBytes == null) return;

            if (rebakeTimer == null)
            {
                rebakeTimer = new System.Windows.Forms.Timer { Interval = RebakeDelayMs };
                rebakeTimer.Tick += OnRebakeTick;
            }
            rebakeTimer.Stop();
            rebakeTimer.Start();
        }

        private static void OnRebakeTick(object? sender, EventArgs e)
        {
            rebakeTimer?.Stop();
            if (moving) { rebakeTimer?.Start(); return; }
            if (!animated || gifBytes == null) return;

            Size target = TargetSize();
            var data = gif;
            if (data != null && data.Target == target) return;

            int gen = Interlocked.Increment(ref generation);
            StartPreRender(gen);
        }

        private static Size TargetSize()
        {
            var host = Host;
            Size s = host?.FindForm()?.ClientSize ?? host?.ClientSize ?? Size.Empty;
            return s.Width < 1 || s.Height < 1 ? new Size(1100, 700) : s;
        }

        private static int Scaled(int v, float k) => Math.Max(1, (int)Math.Round(v * k));
        private static void PreRender(byte[] bytes, Size target, int gen, SynchronizationContext? post)
        {
            var frames = new List<Bitmap>();
            bool handedOff = false;

            try
            {
                if (post == null) return;

                using var ms = new MemoryStream(bytes);
                using var img = Image.FromStream(ms, false, false);

                var fd = new FrameDimension(img.FrameDimensionsList[0]);
                int count = img.GetFrameCount(fd);
                int[] delays = ReadDelays(img, count);

                float k = Scales[^1];
                bool fits = false;
                foreach (float s in Scales)
                {
                    long all = (long)count * Scaled(target.Width, s) * Scaled(target.Height, s) * 4;
                    if (all <= GifBudgetBytes) { k = s; fits = true; break; }
                }

                int w = Scaled(target.Width, k), h = Scaled(target.Height, k);
                int step = fits ? 1 : (int)Math.Ceiling(count * (double)w * h * 4 / GifBudgetBytes);

                var ends = new List<int>();
                int acc = 0;

                for (int n = 0; n < count; n += step)
                {
                    if (gen != Volatile.Read(ref generation)) return; // cancelled

                    img.SelectActiveFrame(fd, n);

                    var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Black);
                        DrawCover(g, img, new Size(w, h), BakeQuality);
                    }
                    frames.Add(bmp);
                    for (int m = n; m < Math.Min(n + step, count); m++) acc += delays[m];
                    ends.Add(acc);
                }

                if (frames.Count == 0 || gen != Volatile.Read(ref generation)) return;

                var data = new GifData
                {
                    Frames = frames.ToArray(),
                    EndTimes = ends.ToArray(),
                    Total = Math.Max(1, acc),
                    Generation = gen,
                    Target = target
                };
                handedOff = true;
                post.Post(_ => Adopt(data), null);
            }
            catch
            {
            }
            finally
            {
                if (!handedOff) foreach (var f in frames) f.Dispose();
            }
        }

        // UI thread
        private static void Adopt(GifData ready)
        {
            if (ready.Generation != Volatile.Read(ref generation))
            {
                ready.Dispose();
                return;
            }

            var old = gif;

            ready.Epoch = old?.Epoch ?? clock.ElapsedMilliseconds;
            Volatile.Write(ref gif, ready);
            old?.Dispose();

            staticBase?.Dispose();
            staticBase = null;

            frameIndex = ready.FrameAt((clock.ElapsedMilliseconds - ready.Epoch) % ready.Total);
            version++;
            if (!moving) Host?.Invalidate(true);

            EnsurePlayback();
        }

        private static int[] ReadDelays(Image img, int count)
        {
            var delays = new int[count];
            for (int i = 0; i < count; i++) delays[i] = 100;

            try
            {
                var item = img.GetPropertyItem(0x5100);
                if (item?.Value != null)
                {
                    for (int i = 0; i < count && i * 4 + 4 <= item.Value.Length; i++)
                    {
                        int ms = BitConverter.ToInt32(item.Value, i * 4) * 10;
                        delays[i] = ms <= 10 ? 100 : ms;
                    }
                }
            }
            catch { /* keep defaults */ }

            return delays;
        }

        private static void EnsurePlayback()
        {
            if (playCts != null) return;

            var cts = new CancellationTokenSource();
            playCts = cts;

            new Thread(() => PlayLoop(cts.Token))
            {
                IsBackground = true,
                Name = "GifClock",
                Priority = ThreadPriority.AboveNormal
            }.Start();
        }

        private static void StopPlayback()
        {
            playCts?.Cancel();
            playCts = null;
            Volatile.Write(ref idle, 0);
        }

        private static void StopRebakeTimer()
        {
            if (rebakeTimer == null) return;
            rebakeTimer.Stop();
            rebakeTimer.Tick -= OnRebakeTick;
            rebakeTimer.Dispose();
            rebakeTimer = null;
        }

        private static void PlayLoop(CancellationToken ct)
        {
            timeBeginPeriod(1);
            try
            {
                GifData? lastData = null;
                int lastIdx = -1;
                long lastRaise = long.MinValue / 2;

                while (!ct.IsCancellationRequested)
                {
                    var data = Volatile.Read(ref gif);
                    if (data == null || data.Total < 1)
                    {
                        if (ct.WaitHandle.WaitOne(20)) break;
                        continue;
                    }

                    if (Volatile.Read(ref idle) != 0)
                    {
                        RaiseTick();
                        if (ct.WaitHandle.WaitOne(IdlePollMs)) break;
                        continue;
                    }

                    long now = clock.ElapsedMilliseconds;
                    long pos = (now - data.Epoch) % data.Total;
                    int idx = data.FrameAt(pos);

                    if (idx != lastIdx || !ReferenceEquals(data, lastData))
                    {
                        long gap = now - lastRaise;
                        long minGap = 1000 / Volatile.Read(ref maxFps);
                        if (gap < minGap)
                        {
                            if (ct.WaitHandle.WaitOne((int)(minGap - gap))) break;
                            continue;
                        }

                        lastIdx = idx;
                        lastData = data;
                        lastRaise = now;
                        RaiseTick();
                    }

                    int wait = (int)Math.Max(1, data.EndTimes[idx] - pos);
                    if (ct.WaitHandle.WaitOne(wait)) break;
                }
            }
            catch (ObjectDisposedException) { }
            finally
            {
                timeEndPeriod(1);
            }
        }

        private static void RaiseTick()
        {
            if (Interlocked.Exchange(ref tickPending, 1) != 0) return;
            try { ui?.Post(OnTick, null); }
            catch { Volatile.Write(ref tickPending, 0); }
        }

        // UI thread
        private static void OnTick(object? _)
        {
            Volatile.Write(ref tickPending, 0);

            var data = gif;
            var host = Host;
            if (data == null || host == null) return;

            var form = host.FindForm();
            if (form != null && form.IsHandleCreated && (hook == null || hook.Handle != form.Handle))
            {
                try
                {
                    hook ??= new MoveHook();
                    if (hook.Handle != IntPtr.Zero) hook.ReleaseHandle();
                    hook.AssignHandle(form.Handle);
                }
                catch { }
            }

            bool visible = host.IsHandleCreated && host.Visible
                           && form?.WindowState != FormWindowState.Minimized;

            long nowMs = clock.ElapsedMilliseconds;
            if (form != null && form.Bounds != lastBounds)
            {
                lastBounds = form.Bounds;
                calmAt = nowMs + CalmMs;
            }

            Volatile.Write(ref idle, visible && !moving ? 0 : 1);
            if (!visible || moving || nowMs < calmAt) return;

            Size target = TargetSize();
            if (target != lastSeenTarget)
            {
                lastSeenTarget = target;
                ScheduleRebake();
            }

            if (PauseGifDuringAnimations && OptimizationTool.UI.Controls.Tween.AnyAnimating) return;

            int idx = data.FrameAt((clock.ElapsedMilliseconds - data.Epoch) % data.Total);
            if (idx == frameIndex) return;

            frameIndex = idx;
            version++;
            presents++;
            host.Invalidate(true);
            RollStats();
        }

        // cache

        public static Bitmap? GetCache(Size size)
        {
            if (size.Width < 1 || size.Height < 1) return null;

            if (!composites.TryGetValue(size, out var entry))
            {
                entry = new Entry { Bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb) };
                composites[size] = entry;
                order.Enqueue(size);

                while (order.Count > MaxComposites)
                {
                    var old = order.Dequeue();
                    if (composites.Remove(old, out var dead)) { dead.Bmp.Dispose(); evictions++; }
                }
            }

            if (entry.Version != version)
            {
                long t0 = Stopwatch.GetTimestamp();
                Render(entry.Bmp, size);
                entry.Version = version;

                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                renderMs = renderMs * 0.9 + ms * 0.1;
                renders++;
                RollStats();
            }

            return entry.Bmp;
        }

        private static void Render(Bitmap bmp, Size size)
        {
            try
            {
                using var g = Graphics.FromImage(bmp);
                var full = new Rectangle(0, 0, size.Width, size.Height);

                if (source == null)
                {
                    g.Clear(Theme.WindowBg);
                    using var brush = new LinearGradientBrush(full, Theme.WindowGradientStart, Theme.WindowGradientEnd, 45f);
                    g.FillRectangle(brush, full);
                    return;
                }

                g.CompositingMode = CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighSpeed;

                var data = gif;
                if (data != null && data.Frames.Length > 0)
                {
                    var f = data.Frames[Math.Min(frameIndex, data.Frames.Length - 1)];

                    if (f.Size == size)
                    {
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.DrawImage(f, full, 0, 0, f.Width, f.Height, GraphicsUnit.Pixel);
                    }
                    else
                    {
                        DrawCover(g, f, size, InterpolationMode.Bilinear);
                    }
                }
                else
                {
                    var b = GetStaticBase(size);
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.DrawImage(b, full, 0, 0, b.Width, b.Height, GraphicsUnit.Pixel);
                }

                if (dim > 0.001f)
                {
                    g.CompositingMode = CompositingMode.SourceOver;
                    using var dimBrush = new SolidBrush(Color.FromArgb((int)(dim * 255f), 0, 0, 0));
                    g.FillRectangle(dimBrush, full);
                }
            }
            catch
            {
                try
                {
                    using var g = Graphics.FromImage(bmp);
                    g.Clear(Theme.WindowBg);
                }
                catch { }
            }
        }

        private static Bitmap GetStaticBase(Size size)
        {
            if (staticBase != null && staticBaseSize == size) return staticBase;

            staticBase?.Dispose();

            var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                DrawCover(g, source!, size,
                    animated ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBicubic);
            }

            staticBase = bmp;
            staticBaseSize = size;
            return bmp;
        }

        private static void DrawCover(Graphics g, Image src, Size size, InterpolationMode mode)
        {
            g.InterpolationMode = mode;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            float scale = Math.Max(size.Width / (float)src.Width, size.Height / (float)src.Height);
            float w = src.Width * scale;
            float h = src.Height * scale;

            using var attrs = new ImageAttributes();
            attrs.SetWrapMode(WrapMode.TileFlipXY);

            g.DrawImage(src,
                new Rectangle((int)Math.Floor((size.Width - w) / 2f), (int)Math.Floor((size.Height - h) / 2f),
                              (int)Math.Ceiling(w), (int)Math.Ceiling(h)),
                0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
        }
    }
}