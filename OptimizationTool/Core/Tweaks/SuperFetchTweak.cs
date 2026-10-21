using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;
using System.Diagnostics;

namespace OptimizationTool.Core.Tweaks
{
    public class SuperFetchTweak : ITweak
    {
        public string Id => "SuperFetchToggle";
        public string Title => "Turn off SuperFetch (SysMain)";
        public string Description => "Disables the SysMain service to stop aggressive application pre-caching and free up RAM.";
        public bool IsAction => false;

        private const string ServiceKey = @"SYSTEM\CurrentControlSet\Services\SysMain";

        public bool IsApplied()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(ServiceKey, false);
                return (int?)key?.GetValue("Start") == 4; // 4 = Disabled
            }
            catch { return false; }
        }

        public void Apply()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(ServiceKey, true);
                key?.SetValue("Start", 4, RegistryValueKind.DWord);

                var psi = new ProcessStartInfo("sc", "stop SysMain")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi)?.WaitForExit();
            }
            catch { }
        }

        public void Revert()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(ServiceKey, true);
                key?.SetValue("Start", 2, RegistryValueKind.DWord); // 2 = Automatic

                var psi = new ProcessStartInfo("sc", "start SysMain")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi)?.WaitForExit();
            }
            catch { }
        }
    }
}