using System;
using System.Runtime.InteropServices;

namespace OptimizationTool.UI
{
    public static class Native
    {
        public const int BorderColor = 0x003E3E3E;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        private static int Set(IntPtr handle, int attribute, int value) =>
            DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));

        public static void UseDarkTitleBar(IntPtr handle)
        {
            if (Set(handle, 20, 1) != 0) Set(handle, 19, 1); 
        }

        public static bool RoundCorners(IntPtr handle, int borderBgr = BorderColor) =>
            Set(handle, 33, 2) == 0 && Set(handle, 34, borderBgr) == 0;

        public static void RoundOnly(IntPtr handle) => Set(handle, 33, 2);
    }
}