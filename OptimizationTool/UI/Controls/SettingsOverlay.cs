using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using OptimizationTool.Services;

namespace OptimizationTool.UI.Controls
{
    public class SettingsOverlay : GradientPanel
    {
        private const int SidebarW = 232;
        private const int ContentTop = 76;

        private static readonly string[] NavNames = { "APPEARANCE", "GENERAL", "ACTIONS", "ABOUT" };
        private static readonly string[] SectionTitles = { "Appearance", "General", "Actions", "About" };

        public event EventHandler? OptimiseAllRequested;
        public event EventHandler? UndoAllRequested;
        public event EventHandler? CloseRequested;

        private readonly AppSettings settings;
        private readonly List<PillButton> navButtons = new();
        private readonly List<PillButton> secondaryButtons = new();
        private readonly List<TransparentPanel> sections = new();
        private readonly PillButton backButton;
        private int active;

        private ThemePicker picker = null!;
        private SettingRow bgRow = null!;
        private SettingRow dimRow = null!;
        private SliderBar dimSlider = null!;
        private ToggleSwitch animToggle = null!;
        private ToggleSwitch confirmToggle = null!;
        private ToggleSwitch discordRpcToggle = null!;

        public SettingsOverlay(AppSettings settings)
        {
            this.settings = settings;

            for (int i = 0; i < NavNames.Length; i++)
            {
                int index = i;
                var b = new PillButton
                {
                    Text = NavNames[i],
                    Font = UiFonts.SmallBold,
                    Size = new Size(188, 38),
                    Location = new Point(24, 78 + i * 46)
                };
                b.ApplyTabStyle(i == active, animate: false);
                b.Click += (s, e) => SelectSection(index);
                navButtons.Add(b);
                Controls.Add(b);
            }

            backButton = PrimaryButton("BACK", 188);
            backButton.Height = 40;
            backButton.Click += (s, e) => CloseRequested?.Invoke(this, EventArgs.Empty);
            Controls.Add(backButton);

            sections.Add(BuildAppearance());
            sections.Add(BuildGeneral());
            sections.Add(BuildActions());
            sections.Add(BuildAbout());

            for (int i = 0; i < sections.Count; i++)
            {
                sections[i].Visible = i == active;
                Controls.Add(sections[i]);
            }

            Theme.Changed += OnThemeChanged;
        }

        // sections

        private TransparentPanel BuildAppearance()
        {
            var section = new TransparentPanel();

            picker = new ThemePicker();
            picker.PresetChosen += (s, preset) =>
            {
                Theme.Apply(preset);
                settings.ThemeName = preset.Name;
                settings.Save();
            };

            var choose = SecondaryButton("CHOOSE IMAGE", 130);
            var reset = SecondaryButton("RESET", 84);
            choose.Location = new Point(0, 0);
            reset.Location = new Point(140, 0);
            choose.Click += (s, e) => ChooseImage();
            reset.Click += (s, e) => ResetBackground();

            var buttons = new TransparentPanel { Size = new Size(224, 34) };
            buttons.Controls.Add(choose);
            buttons.Controls.Add(reset);
            bgRow = new SettingRow("Background", "", buttons);

            dimSlider = new SliderBar { Width = 220, Maximum = 95 };
            dimSlider.Value = Math.Max(0, Math.Min(95, settings.DimPercent));
            dimRow = new SettingRow("Background dim", "", dimSlider);
            dimSlider.ValueChanged += (s, e) =>
            {
                BackgroundManager.Dim = dimSlider.Value / 100f;
                UpdateDimText();
            };
            dimSlider.ValueCommitted += (s, e) =>
            {
                settings.DimPercent = dimSlider.Value;
                settings.Save();
            };

            UpdateBackgroundText();
            UpdateDimText();

            Stack(section, picker, bgRow, dimRow);
            return section;
        }

        private TransparentPanel BuildGeneral()
        {
            var section = new TransparentPanel();

            animToggle = new ToggleSwitch();
            animToggle.SetCheckedSilently(settings.Animations);
            animToggle.CheckedChanged += (s, e) =>
            {
                settings.Animations = animToggle.Checked;
                Tween.Enabled = animToggle.Checked;
                settings.Save();
            };

            confirmToggle = new ToggleSwitch();
            confirmToggle.SetCheckedSilently(settings.ConfirmActions);
            confirmToggle.CheckedChanged += (s, e) =>
            {
                settings.ConfirmActions = confirmToggle.Checked;
                settings.Save();
            };

            discordRpcToggle = new ToggleSwitch();
            discordRpcToggle.SetCheckedSilently(settings.DiscordRpcEnabled);
            discordRpcToggle.CheckedChanged += (s, e) =>
            {
                settings.DiscordRpcEnabled = discordRpcToggle.Checked;
                settings.Save();

                if (discordRpcToggle.Checked)
                {
                    DiscordRpc.Initialize();
                }
                else
                {
                    DiscordRpc.Shutdown();
                }
            };

            Stack(section,
                new SettingRow("Animations", "Smooth fades and slides. Turn off to save a little CPU.", animToggle),
                new SettingRow("Confirm bulk actions", "Ask before Optimise everything and Undo all.", confirmToggle),
                new SettingRow("Discord Rich Presence", "Show your optimization status on your Discord profile.", discordRpcToggle));
            return section;
        }

