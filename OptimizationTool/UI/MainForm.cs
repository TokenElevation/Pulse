using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OptimizationTool.Core.Interfaces;
using OptimizationTool.Core.Tweaks;
using OptimizationTool.Services;
using OptimizationTool.UI.Controls;

namespace OptimizationTool.UI
{
    internal sealed class StatusPill : Control
    {
        private static Color Cyan => Theme.GlowLight;
        private static Color Blue => Theme.GlowDeep;
        private static readonly Color Muted = Color.FromArgb(110, 120, 140);

        private int active, total;

        public StatusPill()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Height = 26;
            Width = 120;
            Cursor = Cursors.Default;
        }

        private string Label => $"{active} / {total} ACTIVE";

        public void SetCounts(int activeCount, int totalCount)
        {
            if (active == activeCount && total == totalCount && Width > 120) return;

            active = activeCount;
            total = totalCount;

            int textW = TextRenderer.MeasureText(Label, UiFonts.SmallBold, new Size(400, 30),
                TextFormatFlags.NoPadding).Width;
            int w = textW + 42;
            if (Width != w) Width = w;
            Invalidate();
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
            if (Width < 30 || Height < 10) return;

            if (Parent != null)
            {
                var state = g.Save();
                g.TranslateTransform(-Left, -Top);
                InvokePaintBackground(Parent, new PaintEventArgs(g, new Rectangle(Left, Top, Width, Height)));
                g.Restore(state);
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool on = active > 0;
            Color accent = on ? Cyan : Muted;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Pill(rect))
            {
                using (var fill = new SolidBrush(Color.FromArgb(on ? 26 : 14, on ? Blue : Color.White)))
                    g.FillPath(fill, path);
                using (var pen = new Pen(Color.FromArgb(on ? 120 : 50, accent)))
                    g.DrawPath(pen, path);
            }

            float cx = 15f, cy = Height / 2f;
            if (on)
            {
                using var halo = new SolidBrush(Color.FromArgb(45, Cyan));
                g.FillEllipse(halo, cx - 6f, cy - 6f, 12f, 12f);
            }
            using (var dot = new SolidBrush(accent))
                g.FillEllipse(dot, cx - 3.5f, cy - 3.5f, 7f, 7f);

            var textRect = new Rectangle(26, 0, Width - 30, Height);
            TextRenderer.DrawText(g, Label, UiFonts.SmallBold, textRect,
                on ? Color.FromArgb(225, 245, 255) : Color.FromArgb(150, 158, 175),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    public partial class MainForm : Form
    {
        private const int TitleBarHeight = 36;

        // window styles and messages
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_SYSMENU = 0x00080000;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_EX_COMPOSITED = 0x02000000;
        private const int WM_NCCALCSIZE = 0x0083;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_RESTORE = 0xF120;
        private const int SC_MINIMIZE = 0xF020;
        private const bool UseCaptionTrick = true;
        private const bool UseComposited = true;
        private const bool UseDwmWindowStyling = true;
        private const bool DisableDwmShowTransition = true;
        private const bool UsePrewarmOverlay = true;
        private const bool PlayCardEntranceOnStartup = false;
        private const bool UseCustomRestore = true;
        private const bool UseCustomMinimize = true;
        private const bool UseOpenFade = true;
        private const bool ShowActionSuccessDialog = false;

        private const int OpenAnimMs = 220;
        private const int CloseAnimMs = 150;
        private const int MinimizeAnimMs = 150;
        private const int OpenSlidePx = 14;
        private const int MinimizeSlidePx = 14;
        private const int CloseSlidePx = 8;
        private static Color PulseBlue => Theme.GlowDeep;
        private static Color PulseCyan => Theme.GlowLight;

        private const string LogoRelativePath = "Assets/pulse_logo.png";
        private const float LogoOpacity = 0.06f;

        [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint ms);

        private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private void SetDwmTransitionsDisabled(bool disabled)
        {
            if (!IsHandleCreated) return;
            try
            {
                int v = disabled ? 1 : 0;
                DwmSetWindowAttribute(Handle, DWMWA_TRANSITIONS_FORCEDISABLED, ref v, sizeof(int));
            }
            catch { /* dwmapi unavailable */ }
        }

        private const double MaxOpacity = 0.999;

        private const double StartOpacity = 0.01;

        private static readonly string[] Categories = { "GENERAL", "PRECISION", "PING", "GPU", "FPS", "CPU" };

        private static readonly Font EmptyFont = new("Segoe UI", 10F, FontStyle.Italic);

        // tweaks that need a reboot to take full effect
        private static readonly HashSet<string> RebootRequiredTweaks = new(StringComparer.OrdinalIgnoreCase)
        {
            "HAGSToggle",
            "GpuPriorityOpts",
            "NetworkThrottling",
            "NetworkResponsiveness",
            "network_nagles",
            "DisableTelemetry",
            "DisableHibernation",
            "DisableCpuMitigations"
        };

        private static readonly HashSet<string> RiskyTweaks = new(StringComparer.OrdinalIgnoreCase)
        {
            "DisableCpuMitigations"
        };

        private readonly AppSettings settings = AppSettings.Load();
        private readonly Dictionary<string, ITweak[]> tweaksByCategory;
        private readonly ITweak[] allTweaks;
        private readonly Tween overlayT;

        private TitleBar? titleBar;
        private GradientPanel? topHeaderPanel;
        private GradientPanel? bottomBar;
        private BufferedFlowPanel? contentPanel;
        private DockBar? tabDock;
        private SettingsOverlay? overlay;
        private StatusPill? statusPill;
        private System.Windows.Forms.Timer? animTimer;
        private Icon? appIcon;
        private Bitmap? bgCache;
        private Bitmap? logoMark;

        private string activeCategory = "GENERAL";
        private bool settingsOpen;
        private bool drawOwnBorder = true;
        private bool closing;
        private bool minimizing;
        private bool closeAnimDone;
        private bool introDone;
        private bool timerPeriodActive;
        private FormWindowState lastState = FormWindowState.Normal;

        private int discordBusy;
        private volatile bool discordDirty;

        public MainForm()
        {
            Theme.Apply(Theme.FindPreset(settings.ThemeName), notify: false);
            Tween.Enabled = settings.Animations;

            tweaksByCategory = new Dictionary<string, ITweak[]>
            {
                ["GENERAL"] = new ITweak[]
                {
                    new DnsCacheFlushTweak(),
                    new TempFileCleanupTweak(),
                    new PrefetchCleanupTweak(),
                    new EventLogClearTweak(),
                    new VisualEffectsTweak(),
                    new EmptyRecycleBinTweak(),
                    new DisableBingSearchTweak(),
                    new BrowserCacheCleanupTweak(),
                    new DisableTelemetryTweak(),
                    new DisableBackgroundAppsTweak(),
                    new DisableTipsAndSuggestionsTweak()
                },
                ["PRECISION"] = new ITweak[]
                {
                    new SystemResponsivenessTweak(),
                    new OverlayWidgetTweak(),
                    new MouseKeyboardOptimizations(),
                    new StorageOptimizationTweak(),
                    new WindowsSettingsTweak(),
                    new GameBarRecordingsTweak(),
                    new SuperFetchTweak(),
                    new WindowsIndexingTweak(),
                    new DisableStartupDelayTweak(),
                    new MenuShowDelayTweak(),
                    new DisableHibernationTweak()
                },
                ["PING"] = new ITweak[]
                {
                    new NetworkTweaks(),
                    new NetworkThrottlingTweak(),
                    new NetworkResponsivenessTweak()
                },
                ["GPU"] = new ITweak[]
                {
                    new HardwareAcceleratedGpuSchedulingTweak(),
                    new GpuPriorityOptimizationTweak()
                },
                ["FPS"] = new ITweak[]
                {
                    new GameModeTweak(),
                    new PowerPlanTweak()
                },
                ["CPU"] = new ITweak[]
                {
                    new CoreParkingDisableTweak(),
                    new PrioritySeparationTweak(),
                    new DisableMitigationsTweak()
                }
            };

            allTweaks = tweaksByCategory.Values.SelectMany(t => t).Distinct().ToArray();

            overlayT = new Tween(0f, 280, PositionOverlay);

            LoadLogoMark();
            InitializeComponentCustom();
            LoadHeaderUI();
            BuildTabDock();
            LoadBackgroundFromSettings();

            Theme.Changed += OnThemeChanged;
            SwitchCategory(activeCategory, animate: false);
        }

        // discord RPC + header pill
        public void UpdateDiscordStatus()
        {
            if (allTweaks == null || allTweaks.Length == 0) return;

            discordDirty = true;
            if (Interlocked.CompareExchange(ref discordBusy, 1, 0) != 0) return;

            var toggles = allTweaks.Where(t => !t.IsAction).ToArray();

            Task.Run(() =>
            {
                try
                {
                    while (discordDirty)
                    {
                        discordDirty = false;

                        int active = toggles.Count(SafeIsApplied);
                        Debug.WriteLine($"[DiscordRPC] Active: {active}/{toggles.Length}");

                        PostStatusPill(active, toggles.Length);
                        DiscordRpc.UpdatePresence("Optimizing Windows...", $"{active}/{toggles.Length} Tweaks Active");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DiscordRPC] Update failed: {ex.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref discordBusy, 0);
                    if (discordDirty) UpdateDiscordStatus();
                }
            });
        }

        private void PostStatusPill(int active, int total)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new Action(() =>
                {
                    if (!IsDisposed && statusPill != null && !statusPill.IsDisposed)
                    {
                        statusPill.SetCounts(active, total);
                        PositionStatusPill();
                    }
                }));
            }
            catch (InvalidOperationException) { /* form is closing */ }
        }

