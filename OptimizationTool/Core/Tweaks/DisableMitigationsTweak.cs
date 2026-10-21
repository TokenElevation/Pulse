using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class DisableMitigationsTweak : ITweak
    {
        public string Id => "DisableCpuMitigations";
        public string Title => "Disable CPU Mitigations (Risk)";
        public string Description => "Disables Spectre/Meltdown kernel mitigations to recover CPU performance. Lowers security.";
        public bool IsAction => false;

        private const string MemMgmtKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";
        private const string OverrideName = "FeatureSettingsOverride";
        private const string MaskName = "FeatureSettingsOverrideMask";

        private const string BackupKey = @"SOFTWARE\OptimizationTool\Backup\DisableCpuMitigations";

        private const int MitigationBits = 0x3;

        public bool IsApplied()
        {
            try
            {
                using var hklm = OpenHklm();
                using var key = hklm.OpenSubKey(MemMgmtKey);
                if (key == null) return false;

                int ov = ReadDword(key, OverrideName) ?? 0;
                int mask = ReadDword(key, MaskName) ?? 0;

                return (ov & mask & MitigationBits) == MitigationBits;
            }
            catch { return false; }
        }

        public void Apply()
        {
            using var hklm = OpenHklm();
            using var key = hklm.CreateSubKey(MemMgmtKey, writable: true)
                ?? throw new InvalidOperationException("Could not open the Memory Management registry key.");

            int? oldOv = ReadDword(key, OverrideName);
            int? oldMask = ReadDword(key, MaskName);

            SaveBackupOnce(hklm, oldOv, oldMask);

            key.SetValue(OverrideName, (oldOv ?? 0) | MitigationBits, RegistryValueKind.DWord);
            key.SetValue(MaskName, (oldMask ?? 0) | MitigationBits, RegistryValueKind.DWord);
        }

        public void Revert()
        {
            using var hklm = OpenHklm();
            using var key = hklm.OpenSubKey(MemMgmtKey, writable: true);
            if (key == null) return;

            var backup = LoadBackup(hklm);

            if (backup is { } b)
            {
                Restore(key, OverrideName, b.HadOverride, b.Override);
                Restore(key, MaskName, b.HadMask, b.Mask);
                hklm.DeleteSubKey(BackupKey, throwOnMissingSubKey: false);
            }
            else
            {
                ClearBits(key, OverrideName);
                ClearBits(key, MaskName);
            }
        }

        // helpers

        private static RegistryKey OpenHklm() =>
            RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

        private static int? ReadDword(RegistryKey key, string name) =>
            key.GetValue(name) is int v ? v : null;

        private static void SaveBackupOnce(RegistryKey hklm, int? ov, int? mask)
        {
            using (var existing = hklm.OpenSubKey(BackupKey))
                if (existing != null) return;

            using var b = hklm.CreateSubKey(BackupKey, writable: true);
            if (b == null) return;

            b.SetValue("HadOverride", ov.HasValue ? 1 : 0, RegistryValueKind.DWord);
            b.SetValue("Override", ov ?? 0, RegistryValueKind.DWord);
            b.SetValue("HadMask", mask.HasValue ? 1 : 0, RegistryValueKind.DWord);
            b.SetValue("Mask", mask ?? 0, RegistryValueKind.DWord);
        }

        private static (bool HadOverride, int Override, bool HadMask, int Mask)? LoadBackup(RegistryKey hklm)
        {
            using var b = hklm.OpenSubKey(BackupKey);
            if (b == null) return null;

            return (
                (ReadDword(b, "HadOverride") ?? 0) == 1,
                ReadDword(b, "Override") ?? 0,
                (ReadDword(b, "HadMask") ?? 0) == 1,
                ReadDword(b, "Mask") ?? 0
            );
        }

        private static void Restore(RegistryKey key, string name, bool had, int value)
        {
            if (had) key.SetValue(name, value, RegistryValueKind.DWord);
            else key.DeleteValue(name, throwOnMissingValue: false);
        }

        private static void ClearBits(RegistryKey key, string name)
        {
            if (ReadDword(key, name) is not int v) return;

            v &= ~MitigationBits;
            if (v == 0) key.DeleteValue(name, throwOnMissingValue: false);
            else key.SetValue(name, v, RegistryValueKind.DWord);
        }
    }
}