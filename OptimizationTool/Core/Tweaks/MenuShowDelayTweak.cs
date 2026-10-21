using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class MenuShowDelayTweak : ITweak
    {
        public string Id => "MenuShowDelay";
        public string Title => "Instant Menu Show Delay";
        public string Description => "Sets the menu popup delay to 0 ms. Applies instantly.";
        public bool IsAction => false;

        private const string KeyPath = @"Control Panel\Desktop";
        private const string ValueName = "MenuShowDelay";
        private const int WindowsDefault = 400;

        public bool IsApplied() => ReadRaw() == "0";

        public void Apply()
        {
            if (!TweakBackup.Has(Id))
                TweakBackup.Save(Id, ValueName, ReadRaw());

            Write("0");
            Spi.SetMenuShowDelay(0);
        }

        public void Revert()
        {
            string? original = TweakBackup.TryLoad(Id, ValueName, out var saved) ? saved : WindowsDefault.ToString();

            if (int.TryParse(original, out var ms))
            {
                Write(original);
                Spi.SetMenuShowDelay(ms);
            }
            else
            {
                Write(WindowsDefault.ToString());
                Spi.SetMenuShowDelay(WindowsDefault);
            }

            TweakBackup.Clear(Id);
        }

        private static string? ReadRaw()
        {
            using var k = Registry.CurrentUser.OpenSubKey(KeyPath);
            return k?.GetValue(ValueName)?.ToString();
        }

        private static void Write(string value)
        {
            using var k = Registry.CurrentUser.CreateSubKey(KeyPath, true)
                ?? throw new InvalidOperationException($"Could not open {KeyPath}");
            k.SetValue(ValueName, value, RegistryValueKind.String);
        }
    }
}