using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class CoreParkingDisableTweak : ITweak
    {
        public string Id => "DisableCoreParking";
        public string Title => "Disable Core Parking";
        public string Description => "Stops Windows from parking idle CPU cores to cut micro-stutters and wake latency.";
        public bool IsAction => false;

        private static readonly Guid SubProcessor = new("54533251-82be-4824-96c1-47b60b740d00");
        private static readonly Guid SettingMinCores = new("0cc5b647-c1df-4637-891a-dec35c3185b3"); // CPMINCORES
        private static readonly Guid SettingMaxCores = new("ea062031-0e34-4ff1-9b6d-eb1059334028"); // CPMAXCORES

        private const string BackupKeyPath = @"Software\OptimizationTool\CoreParking";
        private const uint DefaultMinCores = 10;

        #region Native
        [DllImport("powrprof.dll")]
        private static extern uint PowerGetActiveScheme(IntPtr userRootKey, out IntPtr activeGuid);

        [DllImport("powrprof.dll")]
        private static extern uint PowerSetActiveScheme(IntPtr userRootKey, ref Guid schemeGuid);

        [DllImport("powrprof.dll")]
        private static extern uint PowerReadACValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, out uint value);

        [DllImport("powrprof.dll")]
        private static extern uint PowerReadDCValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, out uint value);

        [DllImport("powrprof.dll")]
        private static extern uint PowerWriteACValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, uint value);

        [DllImport("powrprof.dll")]
        private static extern uint PowerWriteDCValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, uint value);

        [DllImport("powrprof.dll")]
        private static extern uint PowerEnumerate(IntPtr root, IntPtr scheme, IntPtr sub, uint flags, uint index, IntPtr buffer, ref uint bufferSize);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);

        private const uint ACCESS_SCHEME = 16;
        #endregion

        public bool IsApplied()
        {
            try
            {
                if (!TryGetActiveScheme(out Guid scheme)) return false;

                Guid sub = SubProcessor, setting = SettingMinCores;

                if (PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out uint ac) != 0)
                    return false;

                bool dcOk = PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out uint dc) == 0;

                return ac == 100 && (!dcOk || dc == 100);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CoreParking] IsApplied failed: {ex.Message}");
                return false;
            }
        }

        public void Apply()
        {
            EnsureSettingRegistered();

            if (!TryGetActiveScheme(out Guid active))
                throw new InvalidOperationException("Core parking: could not read the active power scheme.");

            BackupIfNeeded(active, strict: true);
            Guid a = active;
            WriteValues(ref a, 100, 100, strict: true);

            foreach (Guid scheme in EnumerateSchemes())
            {
                if (scheme == active) continue;
                Guid s = scheme;
                BackupIfNeeded(s, strict: false);
                WriteValues(ref s, 100, 100, strict: false);
            }

            ReapplyActiveScheme();
        }

        public void Revert()
        {
            if (!TryGetActiveScheme(out Guid active)) return;

            foreach (Guid scheme in EnumerateSchemes())
            {
                if (!TryReadBackup(scheme, out uint ac, out uint dc)) continue;

                Guid s = scheme;
                WriteValues(ref s, ac, dc, strict: s == active);
            }

            ClearBackup();
            ReapplyActiveScheme();
        }

        // helpers

        private static void WriteValues(ref Guid scheme, uint minAc, uint minDc, bool strict)
        {
            Guid sub = SubProcessor, min = SettingMinCores, max = SettingMaxCores;

            Check(PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref min, minAc), "write AC min", strict);
            Check(PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref min, minDc), "write DC min", strict);

            Check(PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref max, 100), "write AC max", false);
            Check(PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref max, 100), "write DC max", false);
        }
        private static void EnsureSettingRegistered()
        {
            try
            {
                string path = $@"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\{SubProcessor}\{SettingMinCores}";
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = hklm.CreateSubKey(path, writable: true);
                if (key == null) return;

                key.SetValue("Attributes", 0, RegistryValueKind.DWord); // unhide
                if (key.GetValue("ValueMin") == null) key.SetValue("ValueMin", 0, RegistryValueKind.DWord);
                if (key.GetValue("ValueMax") == null) key.SetValue("ValueMax", 100, RegistryValueKind.DWord);
                if (key.GetValue("ValueIncrement") == null) key.SetValue("ValueIncrement", 1, RegistryValueKind.DWord);
                if (key.GetValue("ValueUnits") == null) key.SetValue("ValueUnits", "%", RegistryValueKind.String);
                if (key.GetValue("FriendlyName") == null) key.SetValue("FriendlyName", "Processor performance core parking min cores", RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CoreParking] EnsureSettingRegistered failed: {ex.Message}");
            }
        }

        private static void ReapplyActiveScheme()
        {
            if (TryGetActiveScheme(out Guid active))
                Check(PowerSetActiveScheme(IntPtr.Zero, ref active), "set active", false);
        }

        private static bool TryGetActiveScheme(out Guid scheme)
        {
            scheme = Guid.Empty;
            if (PowerGetActiveScheme(IntPtr.Zero, out IntPtr ptr) != 0 || ptr == IntPtr.Zero)
                return false;
            try { scheme = Marshal.PtrToStructure<Guid>(ptr); return true; }
            finally { LocalFree(ptr); }
        }

        private static IEnumerable<Guid> EnumerateSchemes()
        {
            for (uint i = 0; ; i++)
            {
                uint size = (uint)Marshal.SizeOf<Guid>();
                IntPtr buf = Marshal.AllocHGlobal((int)size);
                try
                {
                    if (PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ACCESS_SCHEME, i, buf, ref size) != 0)
                        yield break;
                    yield return Marshal.PtrToStructure<Guid>(buf);
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }

        private static void BackupIfNeeded(Guid scheme, bool strict)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(BackupKeyPath)
                    ?? throw new InvalidOperationException("Could not create backup key.");

                if (key.GetValue($"{scheme}_AC") != null) return;

                Guid sub = SubProcessor, setting = SettingMinCores;
                if (PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out uint ac) != 0) ac = DefaultMinCores;
                if (PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out uint dc) != 0) dc = DefaultMinCores;

                key.SetValue($"{scheme}_AC", (int)ac, RegistryValueKind.DWord);
                key.SetValue($"{scheme}_DC", (int)dc, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CoreParking] Backup failed: {ex.Message}");
                if (strict) throw;
            }
        }

        private static bool TryReadBackup(Guid scheme, out uint ac, out uint dc)
        {
            ac = dc = 0;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(BackupKeyPath);
                if (key?.GetValue($"{scheme}_AC") is int a && key.GetValue($"{scheme}_DC") is int d)
                {
                    ac = (uint)a;
                    dc = (uint)d;
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static void ClearBackup()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(BackupKeyPath, throwOnMissingSubKey: false); }
            catch { }
        }

        private static void Check(uint result, string what, bool strict)
        {
            if (result == 0) return;

            Debug.WriteLine($"[CoreParking] {what} failed with error {result}");
            if (!strict) return;

            if (result == 5) // ERROR_ACCESS_DENIED
                throw new UnauthorizedAccessException("Core parking: access denied. Run as administrator.");

            throw new InvalidOperationException($"Core parking: {what} failed (error {result}).");
        }
    }
}