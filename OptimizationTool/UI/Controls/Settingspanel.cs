using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using OptimizationTool.UI.Controls;

namespace OptimizationTool.UI
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private float dimPercent;
        private string? backgroundPath;
        private bool isBackgroundLoading;

        public event PropertyChangedEventHandler? PropertyChanged;

        public float DimPercent
        {
            get => dimPercent;
            set
            {
                if (Math.Abs(dimPercent - value) > 0.01f)
                {
                    dimPercent = value;
                    OnPropertyChanged(nameof(DimPercent));
                    BackgroundManager.Dim = dimPercent / 100f;
                    Save();
                }
            }
        }

        public string? BackgroundPath
        {
            get => backgroundPath;
            set
            {
                if (backgroundPath != value)
                {
                    backgroundPath = value;
                    OnPropertyChanged(nameof(BackgroundPath));
                    OnPropertyChanged(nameof(HasBackground));
                }
            }
        }

        public bool HasBackground => !string.IsNullOrEmpty(BackgroundPath);

        public bool IsBackgroundLoading
        {
            get => isBackgroundLoading;
            set
            {
                if (isBackgroundLoading != value)
                {
                    isBackgroundLoading = value;
                    OnPropertyChanged(nameof(IsBackgroundLoading));
                }
            }
        }

        public event EventHandler? OptimiseAllClicked;
        public event EventHandler? UndoAllClicked;
        public event EventHandler<string>? BackgroundLoadError;

        public SettingsViewModel(AppSettings settings)
        {
            dimPercent = settings.DimPercent;
            backgroundPath = settings.BackgroundPath;
        }

        public void OnOptimiseAllClick() => OptimiseAllClicked?.Invoke(this, EventArgs.Empty);
        public void OnUndoAllClick() => UndoAllClicked?.Invoke(this, EventArgs.Empty);

        public void LoadBackground(string filePath)
        {
            IsBackgroundLoading = true;
            try
            {
                if (BackgroundManager.Load(filePath, out var error))
                {
                    BackgroundPath = filePath;
                    Save();
                }
                else
                {
                    BackgroundLoadError?.Invoke(this, error);
                }
            }
            finally
            {
                IsBackgroundLoading = false;
            }
        }

        public void ClearBackground()
        {
            BackgroundManager.Clear(notify: true);
            BackgroundPath = null;
            Save();
        }

        private void Save()
        {
            try
            {
                var settings = new AppSettings
                {
                    BackgroundPath = BackgroundPath,
                    DimPercent = (int)DimPercent
                };
                settings.Save();
            }
            catch { }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SettingsPanel_Refactored : Panel
    {
        private readonly SettingsViewModel viewModel;

        private Label? titleLabel;
        private Label? dimLabel;
        private TrackBar? dimSlider;
        private Label? dimValueLabel;
        private Button? backgroundBtn;
        private Button? clearBgBtn;
        private Label? bgStatusLabel;
        private Button? optimiseBtn;
        private Button? undoBtn;
        private ProgressBar? loadingBar;

        public event EventHandler? OptimiseAllClicked;
        public event EventHandler? UndoAllClicked;

        public SettingsPanel_Refactored(AppSettings settings)
        {
            viewModel = new SettingsViewModel(settings);
            InitializeUI();
            BindViewModel();
        }

        private void InitializeUI()
        {
            BackColor = Theme.Card;
            BorderStyle = BorderStyle.FixedSingle;
            Padding = new Padding(20);

            titleLabel = new Label
            {
                Text = "SETTINGS & APPEARANCE",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(20, 20)
            };
            Controls.Add(titleLabel);

            int y = 65;

            dimLabel = new Label
            {
                Text = "Background Dim Intensity",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Location = new Point(20, y)
            };
            Controls.Add(dimLabel);
            y += 24;

            dimSlider = new TrackBar
            {
                Minimum = 0,
                Maximum = 95,
                Value = (int)viewModel.DimPercent,
                TickStyle = TickStyle.None,
                Height = 30,
                Width = 340,
                Location = new Point(18, y)
            };
            dimSlider.ValueChanged += (s, e) => viewModel.DimPercent = dimSlider.Value;
            Controls.Add(dimSlider);

            dimValueLabel = new Label
            {
                Text = $"{viewModel.DimPercent:F0}%",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(370, y + 2)
            };
            Controls.Add(dimValueLabel);
            y += 45;

            bgStatusLabel = new Label
            {
                Text = viewModel.HasBackground ? Path.GetFileName(viewModel.BackgroundPath!) : "No custom background loaded",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Location = new Point(20, y)
            };
            Controls.Add(bgStatusLabel);
            y += 28;

            backgroundBtn = CreateButton("Choose Background Image", y);
            backgroundBtn.Click += (s, e) => ChooseBackground();
            y += 42;

            clearBgBtn = CreateButton("Clear Background", y);
            clearBgBtn.Click += (s, e) => viewModel.ClearBackground();
            y += 42;

            loadingBar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                Height = 3,
                Location = new Point(20, y),
                Width = 400,
                Visible = false
            };
            Controls.Add(loadingBar);
            y += 20;

            optimiseBtn = CreateButton("Optimise All Categories", y);
            optimiseBtn.BackColor = Color.FromArgb(30, 110, 50);
            optimiseBtn.Click += (s, e) => viewModel.OnOptimiseAllClick();
            y += 42;

            undoBtn = CreateButton("Undo All Changes", y);
            undoBtn.BackColor = Color.FromArgb(140, 40, 40);
            undoBtn.Click += (s, e) => viewModel.OnUndoAllClick();
        }

        private Button CreateButton(string text, int y)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 48),
                FlatStyle = FlatStyle.Flat,
                Height = 34,
                Location = new Point(20, y),
                Width = 400,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Theme.Border;
            btn.FlatAppearance.BorderSize = 1;
            Controls.Add(btn);
            return btn;
        }

        private void BindViewModel()
        {
            viewModel.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(SettingsViewModel.DimPercent):
                        dimSlider!.Value = (int)viewModel.DimPercent;
                        dimValueLabel!.Text = $"{viewModel.DimPercent:F0}%";
                        break;
                    case nameof(SettingsViewModel.BackgroundPath):
                        bgStatusLabel!.Text = viewModel.HasBackground
                            ? Path.GetFileName(viewModel.BackgroundPath!)
                            : "No custom background loaded";
                        break;
                    case nameof(SettingsViewModel.IsBackgroundLoading):
                        loadingBar!.Visible = viewModel.IsBackgroundLoading;
                        backgroundBtn!.Enabled = !viewModel.IsBackgroundLoading;
                        break;
                }
            };

            viewModel.OptimiseAllClicked += (s, e) => OptimiseAllClicked?.Invoke(this, EventArgs.Empty);
            viewModel.UndoAllClicked += (s, e) => UndoAllClicked?.Invoke(this, EventArgs.Empty);
            viewModel.BackgroundLoadError += (s, error) =>
            {
                MessageBox.Show($"Background error: {error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };
        }

        private void ChooseBackground()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Choose Background Image",
                Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (ofd.ShowDialog() == DialogResult.OK)
                viewModel.LoadBackground(ofd.FileName);
        }

        public void PlayEntranceAnimation(float delay)
        {
            Invalidate(true);
        }
    }
}