        // form lifecycle

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (DisableDwmShowTransition && !introDone) SetDwmTransitionsDisabled(true);

            if (UseDwmWindowStyling)
            {
                Native.UseDarkTitleBar(Handle);
                drawOwnBorder = !Native.RoundCorners(Handle);
            }
            Invalidate();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            EnsureBackgroundCache();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            Refresh();
            if (UsePrewarmOverlay) PrewarmOverlay();
            ThemedMessageBox.Prewarm();
            UpdateDiscordStatus();

            BeginInvoke(new Action(PlayOpenAnimation));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // only animate a normal user close
            if (e.Cancel || closeAnimDone || e.CloseReason != CloseReason.UserClosing || !settings.Animations)
                return;

            e.Cancel = true;
            if (closing) return;
            closing = true;

            StopAnim();

            if (WindowState == FormWindowState.Minimized)
            {
                closeAnimDone = true;
                BeginInvoke(new Action(Close));
                return;
            }

            int startTop = Top;
            double startOpacity = Opacity;

            RunAnim(CloseAnimMs, t =>
            {
                double eased = t * t; // ease-in
                Opacity = startOpacity * (1.0 - eased);
                if (CloseSlidePx != 0) Top = startTop + (int)(CloseSlidePx * eased);
            }, () =>
            {
                closeAnimDone = true;
                Close();
            });
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopAnim();
            appIcon?.Dispose();
            appIcon = null;
            DisposeBackgroundCache();
            logoMark?.Dispose();
            logoMark = null;

