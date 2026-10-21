using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

public class DisableBackgroundAppsTweak : ITweak
{
    public string Id => "DisableBackgroundApps";
    public string Title => "Stop Background Apps";
    public string Description => "Prevents Microsoft Store apps from running in the background.";
    public bool IsAction => false;

    private const string UserKeyPath = @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications";
    private const string PolicyKeyPath = @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy";

    public bool IsApplied()
    {
        using var policyKey = Registry.LocalMachine.OpenSubKey(PolicyKeyPath);
        if (policyKey?.GetValue("LetAppsRunInBackground") is int policy && policy == 2)
            return true;

        using var userKey = Registry.CurrentUser.OpenSubKey(UserKeyPath);
        return userKey?.GetValue("GlobalUserDisabled") is int val && val == 1;
    }

    public void Apply()
    {
        using (var userKey = Registry.CurrentUser.CreateSubKey(UserKeyPath))
            userKey?.SetValue("GlobalUserDisabled", 1, RegistryValueKind.DWord);

        using (var policyKey = Registry.LocalMachine.CreateSubKey(PolicyKeyPath))
            policyKey?.SetValue("LetAppsRunInBackground", 2, RegistryValueKind.DWord);
    }

    public void Revert()
    {
        using (var userKey = Registry.CurrentUser.OpenSubKey(UserKeyPath, writable: true))
            userKey?.DeleteValue("GlobalUserDisabled", throwOnMissingValue: false);

        using (var policyKey = Registry.LocalMachine.OpenSubKey(PolicyKeyPath, writable: true))
            policyKey?.DeleteValue("LetAppsRunInBackground", throwOnMissingValue: false);
    }
}