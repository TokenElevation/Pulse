using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;
using System.Diagnostics;

namespace OptimizationTool.Core.Tweaks
{
    public class WindowsIndexingTweak : ITweak
    {
        public string Id => "WindowsIndexing";
        public string Title => "Turn off Windows file indexing";
        public string Description => "Disables the Windows Search service.Start menu and Outlook searches become slower or incomplete.";
        public bool IsAction => false;

        private const string ServiceKey = @"SYSTEM\CurrentControlSet\Services\WSearch";
        private const string ServiceName = "WSearch";

        public bool IsApplied()
        {
            using var key = RegBackup.OpenHklm64(ServiceKey, false);
            return key.GetValue("Start") is int v && v == 4;
        }

        public void Apply()
        {
            using (var key = RegBackup.OpenHklm64(ServiceKey, true))
            {
                RegBackup.Save(Id, "Start", key.GetValue("Start"), RegistryValueKind.DWord);
                RegBackup.Save(Id, "DelayedAutostart", key.GetValue("DelayedAutostart"), RegistryValueKind.DWord);
            }

            RunSc($"config {ServiceName} start= disabled", 0);
            RunSc($"stop {ServiceName}", 0, 1062);
        }

        public void Revert()
        {
            int start;
            bool delayed;

            using (var key = RegBackup.OpenHklm64(ServiceKey, true))
            {
                RegBackup.Restore(Id, key, "Start", 2, RegistryValueKind.DWord);
                RegBackup.Restore(Id, key, "DelayedAutostart", 1, RegistryValueKind.DWord);

                start = key.GetValue("Start") is int s ? s : 2;
                delayed = key.GetValue("DelayedAutostart") is int d && d == 1;
            }

            string mode = start switch
            {
                2 => delayed ? "delayed-auto" : "auto",
                3 => "demand",
                4 => "disabled",
                _ => "delayed-auto"
            };
            RunSc($"config {ServiceName} start= {mode}", 0);

            if (start == 2)
                RunSc($"start {ServiceName}", 0, 1056);
        }

        private static void RunSc(string args, params int[] okCodes)
        {
            var psi = new ProcessStartInfo("sc.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var p = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start sc.exe");
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            if (!okCodes.Contains(p.ExitCode))
                throw new InvalidOperationException($"sc {args} failed (exit {p.ExitCode}): {output.Trim()}");
        }
    }
}