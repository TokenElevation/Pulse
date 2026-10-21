using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;

namespace OptimizationTool.Core.Tweaks
{
    public class GameBarRecordingsTweak : ITweak
    {
        public string Id => "GameBarRecordings";
        public string Title => "Turn off Game Bar recording";
        public string Description => "Disables Game DVR capture and background recording.                                                     ";
        public bool IsAction => false;

        private const string DvrKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR";
        private const string StoreKeyPath = @"System\GameConfigStore";

        public bool IsApplied()
        {
            using var store = Registry.CurrentUser.OpenSubKey(StoreKeyPath, false);
            using var dvr = Registry.CurrentUser.OpenSubKey(DvrKeyPath, false);

            return store?.GetValue("GameDVR_Enabled") is int a && a == 0
                && dvr?.GetValue("AppCaptureEnabled") is int b && b == 0
                && !(dvr.GetValue("HistoricalCaptureEnabled") is int c && c == 1); // missing = off
        }

        public void Apply()
        {
            using var store = Registry.CurrentUser.CreateSubKey(StoreKeyPath, true);
            using var dvr = Registry.CurrentUser.CreateSubKey(DvrKeyPath, true);

            HkcuBackup.Save(Id + "_Store", store, "GameDVR_Enabled");
            HkcuBackup.Save(Id + "_Dvr", dvr, "AppCaptureEnabled");
            HkcuBackup.Save(Id + "_Dvr", dvr, "HistoricalCaptureEnabled");

            store.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);
            dvr.SetValue("AppCaptureEnabled", 0, RegistryValueKind.DWord);
            dvr.SetValue("HistoricalCaptureEnabled", 0, RegistryValueKind.DWord);
        }

        public void Revert()
        {
            using (var store = Registry.CurrentUser.CreateSubKey(StoreKeyPath, true))
            using (var dvr = Registry.CurrentUser.CreateSubKey(DvrKeyPath, true))
            {
                HkcuBackup.Restore(Id + "_Store", store, "GameDVR_Enabled");
                HkcuBackup.Restore(Id + "_Dvr", dvr, "AppCaptureEnabled");
                HkcuBackup.Restore(Id + "_Dvr", dvr, "HistoricalCaptureEnabled");
            }

            HkcuBackup.Clear(Id + "_Store");
            HkcuBackup.Clear(Id + "_Dvr");
        }
    }
}