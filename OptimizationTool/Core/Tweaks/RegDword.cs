using Microsoft.Win32;

namespace OptimizationTool.Core.Tweaks
{
    internal static class RegDword
    {
        public static int? Read(RegistryHive hive, string path, string name)
        {
            using var b = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var k = b.OpenSubKey(path);
            return k?.GetValue(name) is int v ? v : null;
        }

        // null = delete the value
        public static void Write(RegistryHive hive, string path, string name, int? value)
        {
            using var b = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var k = b.CreateSubKey(path, true)
                ?? throw new InvalidOperationException($"Could not open {path}");

            if (value is int v) k.SetValue(name, v, RegistryValueKind.DWord);
            else k.DeleteValue(name, throwOnMissingValue: false);
        }
    }
}