using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;
using System.Diagnostics;

namespace OptimizationTool.Core.Tweaks
{
    public class StorageOptimizationTweak : ITweak
    {
        public string Id => "StorageDiskOpts";
        public string Title => "Storage & Disk Optimization";
        public string Description => "Disables NTFS last-access timestamp writes to reduce SSD wear and boost file system performance.";
        public bool IsAction => false;

        public bool IsApplied()
        {
            try
            {
                // check NtfsDisableLastAccessUpdate via fsutil output or registry
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\FileSystem", false);
                return (int?)key?.GetValue("NtfsDisableLastAccessUpdate") == 1;
            }
            catch { return false; }
        }

        public void Apply()
        {
            try
            {
                // disable last access timestamp updates (reduces disk overhead on every read)
                using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\FileSystem", true);
                key?.SetValue("NtfsDisableLastAccessUpdate", 1, RegistryValueKind.DWord);

                // run fsutil command silently to ensure system-wide application
                var psi = new ProcessStartInfo("fsutil", "behavior set disablelastaccess 1")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Verb = "runas"
                };
                Process.Start(psi)?.WaitForExit();
            }
            catch { }
        }

        public void Revert()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\FileSystem", true);
                key?.SetValue("NtfsDisableLastAccessUpdate", 0, RegistryValueKind.DWord);

                var psi = new ProcessStartInfo("fsutil", "behavior set disablelastaccess 0")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Verb = "runas"
                };
                Process.Start(psi)?.WaitForExit();
            }
            catch { }
        }
    }
}