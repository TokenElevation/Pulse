using Microsoft.Win32;

namespace OptimizationTool.Core.Tweaks
{
    internal static class HkcuBackup
    {
        private const string Root = @"SOFTWARE\OptimizationTool\Backup";

        // saves the original value once per apply cycle (cleared on revert)
        public static void Save(string tweakId, RegistryKey target, string name)
        {
            using var backup = Registry.CurrentUser.CreateSubKey($@"{Root}\{tweakId}", true);
            if (backup.GetValue(name + "_kind") != null) return;

            var value = target.GetValue(name);
            if (value == null)
            {
                backup.SetValue(name + "_kind", -1, RegistryValueKind.DWord);
                return;
            }

            var kind = target.GetValueKind(name);
            backup.SetValue(name + "_kind", (int)kind, RegistryValueKind.DWord);
            backup.SetValue(name, value, kind);
        }

        public static void Restore(string tweakId, RegistryKey target, string name)
        {
            using var backup = Registry.CurrentUser.OpenSubKey($@"{Root}\{tweakId}");

            if (backup?.GetValue(name + "_kind") is int kind && kind != -1)
                target.SetValue(name, backup.GetValue(name)!, (RegistryValueKind)kind);
            else
                target.DeleteValue(name, false);
        }

        public static void Clear(string tweakId) =>
            Registry.CurrentUser.DeleteSubKeyTree($@"{Root}\{tweakId}", false);
    }
}