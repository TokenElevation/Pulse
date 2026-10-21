using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class GpuPriorityOptimizationTweak : ITweak
    {
        public string Id => "GpuPriorityOpts";
        public string Title => "Games Task Scheduling (MMCSS)";
        public string Description => "Raises the MMCSS 'Games' task priority. Only affects apps that register with MMCSS.";
        public bool IsAction => false;

        private const string KeyPath =
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";

        public bool IsApplied()
        {
            using var key = RegBackup.OpenHklm64(KeyPath, false);
            return key.GetValue("Priority") is int p && p == 6
                && key.GetValue("Scheduling Category") is string c && c == "High"
                && key.GetValue("SFIO Priority") is string s && s == "High";
        }

        public void Apply()
        {
            using var key = RegBackup.OpenHklm64(KeyPath, true);

            RegBackup.Save(Id, "Priority", key.GetValue("Priority"), RegistryValueKind.DWord);
            RegBackup.Save(Id, "Scheduling Category", key.GetValue("Scheduling Category"), RegistryValueKind.String);
            RegBackup.Save(Id, "SFIO Priority", key.GetValue("SFIO Priority"), RegistryValueKind.String);

            key.SetValue("Priority", 6, RegistryValueKind.DWord);
            key.SetValue("Scheduling Category", "High", RegistryValueKind.String);
            key.SetValue("SFIO Priority", "High", RegistryValueKind.String);
        }

        public void Revert()
        {
            using (var key = RegBackup.OpenHklm64(KeyPath, true))
            {
                RegBackup.Restore(Id, key, "Priority", 2, RegistryValueKind.DWord);
                RegBackup.Restore(Id, key, "Scheduling Category", "Medium", RegistryValueKind.String);
                RegBackup.Restore(Id, key, "SFIO Priority", "Normal", RegistryValueKind.String);
            }

            RegBackup.Clear(Id);
        }
    }
}