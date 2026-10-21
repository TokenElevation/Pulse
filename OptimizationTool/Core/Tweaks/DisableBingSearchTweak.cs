using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class DisableBingSearchTweak : ITweak
    {
        public string Id => "DisableBingSearch";
        public string Title => "Disable Web Results in Start";
        public string Description => "Disables Bing web results in Start search. Requires an Explorer restart or sign-out.                  "; // dont judge for not fixing customtweakcard instead plz autoscale was a cheaper fix
        public bool IsAction => false;

        private const string KeyPath = @"SOFTWARE\Policies\Microsoft\Windows\Explorer";
        private const string ValueName = "DisableSearchBoxSuggestions";

        public bool IsApplied()
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, false);
            return key?.GetValue(ValueName) is int v && v == 1;
        }

        public void Apply()
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);
            HkcuBackup.Save(Id, key, ValueName);
            key.SetValue(ValueName, 1, RegistryValueKind.DWord);
        }

        public void Revert()
        {
            using (var key = Registry.CurrentUser.CreateSubKey(KeyPath, true))
            {
                HkcuBackup.Restore(Id, key, ValueName);
            }

            HkcuBackup.Clear(Id);
        }
    }
}