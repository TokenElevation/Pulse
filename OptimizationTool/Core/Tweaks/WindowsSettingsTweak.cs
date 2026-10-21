using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace OptimizationTool.Core.Tweaks
{
    public class WindowsSettingsTweak : ITweak
    {
        public string Id => "WindowsSettingsSnap";
        public string Title => "Faster Menus & Shutdown";
        public string Description => "Reduces menu delays and speeds up shutdown by lowering app/service timeouts.";
        public bool IsAction => false;

        private sealed record Item(RegistryHive Hive, string Path, string Name, string Target, string Default);

        private static readonly Item[] Items =
        {
            new(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control", "WaitToKillServiceTimeout", "2000", "5000"),
            new(RegistryHive.CurrentUser,  @"Control Panel\Desktop",            "WaitToKillAppTimeout",     "2000", "20000"),
            new(RegistryHive.CurrentUser,  @"Control Panel\Desktop",            "HungAppTimeout",           "1000", "5000"),
        };

        public bool IsApplied()
        {
            try
            {
                foreach (var i in Items)
                    if (Read(i) != i.Target) return false;
                return true;
            }
            catch { return false; }
        }

        public void Apply()
        {
            if (!TweakBackup.Has(Id))
                foreach (var i in Items)
                    TweakBackup.Save(Id, i.Name, Read(i));
            foreach (var i in Items)
                Write(i, i.Target);

        }

        public void Revert()
        {
            foreach (var i in Items)
            {
                string? value = TweakBackup.TryLoad(Id, i.Name, out var saved) ? saved : i.Default;
                Write(i, value);
            }

            TweakBackup.Clear(Id);
        }

        // helpers

        private static string? Read(Item i)
        {
            using var baseKey = RegistryKey.OpenBaseKey(i.Hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(i.Path);
            return key?.GetValue(i.Name)?.ToString();
        }

        private static void Write(Item i, string? value)
        {
            using var baseKey = RegistryKey.OpenBaseKey(i.Hive, RegistryView.Registry64);
            using var key = baseKey.CreateSubKey(i.Path, true)
                ?? throw new InvalidOperationException($"Could not open {i.Path}");

            if (value == null) key.DeleteValue(i.Name, throwOnMissingValue: false);
            else key.SetValue(i.Name, value, RegistryValueKind.String);
        }
    }
}