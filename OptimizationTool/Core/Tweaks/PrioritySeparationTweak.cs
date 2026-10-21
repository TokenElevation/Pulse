using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class PrioritySeparationTweak : ITweak
    {
        public string Id => "CpuPrioritySeparation";
        public string Title => "Foreground Processor Priority";
        public string Description => "Uses short, fixed CPU time slices with a 3:1 foreground boost. Effect is small on modern Windows.";
        public bool IsAction => false;

        private const string SubKey = @"SYSTEM\CurrentControlSet\Control\PriorityControl";
        private const string ValueName = "Win32PrioritySeparation";
        private const int Target = 0x2A;
        private const int ClientDefault = 0x02; // Windows client default

        public bool IsApplied()
        {
            using var key = RegBackup.OpenHklm64(SubKey, false);
            return key.GetValue(ValueName) is int v && v == Target;
        }

        public void Apply()
        {
            using var key = RegBackup.OpenHklm64(SubKey, true);
            RegBackup.Save(Id, ValueName, key.GetValue(ValueName), RegistryValueKind.DWord);
            key.SetValue(ValueName, Target, RegistryValueKind.DWord);
        }

        public void Revert()
        {
            using var key = RegBackup.OpenHklm64(SubKey, true);
            RegBackup.Restore(Id, key, ValueName, ClientDefault, RegistryValueKind.DWord);
        }
    }
}