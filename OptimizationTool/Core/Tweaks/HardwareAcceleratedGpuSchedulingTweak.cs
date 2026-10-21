using System;
using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class HardwareAcceleratedGpuSchedulingTweak : ITweak
    {
        public string Id => "HAGSToggle";
        public string Title => "Hardware GPU Scheduling (HAGS)";
        public string Description => "Turns on Hardware-accelerated GPU Scheduling. Results vary.";
        public bool IsAction => false;

        public bool RequiresReboot => true;

        private const string KeyPath = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
        private const string ValueName = "HwSchMode"; // 2 = on, 1 = off, missing = OS/driver decides
        private const int MinBuild = 19041;           // Windows 10 2004

        public bool IsApplied()
        {
            using var key = RegBackup.OpenHklm64(KeyPath, false);
            return key.GetValue(ValueName) is int v && v == 2;
        }

        public void Apply()
        {
            if (Environment.OSVersion.Version.Build < MinBuild)
                throw new NotSupportedException("HAGS requires Windows 10 version 2004 or later.");

            using var key = RegBackup.OpenHklm64(KeyPath, true);
            RegBackup.Save(Id, ValueName, key.GetValue(ValueName), RegistryValueKind.DWord);
            key.SetValue(ValueName, 2, RegistryValueKind.DWord);
        }

        public void Revert()
        {
            using (var key = RegBackup.OpenHklm64(KeyPath, true))
            {
                RegBackup.RestoreOrDelete(Id, key, ValueName);
            }

            RegBackup.Clear(Id);
        }
    }
}