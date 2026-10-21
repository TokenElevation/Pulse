using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class DisableHibernationTweak : ITweak
    {
        public string Id => "DisableHibernation";
        public string Title => "Disable Hibernation (Free GBs)";
        public string Description => "Frees disk space by disabling Hibernate and Fast Startup.";
        public bool IsAction => false;

        private const string KeyPath = @"SYSTEM\CurrentControlSet\Control\Power";
        private const string ValueName = "HibernateEnabled";

        public bool IsApplied() =>
            RegDword.Read(RegistryHive.LocalMachine, KeyPath, ValueName) == 0;

        public void Apply()
        {
            if (!TweakBackup.Has(Id))
                TweakBackup.Save(Id, ValueName,
                    RegDword.Read(RegistryHive.LocalMachine, KeyPath, ValueName)?.ToString());

            Powercfg.RunOrThrow("-h", "off");
        }

        public void Revert()
        {
            bool hadBackup = TweakBackup.TryLoad(Id, ValueName, out var saved);
            bool turnOn = !hadBackup || saved == "1";

            if (turnOn)
                Powercfg.RunOrThrow("-h", "on");

            TweakBackup.Clear(Id);
        }
    }
}