            Theme.Changed -= OnThemeChanged;
            BackgroundManager.Changed -= OnBackgroundChanged;
            if (ReferenceEquals(BackgroundManager.Host, this)) BackgroundManager.Host = null;
            BackgroundManager.Clear(notify: false);

            overlayT.Dispose();
            base.OnFormClosed(e);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;

                if (UseCaptionTrick) cp.Style |= WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX;
                if (UseComposited) cp.ExStyle |= WS_EX_COMPOSITED;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            // Minimize
            if (UseCustomMinimize && introDone && !closing && settings.Animations && m.Msg == WM_SYSCOMMAND &&
                ((int)m.WParam & 0xFFF0) == SC_MINIMIZE)
            {
                if (WindowState != FormWindowState.Minimized && !minimizing)
                    PlayMinimizeAnimation();
                return;
            }

            if (UseCustomRestore && introDone && settings.Animations && m.Msg == WM_SYSCOMMAND &&
                ((int)m.WParam & 0xFFF0) == SC_RESTORE && WindowState == FormWindowState.Minimized)
            {
                StopAnim();
                SetDwmTransitionsDisabled(true);
                Opacity = StartOpacity;
            }

            if (UseCaptionTrick && m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            base.WndProc(ref m);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && settingsOpen)
            {
                ToggleSettings();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var cache = EnsureBackgroundCache();
            if (cache != null)
            {
                e.Graphics.DrawImageUnscaled(cache, 0, 0);
                return;
            }

            GradientBackground.Paint(this, e.Graphics);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!drawOwnBorder) return;

            using var pen = new Pen(Theme.BorderHover);
            e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            if (WindowState == FormWindowState.Minimized)
            {
                lastState = WindowState;
                return;
            }

            bool restoring = lastState == FormWindowState.Minimized;
            lastState = WindowState;

            if (restoring)
            {
                if (UseCustomRestore && settings.Animations)
                {
                    Opacity = StartOpacity;
                    BeginInvoke(new Action(PlayRestoreAnimation));
                    return;
                }

                RepaintAll();
                BeginInvoke(new Action(RepaintAll));
                return;
            }

