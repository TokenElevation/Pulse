using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class GameModeTweak : ITweak
    {
        public string Id => "GameMode";
        public string Title => "Enable Windows Game Mode";
        public string Description => "Makes sure Windows Game Mode is turned on. Effect is small.";
        public bool IsAction => false;

        private const string KeyPath = @"Software\Microsoft\GameBar";
        private const string BackupPath = @"SOFTWARE\OptimizationTool\Backup\GameMode";
        private static readonly string[] ValueNames = { "AutoGameModeEnabled", "AllowAutoGameMode" };

        public bool IsApplied()
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, false);

            foreach (var name in ValueNames)
                if (key?.GetValue(name) is int v && v == 0)
                    return false;

            return true;
        }

        public void Apply()
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);
            using var backup = Registry.CurrentUser.CreateSubKey(BackupPath, true);

            foreach (var name in ValueNames)
            {
                if (backup.GetValue(name) == null && backup.GetValue(name + "_missing") == null)
                {
                    if (key.GetValue(name) is int original)
                        backup.SetValue(name, original, RegistryValueKind.DWord);
                    else
                        backup.SetValue(name + "_missing", 1, RegistryValueKind.DWord);
                }

                key.SetValue(name, 1, RegistryValueKind.DWord);
            }
        }

        public void Revert()
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);
            using (var backup = Registry.CurrentUser.OpenSubKey(BackupPath, false))
            {
                foreach (var name in ValueNames)
                {
                    if (backup?.GetValue(name) is int original)
                        key.SetValue(name, original, RegistryValueKind.DWord);
                    else
                        key.DeleteValue(name, false);
                }
            }

            Registry.CurrentUser.DeleteSubKeyTree(BackupPath, false);
        }
    }
}