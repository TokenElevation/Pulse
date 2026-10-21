using System;
using System.IO;
using System.Text.Json;

namespace OptimizationTool.UI
{
    public class AppSettings
    {
        public string ThemeName { get; set; } = "Mono";
        public string? BackgroundPath { get; set; }
        public int DimPercent { get; set; } = 55;
        public bool Animations { get; set; } = true;
        public bool ConfirmActions { get; set; } = true;
        public bool DiscordRpcEnabled { get; set; } = true; // enabled by default

        public static string FolderPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "winoptimizer");

        private static string FilePath => Path.Combine(FolderPath, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
            catch { /* corrupt or unreadable file: start fresh */ }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(FolderPath);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* saving is best-effort */ }
        }
    }
}