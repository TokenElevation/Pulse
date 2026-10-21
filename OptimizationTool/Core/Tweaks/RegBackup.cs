using System;
using Microsoft.Win32;

namespace OptimizationTool.Core.Tweaks
{
    internal static class RegBackup
    {
        private const string Root = @"SOFTWARE\OptimizationTool\Backup";
        private const string KindSuffix = "_kind";
        private const string MissingSuffix = "_missing";

        private static RegistryKey OpenBase() =>
            RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        public static RegistryKey OpenHklm64(string subKey, bool writable)
        {
            using var baseKey = OpenBase();
            return baseKey.OpenSubKey(subKey, writable)
                ?? throw new InvalidOperationException($"Registry key not found: HKLM\\{subKey}");
        }

        public static bool HasBackup(string tweakId)
        {
            using var baseKey = OpenBase();
            using var key = baseKey.OpenSubKey($@"{Root}\{tweakId}");
            return key != null;
        }

        public static void Save(string tweakId, string name, object? value, RegistryValueKind kind)
        {
            using var baseKey = OpenBase();
            using var key = baseKey.CreateSubKey($@"{Root}\{tweakId}", true);

            if (key.GetValue(name + KindSuffix) != null) return;

            key.SetValue(name + KindSuffix, (int)kind, RegistryValueKind.DWord);

            if (value == null)
                key.SetValue(name + MissingSuffix, 1, RegistryValueKind.DWord);
            else
                key.SetValue(name, value, kind);
        }

        public static void Restore(string tweakId, RegistryKey target, string name,
                                   object? fallbackDefault = null,
                                   RegistryValueKind fallbackKind = RegistryValueKind.DWord)
        {
            using var baseKey = OpenBase();
            using var key = baseKey.OpenSubKey($@"{Root}\{tweakId}");

            if (key?.GetValue(name + KindSuffix) is not int kind)
            {
                if (fallbackDefault != null)
                    target.SetValue(name, fallbackDefault, fallbackKind);
                else
                    target.DeleteValue(name, false);
                return;
            }

            if (key.GetValue(name + MissingSuffix) != null)
            {
                target.DeleteValue(name, false);
                return;
            }

            target.SetValue(name, key.GetValue(name)!, (RegistryValueKind)kind);
        }

        public static void RestoreOrDelete(string tweakId, RegistryKey target, string name) =>
            Restore(tweakId, target, name);

        public static void Clear(string tweakId)
        {
            using var baseKey = OpenBase();
            baseKey.DeleteSubKeyTree($@"{Root}\{tweakId}", false);
        }
    }
}