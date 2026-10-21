using Microsoft.Win32;

namespace OptimizationTool.Services
{
    public static class RegistryBackup
    {
        private const string BackupRoot = @"SOFTWARE\Pulse\Backups";

        // one backup key per (tweak,hive/view,path,value name)
        private static string BackupPath(string tweakId, RegistryHive hive, string subKeyPath,
                                         string valueName, RegistryView view) =>
            $@"{BackupRoot}\{tweakId}\{hive}.{view}\{subKeyPath.Replace('\\', '/')}|{valueName}";

        public static void SaveOriginalValue(string tweakId, RegistryHive hive, string subKeyPath,
                                             string valueName, RegistryView view = RegistryView.Registry64)
        {
            string path = BackupPath(tweakId, hive, subKeyPath, valueName, view);

            using var backup = Registry.CurrentUser.CreateSubKey(path)
                ?? throw new InvalidOperationException($"Could not create backup key for {tweakId}.");

            if (backup.GetValue("Existed") != null) return;

            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var target = baseKey.OpenSubKey(subKeyPath);

            if (target == null || target.GetValue(valueName) == null)
            {
                backup.SetValue("Existed", 0, RegistryValueKind.DWord);
                return;
            }

            var kind = target.GetValueKind(valueName);
            var raw = target.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)!;

            backup.SetValue("Value", raw, kind);
            backup.SetValue("Kind", kind.ToString(), RegistryValueKind.String);
            backup.SetValue("Existed", 1, RegistryValueKind.DWord);
        }

        public static bool RestoreOriginalValue(string tweakId, RegistryHive hive, string subKeyPath,
                                                string valueName, RegistryView view = RegistryView.Registry64)
        {
            string path = BackupPath(tweakId, hive, subKeyPath, valueName, view);

            using (var backup = Registry.CurrentUser.OpenSubKey(path))
            {
                if (backup?.GetValue("Existed") is not int existed) return false;

                using var baseKey = RegistryKey.OpenBaseKey(hive, view);

                if (existed == 0)
                {
                    using var target = baseKey.OpenSubKey(subKeyPath, writable: true);
                    target?.DeleteValue(valueName, throwOnMissingValue: false);
                }
                else
                {
                    var raw = backup.GetValue("Value", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (raw == null ||
                        !Enum.TryParse<RegistryValueKind>(backup.GetValue("Kind") as string, out var kind))
                        return false;

                    using var target = baseKey.CreateSubKey(subKeyPath, true)
                        ?? throw new InvalidOperationException($"Could not open {hive}\\{subKeyPath}");
                    target.SetValue(valueName, raw, kind);
                }
            }

            Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
            return true;
        }
    }
}