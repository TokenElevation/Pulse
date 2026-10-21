using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class DisableTipsAndSuggestionsTweak : ITweak
    {
        public string Id => "DisableTipsAndSuggestions";
        public string Title => "Disable Windows Tips & Suggestions";
        public string Description => "Disables Windows suggestions, tips, and promotional screens.";
        public bool IsAction => false;

        private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

        private static readonly string[] ValueNames =
        {
            "SystemPaneSuggestionsEnabled",     // start menu suggestions
            "SubscribedContent-338388Enabled",  // start suggestions
            "SubscribedContent-338389Enabled",  // tips & tricks notifications
            "SubscribedContent-338387Enabled",  // lock screen tips
            "SubscribedContent-338393Enabled",  // settings suggestions
            "SubscribedContent-353694Enabled",  // settings suggestions
            "SubscribedContent-353696Enabled",  // settings suggestions
            "SubscribedContent-310093Enabled",  // post-update welcome experience
            "SoftLandingEnabled"                // tips
        };

        public bool IsApplied()
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, false);
            if (key == null) return false;

            foreach (var name in ValueNames)
                if (key.GetValue(name) is not int v || v != 0)
                    return false;

            return true;
        }

        public void Apply()
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);

            foreach (var name in ValueNames)
            {
                HkcuBackup.Save(Id, key, name);
                key.SetValue(name, 0, RegistryValueKind.DWord);
            }
        }

        public void Revert()
        {
            using (var key = Registry.CurrentUser.CreateSubKey(KeyPath, true))
            {
                foreach (var name in ValueNames)
                    HkcuBackup.Restore(Id, key, name);
            }

            HkcuBackup.Clear(Id);
        }
    }
}