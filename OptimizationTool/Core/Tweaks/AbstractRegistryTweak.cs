using System.Globalization;
using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;
using OptimizationTool.Services;

namespace OptimizationTool.Core.Tweaks
{
    public abstract class AbstractRegistryTweak : ITweak
    {
        public abstract string Id { get; }
        public abstract string Title { get; }
        public abstract string Description { get; }
        public virtual bool IsAction => false;

        protected abstract RegistryHive Hive { get; }
        protected abstract string SubKeyPath { get; }
        protected abstract string ValueName { get; }
        protected abstract object TargetValue { get; }
        protected abstract RegistryValueKind ValueKind { get; }

        protected virtual RegistryView View => RegistryView.Registry64;

        private object NormalizedTarget =>
            ValueKind == RegistryValueKind.DWord && TargetValue is uint u ? unchecked((int)u) :
            ValueKind == RegistryValueKind.QWord && TargetValue is ulong l ? unchecked((long)l) :
            TargetValue;

        public virtual bool IsApplied()
        {
            using var baseKey = RegistryKey.OpenBaseKey(Hive, View);
            using var key = baseKey.OpenSubKey(SubKeyPath);
            var val = key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return val != null && ValuesEqual(val, NormalizedTarget);
        }

        public virtual void Apply()
        {
            // backs up the original value
            RegistryBackup.SaveOriginalValue(Id, Hive, SubKeyPath, ValueName, View);

            using var baseKey = RegistryKey.OpenBaseKey(Hive, View);
            using var key = baseKey.CreateSubKey(SubKeyPath, true)
                ?? throw new InvalidOperationException($"Could not open {Hive}\\{SubKeyPath}");
            key.SetValue(ValueName, NormalizedTarget, ValueKind);
        }

        public virtual void Revert()
        {
            if (RegistryBackup.RestoreOriginalValue(Id, Hive, SubKeyPath, ValueName, View))
                return;

            // no backup
            using var baseKey = RegistryKey.OpenBaseKey(Hive, View);
            using var key = baseKey.OpenSubKey(SubKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }

        private static bool ValuesEqual(object actual, object target) => (actual, target) switch
        {
            (byte[] a, byte[] t) => a.AsSpan().SequenceEqual(t),
            (string[] a, string[] t) => a.SequenceEqual(t),
            _ => Equals(actual, target) ||
                 string.Equals(Convert.ToString(actual, CultureInfo.InvariantCulture),
                               Convert.ToString(target, CultureInfo.InvariantCulture),
                               StringComparison.Ordinal)
        };
    }
}