using Microsoft.Win32;

namespace OptimizationTool.Core.Tweaks
{
    // Stores original values per tweak under HKCU\SOFTWARE\OptimizationTool\Backup\<TweakId>
    // A null value means "did not exist", so revert can delete it again.
    internal static class TweakBackup
    {
        private const string Root = @"SOFTWARE\OptimizationTool\Backup\";

        public static bool Has(string tweakId)
        {
            using var k = Registry.CurrentUser.OpenSubKey(Root + tweakId);
            return k != null;
        }

        public static void Save(string tweakId, string name, string? value)
        {
            using var k = Registry.CurrentUser.CreateSubKey(Root + tweakId, true)!;
            k.SetValue(name + ".had", value != null ? 1 : 0, RegistryValueKind.DWord);
            k.SetValue(name, value ?? "", RegistryValueKind.String);
        }

        // Returns false if no backup exists for this name.
        public static bool TryLoad(string tweakId, string name, out string? value)
        {
            value = null;
            using var k = Registry.CurrentUser.OpenSubKey(Root + tweakId);
            if (k?.GetValue(name + ".had") is not int had) return false;

            value = had == 1 ? k.GetValue(name) as string : null;
            return true;
        }

        public static void Clear(string tweakId) =>
            Registry.CurrentUser.DeleteSubKeyTree(Root + tweakId, throwOnMissingSubKey: false);
    }
}