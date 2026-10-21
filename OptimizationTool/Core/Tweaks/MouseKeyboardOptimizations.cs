using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;
using System.Runtime.InteropServices;

namespace OptimizationTool.Core.Tweaks
{
    public class MouseKeyboardOptimizations : ITweak
    {
        public string Id => "MouseKeyboardOpts";
        public string Title => "Mouse and Keyboard Optimization";
        public string Description => "Disables mouse acceleration and reduces keyboard repeat delay.";
        public bool IsAction => false;

        private const string MousePath = @"Control Panel\Mouse";
        private const string KeyboardPath = @"Control Panel\Keyboard";
        private const string BackupPath = @"SOFTWARE\OptimizationTool\Backup\MouseKeyboardOpts";

        private static readonly (string Path, string Name, string Target, string Default)[] Values =
        {
            (MousePath,    "MouseSpeed",       "0",  "1"),
            (MousePath,    "MouseThreshold1",  "0",  "6"),
            (MousePath,    "MouseThreshold2",  "0",  "10"),
            (KeyboardPath, "KeyboardSpeed",    "31", "31"),
            (KeyboardPath, "KeyboardDelay",    "0",  "1"),
        };

        public bool IsApplied()
        {
            foreach (var (path, name, target, _) in Values)
            {
                using var key = Registry.CurrentUser.OpenSubKey(path, false);
                if (key?.GetValue(name) as string != target) return false;
            }
            return true;
        }

        public void Apply()
        {
            using (var backup = Registry.CurrentUser.CreateSubKey(BackupPath, true))
            {
                foreach (var (path, name, target, _) in Values)
                {
                    using var key = Registry.CurrentUser.OpenSubKey(path, true)
                        ?? throw new InvalidOperationException($"Registry key not found: HKCU\\{path}");

                    if (backup.GetValue(name) == null && backup.GetValue(name + "_missing") == null)
                    {
                        if (key.GetValue(name) is string original)
                            backup.SetValue(name, original, RegistryValueKind.String);
                        else
                            backup.SetValue(name + "_missing", 1, RegistryValueKind.DWord);
                    }

                    key.SetValue(name, target, RegistryValueKind.String);
                }
            }

            ApplyLive();
        }

        public void Revert()
        {
            using (var backup = Registry.CurrentUser.OpenSubKey(BackupPath, false))
            {
                foreach (var (path, name, _, defaultValue) in Values)
                {
                    using var key = Registry.CurrentUser.OpenSubKey(path, true)
                        ?? throw new InvalidOperationException($"Registry key not found: HKCU\\{path}");

                    if (backup?.GetValue(name) is string original)
                        key.SetValue(name, original, RegistryValueKind.String);
                    else if (backup?.GetValue(name + "_missing") != null)
                        key.DeleteValue(name, false);
                    else
                        key.SetValue(name, defaultValue, RegistryValueKind.String);
                }
            }

            Registry.CurrentUser.DeleteSubKeyTree(BackupPath, false);

            ApplyLive();
        }

        private static void ApplyLive()
        {
            using var mouse = Registry.CurrentUser.OpenSubKey(MousePath, false);
            using var kb = Registry.CurrentUser.OpenSubKey(KeyboardPath, false);

            int speed = ReadInt(mouse, "MouseSpeed", 1);
            int t1 = ReadInt(mouse, "MouseThreshold1", 6);
            int t2 = ReadInt(mouse, "MouseThreshold2", 10);
            int kbSpeed = ReadInt(kb, "KeyboardSpeed", 31);
            int kbDelay = ReadInt(kb, "KeyboardDelay", 1);

            const uint SPIF = 0x01 | 0x02;

            if (!SystemParametersInfo(SPI_SETMOUSE, 0, new[] { t1, t2, speed }, SPIF))
                throw new InvalidOperationException($"SPI_SETMOUSE failed ({Marshal.GetLastWin32Error()})");
            if (!SystemParametersInfo(SPI_SETKEYBOARDSPEED, (uint)kbSpeed, IntPtr.Zero, SPIF))
                throw new InvalidOperationException($"SPI_SETKEYBOARDSPEED failed ({Marshal.GetLastWin32Error()})");
            if (!SystemParametersInfo(SPI_SETKEYBOARDDELAY, (uint)kbDelay, IntPtr.Zero, SPIF))
                throw new InvalidOperationException($"SPI_SETKEYBOARDDELAY failed ({Marshal.GetLastWin32Error()})");
        }

        private static int ReadInt(RegistryKey? key, string name, int fallback) =>
            int.TryParse(key?.GetValue(name) as string, out var v) ? v : fallback;

        private const uint SPI_SETMOUSE = 0x0004;
        private const uint SPI_SETKEYBOARDSPEED = 0x000B;
        private const uint SPI_SETKEYBOARDDELAY = 0x0017;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, int[] pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
    }
}