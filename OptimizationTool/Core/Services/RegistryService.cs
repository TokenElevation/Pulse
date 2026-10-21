using System.Security;
using Microsoft.Win32;

namespace OptimizationTool.Core.Services
{
    public static class RegistryService
    {
        // main API

        public static void SetValue(RegistryHive hive, string subKey, string valueName, object value, RegistryValueKind kind)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = baseKey.CreateSubKey(subKey, true)
                    ?? throw new InvalidOperationException("CreateSubKey returned null.");
                key.SetValue(valueName, value, kind);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set {hive}\\{subKey}\\{valueName}: {ex.Message}", ex);
            }
        }

        public static object? GetValue(RegistryHive hive, string subKey, string valueName, object? defaultValue = null)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(subKey, false);
                return key?.GetValue(valueName) ?? defaultValue;
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
            {
                return defaultValue;
            }
        }

        public static void DeleteValue(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(subKey, true);
                key?.DeleteValue(valueName, throwOnMissingValue: false);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete {hive}\\{subKey}\\{valueName}: {ex.Message}", ex);
            }
        }

        public static void SetValue(RegistryKey rootKey, string subKey, string valueName, object value, RegistryValueKind valueKind) =>
            SetValue(ToHive(rootKey), subKey, valueName, value, valueKind);

        public static object? GetValue(RegistryKey rootKey, string subKey, string valueName, object? defaultValue = null) =>
            GetValue(ToHive(rootKey), subKey, valueName, defaultValue);

        private static RegistryHive ToHive(RegistryKey root) => root.Name switch
        {
            "HKEY_LOCAL_MACHINE" => RegistryHive.LocalMachine,
            "HKEY_CURRENT_USER" => RegistryHive.CurrentUser,
            "HKEY_CLASSES_ROOT" => RegistryHive.ClassesRoot,
            "HKEY_USERS" => RegistryHive.Users,
            "HKEY_CURRENT_CONFIG" => RegistryHive.CurrentConfig,
            _ => throw new ArgumentException("Pass a root hive (e.g. Registry.LocalMachine), not a subkey.", nameof(root))
        };
    }
}