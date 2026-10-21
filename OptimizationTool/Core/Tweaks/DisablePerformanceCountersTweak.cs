using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class DisablePerformanceCountersTweak : ITweak
    {
        public string Id => "DisablePerfCounters";
        public string Title => "Turn off Performance Counters";
        public string Description => "Disables Windows performance monitoring in the background to free up CPU resources and reduce system overhead.";
        public bool IsAction => false;

        private const string KeyPath = @"SYSTEM\CurrentControlSet\Services\PerfOS\Performance";

        public bool IsApplied()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(KeyPath, false);
                return (int?)key?.GetValue("Disable Performance Counters") == 1;
            }
            catch { return false; }
        }

        public void Apply()
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(KeyPath, true);
                key?.SetValue("Disable Performance Counters", 1, RegistryValueKind.DWord);
            }
            catch { }
        }

        public void Revert()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(KeyPath, true);
                key?.SetValue("Disable Performance Counters", 0, RegistryValueKind.DWord);
            }
            catch { }
        }
    }
}