using System.Runtime.InteropServices;

namespace OptimizationTool.Core.Tweaks
{
    internal static class Spi
    {
        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDCHANGE = 0x02;
        private const uint Persist = SPIF_UPDATEINIFILE | SPIF_SENDCHANGE;

        private const uint SPI_GETANIMATION = 0x0048;
        private const uint SPI_SETANIMATION = 0x0049;
        private const uint SPI_SETMENUSHOWDELAY = 0x006B;

        [StructLayout(LayoutKind.Sequential)]
        private struct AnimationInfo
        {
            public uint cbSize;
            public int iMinAnimate;
        }

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint param, ref int value, uint flags);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint param, IntPtr value, uint flags);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint param, ref AnimationInfo value, uint flags);

        // Generic BOOL getter/setter (menu animation, combo box animation, etc.)
        public static bool? GetBool(uint getAction)
        {
            int v = 0;
            return SystemParametersInfo(getAction, 0, ref v, 0) ? v != 0 : null;
        }

        public static bool SetBool(uint setAction, bool on) =>
            SystemParametersInfo(setAction, 0, (IntPtr)(on ? 1 : 0), Persist);

        // Minimize/maximize window animation
        public static bool? GetMinAnimate()
        {
            var info = new AnimationInfo { cbSize = (uint)Marshal.SizeOf<AnimationInfo>() };
            return SystemParametersInfo(SPI_GETANIMATION, info.cbSize, ref info, 0)
                ? info.iMinAnimate != 0
                : null;
        }

        public static bool SetMinAnimate(bool on)
        {
            var info = new AnimationInfo
            {
                cbSize = (uint)Marshal.SizeOf<AnimationInfo>(),
                iMinAnimate = on ? 1 : 0
            };
            return SystemParametersInfo(SPI_SETANIMATION, info.cbSize, ref info, Persist);
        }

        public static bool SetMenuShowDelay(int ms) =>
            SystemParametersInfo(SPI_SETMENUSHOWDELAY, (uint)ms, IntPtr.Zero, Persist);
    }
}