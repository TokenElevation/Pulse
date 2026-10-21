using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class SystemResponsivenessTweak : ITweak
    {
        public string Id => "SystemResponsiveness";
        public string Title => "Optimize CPU Responsiveness";
        public string Description => "Reduces background task interference to guarantee more CPU cycles for active foreground applications.";
        public bool IsAction => false;

        private const string KeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

        public bool IsApplied()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(KeyPath, false);
                return (int?)key?.GetValue("SystemResponsiveness") == 0;
            }
            catch { return false; }
        }

        public void Apply()
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(KeyPath, true);
                key?.SetValue("SystemResponsiveness", 0, RegistryValueKind.DWord);
            }
            catch { }
        }

        public void Revert()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(KeyPath, true);
                key?.SetValue("SystemResponsiveness", 2, RegistryValueKind.DWord);
            }
            catch { }
        }
    }
}