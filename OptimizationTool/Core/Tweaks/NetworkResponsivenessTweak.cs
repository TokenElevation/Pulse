using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class NetworkResponsivenessTweak : ITweak
    {
        public string Id => "NetworkResponsiveness";
        public string Title => "Max Network Responsiveness";
        public string Description => "Sets SystemResponsiveness to 0 so less CPU is reserved for background tasks.";

        private const string KeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        private const string ValueName = "SystemResponsiveness";

        private const int Target = 0;
        private const int WindowsDefault = 20;   // was 2 / 14 before, both wrong

        public bool IsApplied() =>
            RegDword.Read(RegistryHive.LocalMachine, KeyPath, ValueName) == Target;

        public void Apply()
        {
            if (!TweakBackup.Has(Id))
                TweakBackup.Save(Id, ValueName,
                    RegDword.Read(RegistryHive.LocalMachine, KeyPath, ValueName)?.ToString());

            RegDword.Write(RegistryHive.LocalMachine, KeyPath, ValueName, Target);
        }

        public void Revert()
        {
            int? target = TweakBackup.TryLoad(Id, ValueName, out var saved)
                ? (int.TryParse(saved, out var n) ? n : null)
                : WindowsDefault;

            RegDword.Write(RegistryHive.LocalMachine, KeyPath, ValueName, target);
            TweakBackup.Clear(Id);
        }
    }
}