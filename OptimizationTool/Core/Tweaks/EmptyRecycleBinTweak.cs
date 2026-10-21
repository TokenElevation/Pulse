using System;
using System.Runtime.InteropServices;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class EmptyRecycleBinTweak : ITweak
    {
        public string Id => "EmptyRecycleBin";
        public string Title => "Empty Recycle Bin";
        public string Description => "Permanently empties the Recycle Bin. This cannot be undone.";
        public bool IsAction => true;

        public long LastBytesFreed { get; private set; }
        public string? LastError { get; private set; }

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        private const int S_OK = 0;
        private const int S_FALSE = 1;
        private const int E_FILE_NOT_FOUND = unchecked((int)0x80070002);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        public bool IsApplied() => false;
        public long GetCurrentSizeBytes()
        {
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
            return SHQueryRecycleBin(null, ref info) == S_OK ? info.i64Size : 0;
        }

        public void Apply()
        {
            LastError = null;
            LastBytesFreed = GetCurrentSizeBytes();

            int hr = SHEmptyRecycleBin(
                IntPtr.Zero, null,
                SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);

            if (hr != S_OK && hr != S_FALSE && hr != E_FILE_NOT_FOUND)
            {
                LastBytesFreed = 0;
                LastError = $"Could not empty the Recycle Bin (0x{hr:X8}).";
            }
        }

        public void Revert() { }
    }
}