            if (!settingsOpen) Invalidate();
        }
        public void MinimizeAnimated()
        {
            if (WindowState == FormWindowState.Minimized || minimizing) return;

            if (UseCustomMinimize && introDone && !closing && settings.Animations)
                PlayMinimizeAnimation();
            else
                WindowState = FormWindowState.Minimized;
        }

        private void PlayMinimizeAnimation()
        {
            minimizing = true;

            int startTop = Top;
            double startOpacity = Opacity;

            RunAnim(MinimizeAnimMs, t =>
            {
                double eased = t * t;
                Opacity = Math.Max(StartOpacity, startOpacity * (1.0 - eased));
                if (MinimizeSlidePx != 0) Top = startTop + (int)Math.Round(MinimizeSlidePx * eased);
            }, () =>
            {
                Opacity = StartOpacity;
                Top = startTop;
                SetDwmTransitionsDisabled(true);
                minimizing = false;
                WindowState = FormWindowState.Minimized;
            });
        }

        private void PlayRestoreAnimation()
        {
            if (IsDisposed || WindowState == FormWindowState.Minimized) return;

            int targetTop = Top;
            if (OpenSlidePx != 0) Top = targetTop + OpenSlidePx;

            RepaintAll();

            RunAnim(OpenAnimMs, t =>
            {
                double eased = 1.0 - Math.Pow(1.0 - t, 3);
                Opacity = StartOpacity + (MaxOpacity - StartOpacity) * eased;
                if (OpenSlidePx != 0)
                    Top = targetTop + (int)Math.Round(OpenSlidePx * (1.0 - eased));
            }, () =>
            {
                Opacity = MaxOpacity;
                Top = targetTop;
                if (DisableDwmShowTransition) SetDwmTransitionsDisabled(false);
            });
        }

        private void RepaintAll()
        {
            if (IsDisposed || WindowState == FormWindowState.Minimized) return;

            PositionDock();
            PositionOverlay();
            PositionStatusPill();
            LayoutCards();
            Invalidate(true);
            Update();
        }

        // cached window background

        private Bitmap? EnsureBackgroundCache()
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w <= 0 || h <= 0) return null;

            if (bgCache != null && bgCache.Width == w && bgCache.Height == h)
                return bgCache;

            DisposeBackgroundCache();

            try
            {
                var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    GradientBackground.Paint(this, g);
                    DrawLogoWatermark(g, w, h);
                }

                bgCache = bmp;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Render] Background cache failed: {ex.Message}");
                bgCache = null;
            }

            return bgCache;
        }

        private void DisposeBackgroundCache()
        {
            bgCache?.Dispose();
            bgCache = null;
        }

        // logo  watermark

        private void LoadLogoMark()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, LogoRelativePath);
                if (!File.Exists(path)) return;

                using var fs = File.OpenRead(path);
                using var img = Image.FromStream(fs);
                logoMark = new Bitmap(img);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Logo] Could not load watermark: {ex.Message}");
                logoMark = null;
            }
        }

        private void DrawLogoWatermark(Graphics g, int w, int h)
        {
            if (logoMark == null) return;

            int size = (int)(Math.Min(w, h) * 0.62f);
            var dest = new Rectangle(w - size + 30, h - size + 10, size, size);

            var matrix = new ColorMatrix { Matrix33 = LogoOpacity };
            using var attrs = new ImageAttributes();
            attrs.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(logoMark, dest, 0, 0, logoMark.Width, logoMark.Height, GraphicsUnit.Pixel, attrs);
        }

        // window animation

        private void PlayOpenAnimation()
        {
            if (IsDisposed) return;

            if (!settings.Animations)
            {
                Opacity = MaxOpacity;
                FinishIntro();
                return;
            }

            int targetTop = Top;
            if (OpenSlidePx != 0) Top = targetTop + OpenSlidePx;

            RunAnim(OpenAnimMs, t =>
            {
                double eased = 1.0 - Math.Pow(1.0 - t, 3);
                Opacity = StartOpacity + (MaxOpacity - StartOpacity) * eased;
                if (OpenSlidePx != 0)
                    Top = targetTop + (int)Math.Round(OpenSlidePx * (1.0 - eased));
            }, () =>
            {
                Opacity = MaxOpacity;
                Top = targetTop;
                FinishIntro();
            });
        }

        private void FinishIntro()
        {
            if (introDone) return;
            introDone = true;
            if (DisableDwmShowTransition) SetDwmTransitionsDisabled(false);

            if (PlayCardEntranceOnStartup) PlayCardEntrances();
        }

        private void PlayCardEntrances()
        {
            if (contentPanel == null || contentPanel.IsDisposed) return;

            foreach (var card in contentPanel.Controls.OfType<CustomTweakCard>())
                card.PlayEntrance(0);
        }

        private void RunAnim(int durationMs, Action<double> step, Action done)
        {
            StopAnim();

            if (!timerPeriodActive)
            {
                try { timeBeginPeriod(1); timerPeriodActive = true; }
                catch { /* winmm unavailable: use default resolution */ }
            }

            var sw = Stopwatch.StartNew();
            var timer = new System.Windows.Forms.Timer { Interval = 10 };
            animTimer = timer;

            timer.Tick += (s, e) =>
            {
                if (IsDisposed) { StopAnim(); return; }

                double t = Math.Min(1.0, sw.ElapsedMilliseconds / (double)durationMs);
                step(t);

                if (t >= 1.0)
                {
                    StopAnim();
                    done();
                }
            };

            timer.Start();
        }

        private void StopAnim()
        {
            if (animTimer != null)
            {
                animTimer.Stop();
                animTimer.Dispose();
                animTimer = null;
            }

            if (timerPeriodActive)
            {
                try { timeEndPeriod(1); } catch { }
                timerPeriodActive = false;
            }
        }

        // icon

        private void LoadExecutableIcon()
        {
            try
            {
                string? exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return;

                appIcon = Icon.ExtractAssociatedIcon(exePath);
                if (appIcon != null) Icon = appIcon;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Icon] Failed to load embedded icon: {ex.Message}");
            }
        }

        // UI construction

        private void InitializeComponentCustom()
        {
            Text = "Pulse";
            ShowIcon = true;
            ShowInTaskbar = true;
            LoadExecutableIcon();

            if (UseOpenFade) Opacity = StartOpacity;

            FormBorderStyle = FormBorderStyle.None;
            Size = new Size(1100, 700);
            MinimumSize = Size;
            MaximumSize = Size;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.WindowBg;
            Padding = new Padding(1);
            DoubleBuffered = true;

            topHeaderPanel = new GradientPanel { Dock = DockStyle.Top, Height = 64 };
            topHeaderPanel.Paint += PaintHeaderAccent;
            topHeaderPanel.Resize += (s, e) => PositionStatusPill();

            bottomBar = new GradientPanel { Dock = DockStyle.Bottom, Height = 84 };
            bottomBar.Resize += (s, e) => PositionDock();

            contentPanel = new BufferedFlowPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false,
                BackColor = Theme.WindowBg,
                Padding = new Padding(25, 8, 25, 10)
            };
            contentPanel.Resize += (s, e) => LayoutCards();

            titleBar = new TitleBar { Title = Text };
            titleBar.SettingsClicked += (s, e) => ToggleSettings();
            titleBar.HelpClicked += (s, e) => ShowFaqDialog();

            Controls.Add(contentPanel);
            Controls.Add(bottomBar);
            Controls.Add(topHeaderPanel);
            Controls.Add(titleBar);
        }
        private void PaintHeaderAccent(object? sender, PaintEventArgs e)
        {
            if (sender is not Control c || c.Width < 40 || c.Height < 4) return;

            var rect = new Rectangle(0, c.Height - 1, c.Width, 1);
            using var br = new LinearGradientBrush(rect, PulseBlue, PulseCyan, 0f);
            br.InterpolationColors = new ColorBlend
            {
                Positions = new[] { 0f, 0.12f, 0.5f, 0.88f, 1f },
                Colors = new[]
                {
                    Color.FromArgb(0, PulseBlue),
                    Color.FromArgb(90, PulseBlue),
                    Color.FromArgb(150, PulseCyan),
                    Color.FromArgb(90, PulseBlue),
                    Color.FromArgb(0, PulseBlue)
                }
            };
            e.Graphics.FillRectangle(br, rect);
        }

        private void LoadHeaderUI()
        {
            if (topHeaderPanel == null) return;

            topHeaderPanel.Controls.Add(new SheenLabel
            {
                Text = "T W E A K S",
                Font = UiFonts.Big,
                Location = new Point(22, 18)
            });

            statusPill = new StatusPill();
            statusPill.SetCounts(0, allTweaks.Count(t => !t.IsAction));
            topHeaderPanel.Controls.Add(statusPill);
            statusPill.Resize += (s, e) => PositionStatusPill();
            PositionStatusPill();
        }

        private void PositionStatusPill()
        {
            if (topHeaderPanel == null || statusPill == null) return;
            if (topHeaderPanel.ClientSize.Width < 200) return;

            var p = new Point(
                topHeaderPanel.ClientSize.Width - statusPill.Width - 25,
                (topHeaderPanel.ClientSize.Height - statusPill.Height) / 2 + 4);

            if (statusPill.Location != p) statusPill.Location = p;
        }

        private void BuildTabDock()
        {
            if (bottomBar == null) return;

            const int tabW = 92, tabH = 32, gap = 6, pad = 10;

            tabDock = new DockBar
            {
                Size = new Size(pad * 2 + Categories.Length * tabW + (Categories.Length - 1) * gap, tabH + 16)
            };

            int x = pad;
            foreach (var category in Categories)
            {
                var tab = new PillButton
                {
                    Text = category,
                    Font = UiFonts.SmallBold,
                    Size = new Size(tabW, tabH),
                    Location = new Point(x, 8)
                };
                tab.ApplyTabStyle(false, animate: false);
                tab.Click += (s, e) => SwitchCategory(category);
                tabDock.Controls.Add(tab);
                x += tabW + gap;
            }

            bottomBar.Controls.Add(tabDock);
            PositionDock();
        }

        private void PositionDock()
        {
            if (bottomBar == null || tabDock == null) return;
            if (bottomBar.ClientSize.Width < 300) return;

            var p = new Point(
                (bottomBar.ClientSize.Width - tabDock.Width) / 2,
                (bottomBar.ClientSize.Height - tabDock.Height) / 2);

            if (tabDock.Location != p) tabDock.Location = p;
        }

        // categories and cards

        private void SwitchCategory(string category, bool animate = true)
        {
            if (animate && category == activeCategory && contentPanel != null && contentPanel.Controls.Count > 0)
                return;

            activeCategory = category;

            if (tabDock != null)
            {
                foreach (Control c in tabDock.Controls)
                    if (c is PillButton tab) tab.ApplyTabStyle(tab.Text == category, animate);
            }

            if (contentPanel == null) return;

            contentPanel.SuspendLayout();

            while (contentPanel.Controls.Count > 0)
                contentPanel.Controls[0].Dispose();

            var newCards = new List<CustomTweakCard>();

            if (tweaksByCategory.TryGetValue(category, out var tweaks) && tweaks.Length > 0)
            {
                for (int i = 0; i < tweaks.Length; i++)
                {
                    var card = CreateCard(tweaks[i], i + 1);
                    contentPanel.Controls.Add(card);
                    newCards.Add(card);
                }
            }
            else
            {
                contentPanel.Controls.Add(new Label
                {
                    Text = $"No tweaks registered under '{category}' yet.",
                    ForeColor = Theme.TextSecondary,
                    BackColor = Color.Transparent,
                    Font = EmptyFont,
                    AutoSize = true,
                    Margin = new Padding(10, 30, 0, 0),
                    UseCompatibleTextRendering = true
                });
            }

            contentPanel.ResumeLayout();
            LayoutCards();

            if (introDone)
            {
                foreach (var card in newCards) card.PlayEntrance(0);
            }
            else if (!PlayCardEntranceOnStartup)
            {
                foreach (var card in newCards) card.ShowInstantly();
            }
        }

        private CustomTweakCard CreateCard(ITweak tweak, int number)
        {
            var card = new CustomTweakCard { CardNumber = number.ToString("00") };
            card.BindTweak(tweak, card.CardNumber);
            card.IsApplied = SafeIsApplied(tweak);
            card.IsRisky = RiskyTweaks.Contains(tweak.Id);

            card.TweakClicked += (s, e) =>
            {
                try
                {
                    if (tweak.IsAction)
                    {
                        RunActionAsync(card, tweak);
                        return;
                    }

                    bool isCurrentlyApplied = tweak.IsApplied();

                    // Confirm before turning ON a risky tweak
                    if (!isCurrentlyApplied && RiskyTweaks.Contains(tweak.Id))
                    {
                        var confirm = ThemedMessageBox.Show(this,
                            $"⚠️ WARNING: '{tweak.Title}' is an advanced tweak that modifies security or low-level kernel configurations.\n\n" +
                            $"Enabling this can expose your system to theoretical side-channel vulnerabilities or stability side-effects in exchange for raw benchmark gains.\n\n" +
                            $"Do you wish to proceed?",
                            "Security Warning",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                        if (confirm != DialogResult.Yes)
                        {
                            card.IsApplied = SafeIsApplied(tweak);
                            return;
                        }
                    }

                    if (isCurrentlyApplied)
                    {
                        tweak.Revert();
                    }
                    else
                    {
                        tweak.Apply();

                        if (RebootRequiredTweaks.Contains(tweak.Id))
                            PromptRestart($"{tweak.Title} has been applied successfully.\n\n" +
                                          "This optimization requires a system restart to take full effect. " +
                                          "Would you like to restart your PC now?");
                    }

                    card.IsApplied = SafeIsApplied(tweak);
                    UpdateDiscordStatus();
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                    if (!card.IsDisposed && !tweak.IsAction) card.IsApplied = SafeIsApplied(tweak);
                    UpdateDiscordStatus();
                }
            };

            return card;
        }
        private async void RunActionAsync(CustomTweakCard card, ITweak tweak)
        {
            if (card.IsDisposed || card.State == ActionState.Running) return;

            card.SetActionState(ActionState.Running);

            try
            {
                await Task.Run(() => tweak.Apply());

                if (card.IsDisposed) return;
                card.SetActionState(ActionState.Success);

                if (ShowActionSuccessDialog && !IsDisposed)
                    ThemedMessageBox.Show(this, $"{tweak.Title} completed successfully!", "Action Executed",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (!card.IsDisposed) card.SetActionState(ActionState.Failed);
                if (!IsDisposed) ShowError(ex);
            }
        }

        private void LayoutCards()
        {
            if (contentPanel == null) return;

            int avail = contentPanel.ClientSize.Width - contentPanel.Padding.Horizontal;
            if (avail < 100) return;

            const int gap = 15;
            const int minCardW = 240;

            int cols = Math.Max(1, avail / (minCardW + gap));
            int cardW = avail / cols - gap;

            var cards = contentPanel.Controls.OfType<CustomTweakCard>().ToList();
            if (cards.Count == 0) return;

            int cardH = Math.Max(100, cards.Max(c => c.GetPreferredHeight(cardW)));

            contentPanel.SuspendLayout();
            foreach (var card in cards)
            {
                card.Size = new Size(cardW, cardH);
                card.Margin = new Padding(0, 0, gap, gap);
            }
            contentPanel.ResumeLayout();
        }

        private void RefreshCards()
        {
            if (contentPanel == null) return;

            foreach (var card in contentPanel.Controls.OfType<CustomTweakCard>())
                if (card.AssociatedTweak != null && !card.AssociatedTweak.IsAction)
                    card.IsApplied = SafeIsApplied(card.AssociatedTweak);

            UpdateDiscordStatus();
        }

        private static bool SafeIsApplied(ITweak tweak)
        {
            try { return tweak.IsApplied(); }
            catch { return false; }
        }

        // settings overlay

        private SettingsOverlay EnsureOverlay()
        {
            if (overlay != null) return overlay;

            overlay = new SettingsOverlay(settings) { Visible = false };
            overlay.OptimiseAllRequested += (s, e) => OptimiseAll();
            overlay.UndoAllRequested += (s, e) => UndoAll();
            overlay.CloseRequested += (s, e) => { if (settingsOpen) ToggleSettings(); };

            Controls.Add(overlay);
            overlay.BringToFront();
            PositionOverlay();
            return overlay;
        }

        private void PrewarmOverlay()
        {
            if (IsDisposed || settingsOpen) return;

            var ov = EnsureOverlay();
            ov.Visible = true;
            ov.Visible = false;
        }

        private void ToggleSettings()
        {
            settingsOpen = !settingsOpen;

            if (settingsOpen)
                EnsureOverlay().BringToFront();

            overlayT.Go(settingsOpen ? 1f : 0f);
        }

        private void PositionOverlay()
        {
            if (overlay == null) return;

            float p = overlayT.Value;
            int top = TitleBarHeight + 1;
            int height = Math.Max(100, ClientSize.Height - top - 1);

            var bounds = new Rectangle(1, top + (int)((1f - p) * height), Math.Max(100, ClientSize.Width - 2), height);

            bool visible = p > 0.001f;
            if (overlay.Visible != visible) overlay.Visible = visible;
            if (!visible) return;

            if (overlay.Bounds != bounds)
            {
                overlay.Bounds = bounds;
                overlay.Invalidate(true);
            }
        }

        // faq

        private void ShowFaqDialog()
        {
            ThemedMessageBox.Show(this,
                "📖 PULSE OPTIMIZER - FAQ and SAFETY\n\n" +

                "• What do tweaks do?\n" +
                "They adjust Windows registry values, power plans, and services to try to improve gaming latency. Results vary by system.\n\n" +

                "• Can I undo them?\n" +
                "Each tweak has a Revert option, and Pulse restores users saved or original Window's default registry values after revert. This is a portfolio project and may still have bugs, so revert isn't guaranteed to work in every case.\n\n" +

                "• Risky tweaks\n" +
                "Some, like CPU mitigations, trade security for speed. You'll get a warning first.\n\n" +

                "• Stay safe\n" +
                "Create a System Restore point before applying tweaks, and apply them a few at a time.\n\n" +

                "Use at your own risk. Provided as-is, with no warranty.",
                "Pulse FAQ - Safety Guide",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // background and theme

        private void LoadBackgroundFromSettings()
        {
            BackgroundManager.Host = this;
            BackgroundManager.Dim = settings.DimPercent / 100f;
            BackgroundManager.Changed += OnBackgroundChanged;

            if (!string.IsNullOrWhiteSpace(settings.BackgroundPath) && File.Exists(settings.BackgroundPath))
                BackgroundManager.Load(settings.BackgroundPath, out _);
        }

        private void OnBackgroundChanged(object? sender, EventArgs e)
        {
            DisposeBackgroundCache();

            if (WindowState != FormWindowState.Minimized) Invalidate(true);
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            if (tabDock != null)
            {
                foreach (Control c in tabDock.Controls)
                    if (c is PillButton tab) tab.ApplyTabStyle(tab.Text == activeCategory);
            }

            BackColor = Theme.WindowBg;
            DisposeBackgroundCache();

            statusPill?.Invalidate();
            topHeaderPanel?.Invalidate();

            if (WindowState != FormWindowState.Minimized) Invalidate(true);
        }

        // dialogs

        private bool Confirm(string text, string title, MessageBoxIcon icon) =>
            !settings.ConfirmActions ||
            ThemedMessageBox.Show(this, text, title, MessageBoxButtons.YesNo, icon) == DialogResult.Yes;

        private void ShowError(Exception ex) =>
            ThemedMessageBox.Show(this, ex.Message, "Registry error", MessageBoxButtons.OK, MessageBoxIcon.Error);

        private void PromptRestart(string message)
        {
            var result = ThemedMessageBox.Show(this, message, "Restart Required",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            try { Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { UseShellExecute = false, CreateNoWindow = true }); }
            catch (Exception ex) { ShowError(ex); }
        }

        private void OptimiseAll()
        {
            if (!Confirm("Apply all optimizations across all categories excluding risky tweaks?", "Optimise everything", MessageBoxIcon.Question))
                return;

            var errors = new List<string>();
            bool needsReboot = false;

            UseWaitCursor = true;
            try
            {
                foreach (var tweak in allTweaks)
                {
                    try
                    {
                        // skip actions
                        if (tweak.IsAction || tweak.IsApplied() || RiskyTweaks.Contains(tweak.Id)) continue;

                        tweak.Apply();
                        if (RebootRequiredTweaks.Contains(tweak.Id)) needsReboot = true;
                    }
                    catch (Exception ex) { errors.Add($"{tweak.Title}: {ex.Message}"); }
                }
            }
            finally { UseWaitCursor = false; }

            RefreshCards();
            ReportBulkResult(errors, needsReboot);
        }

        private void UndoAll()
        {
            if (!Confirm("Undo all changes and revert back to standard Windows defaults or users saved original values?", "Undo all", MessageBoxIcon.Warning))
                return;

            var errors = new List<string>();

            UseWaitCursor = true;
            try
            {
                foreach (var tweak in allTweaks)
                {
                    try { if (!tweak.IsAction && tweak.IsApplied()) tweak.Revert(); }
                    catch (Exception ex) { errors.Add($"{tweak.Title}: {ex.Message}"); }
                }
            }
            finally { UseWaitCursor = false; }

            RefreshCards();
            ReportBulkResult(errors, needsReboot: false);
        }

        private void ReportBulkResult(List<string> errors, bool needsReboot)
        {
            if (errors.Count > 0)
            {
                string shown = string.Join("\n", errors.Take(8));
                if (errors.Count > 8) shown += $"\n...and {errors.Count - 8} more";

                ThemedMessageBox.Show(this, $"{errors.Count} tweak(s) failed:\n\n{shown}", "Some tweaks failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (needsReboot)
                PromptRestart("Some of the applied optimizations need a restart to take full effect.\n\nRestart your PC now?");
        }
    }
}