        private TransparentPanel BuildActions()
        {
            var section = new TransparentPanel();

            var apply = PrimaryButton("APPLY ALL", 124);
            apply.Click += (s, e) => OptimiseAllRequested?.Invoke(this, EventArgs.Empty);

            var undo = SecondaryButton("UNDO ALL", 124);
            undo.Click += (s, e) => UndoAllRequested?.Invoke(this, EventArgs.Empty);

            Stack(section,
                new SettingRow("Optimise everything", "Apply every tweak in every category.", apply),
                new SettingRow("Undo all", "Revert every tweak back to the Windows defaults.", undo));
            return section;
        }

        private TransparentPanel BuildAbout()
        {
            var section = new TransparentPanel();

            var open = SecondaryButton("OPEN FOLDER", 124);
            open.Click += (s, e) => OpenSettingsFolder();

            var resetAll = SecondaryButton("RESET", 124);
            resetAll.Click += (s, e) => ResetAll();

            Stack(section,
                new SettingRow("winoptimizer - Studio Edition", "Settings are stored in " + AppSettings.FolderPath, open),
                new SettingRow("Reset settings", "Restores the theme, background and options to their defaults.", resetAll));
            return section;
        }

        // helpers

        private PillButton SecondaryButton(string text, int width)
        {
            var b = new PillButton { Text = text, Font = UiFonts.SmallBold, Size = new Size(width, 34) };
            b.ApplyTabStyle(false, animate: false);
            secondaryButtons.Add(b);
            return b;
        }

        private static PillButton PrimaryButton(string text, int width) => new PillButton
        {
            Text = text,
            Font = UiFonts.SmallBold,
            Size = new Size(width, 34),
            BackColor = Color.White,
            ForeColor = Color.Black,
            BorderCol = Color.White
        };

        private static void Stack(TransparentPanel section, params Control[] rows)
        {
            foreach (var r in rows) section.Controls.Add(r);

            void Relayout()
            {
                int y = 0;
                int w = section.ClientSize.Width;
                foreach (var r in rows)
                {
                    r.SetBounds(0, y, w, r.Height);
                    y += r.Height + 12;
                }
            }

            section.Resize += (s, e) => Relayout();
            Relayout();
        }

        private void SelectSection(int index)
        {
            if (index == active) return;
            active = index;

            for (int i = 0; i < navButtons.Count; i++)
                navButtons[i].ApplyTabStyle(i == active);

            for (int i = 0; i < sections.Count; i++)
                sections[i].Visible = i == active;

            Invalidate();
        }

        private void UpdateBackgroundText()
        {
            bgRow.Description = BackgroundManager.HasImage && BackgroundManager.CurrentPath != null
                ? Path.GetFileName(BackgroundManager.CurrentPath)
                : "Default background. Choose a png, jpg, gif or bmp.";
        }

        private void UpdateDimText() => dimRow.Description = $"How dark the image is: {dimSlider.Value}%";

        private void ChooseImage()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Choose a background",
                Filter = "Images (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp",
                CheckFileExists = true
            };
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

            if (BackgroundManager.Load(dlg.FileName, out string error))
            {
                settings.BackgroundPath = dlg.FileName;
                settings.Save();
                UpdateBackgroundText();

                FindForm()?.Invalidate(true);
            }
            else
            {
                ThemedMessageBox.Show(FindForm(), error, "Background", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ResetBackground()
        {
            BackgroundManager.Clear();
            settings.BackgroundPath = null;
            settings.Save();
            UpdateBackgroundText();

            FindForm()?.Invalidate(true);
        }

        private static void OpenSettingsFolder()
        {
            try
            {
                Directory.CreateDirectory(AppSettings.FolderPath);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppSettings.FolderPath}\"") { UseShellExecute = true });
            }
            catch { /* nothing useful to do if Explorer won't open */ }
        }

        private void ResetAll()

        {
            var result = ThemedMessageBox.Show(FindForm(), "Reset the theme, background and options to their defaults?",
                "Reset settings", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;

            settings.ThemeName = "Mono";
            settings.BackgroundPath = null;
            settings.DimPercent = 55;
            settings.Animations = true;
            settings.ConfirmActions = true;
            settings.DiscordRpcEnabled = true;
            settings.Save();

            Tween.Enabled = true;
            BackgroundManager.Clear();
            BackgroundManager.Dim = 0.55f;
            Theme.Apply(Theme.Presets[0]);

            dimSlider.Value = 55;
            animToggle.SetCheckedSilently(true);
            confirmToggle.SetCheckedSilently(true);
            discordRpcToggle.SetCheckedSilently(true);
            UpdateBackgroundText();
            UpdateDimText();

            DiscordRpc.Initialize();

            FindForm()?.Invalidate(true);
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            foreach (var b in secondaryButtons) b.ApplyTabStyle(false, animate: false);
            for (int i = 0; i < navButtons.Count; i++) navButtons[i].ApplyTabStyle(i == active);
            Invalidate(true);
        }

        // layout and painting

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            backButton.Location = new Point(24, Math.Max(0, Height - 24 - backButton.Height));

            int x = SidebarW + 24;
            int w = Math.Max(100, Width - x - 28);
            int h = Math.Max(100, Height - ContentTop - 24);
            foreach (var s in sections) s.SetBounds(x, ContentTop, w, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;

            TextRenderer.DrawText(g, "SETTINGS", UiFonts.Heading, new Point(28, 26), Theme.TextTitle,
                TextFormatFlags.NoPadding);

            TextRenderer.DrawText(g, SectionTitles[active], UiFonts.Big, new Point(SidebarW + 24, 22), Theme.TextTitle,
                TextFormatFlags.NoPadding);

            using var pen = new Pen(Theme.Border);
            g.DrawLine(pen, SidebarW, 20, SidebarW, Math.Max(21, Height - 20));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Theme.Changed -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }
}