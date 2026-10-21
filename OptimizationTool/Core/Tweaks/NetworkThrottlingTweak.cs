using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class NetworkThrottlingTweak : ITweak
    {
        public string Id => "NetworkThrottling";
        public string Title => "Disable Network Throttling";
        public string Description => "Disables multimedia network throttling (NetworkThrottlingIndex).";

        private const string KeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        private const string ValueName = "NetworkThrottlingIndex";

        private const int Disabled = unchecked((int)0xFFFFFFFF);
        private const int WindowsDefault = 10;

        public bool IsApplied() =>
            RegDword.Read(RegistryHive.LocalMachine, KeyPath, ValueName) == Disabled;

        public void Apply()
        {
            if (!TweakBackup.Has(Id))
                TweakBackup.Save(Id, ValueName,
                    RegDword.Read(RegistryHive.LocalMachine, KeyPath, ValueName)?.ToString());

            RegDword.Write(RegistryHive.LocalMachine, KeyPath, ValueName, Disabled);
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