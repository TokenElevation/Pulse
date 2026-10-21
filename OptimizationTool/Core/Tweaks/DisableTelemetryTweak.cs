using System;
using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class DisableTelemetryTweak : ITweak
    {
        public string Id => "DisableTelemetry";
        public string Title => "Reduce Data Collection";
        public string Description => "Limits diagnostic data Windows sends to Microsoft.";
        public bool IsAction => false;

        private const string PolicyKeyPath = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection";

        public bool IsApplied()
        {
            using var key = Registry.LocalMachine.OpenSubKey(PolicyKeyPath);
            int telemetry = key?.GetValue("AllowTelemetry") is int val ? val : 1;
            return telemetry == 0 && GetServiceStartType("DiagTrack") == 4; // 4 == disabled 
        }

        public void Apply()
        {
            using (var key = Registry.LocalMachine.CreateSubKey(PolicyKeyPath))
            {
                key?.SetValue("AllowTelemetry", 0, RegistryValueKind.DWord);
            }

            ConfigureService("DiagTrack", startType: 4, stopService: true); // 4 == disabled
            ConfigureService("dmwappushservice", startType: 4, stopService: true);
        }

        public void Revert()
        {
            using (var key = Registry.LocalMachine.CreateSubKey(PolicyKeyPath))
            {
                key?.SetValue("AllowTelemetry", 1, RegistryValueKind.DWord);
            }

            ConfigureService("DiagTrack", startType: 2, stopService: false); // 2 == auto
            ConfigureService("dmwappushservice", startType: 3, stopService: false); // 3 == manual
        }

        private static int GetServiceStartType(string serviceName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
                return key?.GetValue("Start") is int startType ? startType : -1;
            }
            catch { return -1; }
        }

        private static void ConfigureService(string serviceName, int startType, bool stopService)
        {
            try
            {
                // stop the service if running
                if (stopService)
                {
                    using var sc = new ServiceController(serviceName);
                    if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.Paused)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(3));
                    }
                }

                // update start type via registry ( 2 == auto , 3 == manual , 4 == disabled)
                using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}", writable: true);
                key?.SetValue("Start", startType, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DisableTelemetryTweak] Service config failed for {serviceName}: {ex.Message}");
            }
        }
    }
}