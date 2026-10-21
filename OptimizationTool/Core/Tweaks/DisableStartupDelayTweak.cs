using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

public class DisableStartupDelayTweak : ITweak
{
    public string Id => "DisableStartupDelay";
    public string Title => "Disable Startup Delay";
    public string Description => "Removes artificial Windows boot delay so desktop startup apps launch instantly.";
    public bool IsAction => false;

    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize";

    public bool IsApplied()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue("StartupDelayInMSec") is int val && val == 0;
    }

    public void Apply()
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key?.SetValue("StartupDelayInMSec", 0, RegistryValueKind.DWord);
    }

    public void Revert()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue("StartupDelayInMSec", false);
    }
}