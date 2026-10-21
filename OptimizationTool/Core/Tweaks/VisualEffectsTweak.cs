using Microsoft.Win32;
using OptimizationTool.Core.Interfaces;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace OptimizationTool.Core.Tweaks
{
    public class VisualEffectsTweak : ITweak
    {
        public string Id => "VisualEffects";
        public string Title => "Reduce Visual Effects";
        public string Description => "Disables animations and transparency for a snappier desktop.";
        public bool IsAction => false;

        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string TransparencyName = "EnableTransparency";

        private static readonly (string Name, uint Get, uint Set)[] Effects =
        {
            ("MenuAnimation",       0x1002, 0x1003),
            ("ComboBoxAnimation",   0x1004, 0x1005),
            ("ListBoxSmoothScroll", 0x1006, 0x1007),
            ("MenuFade",            0x1012, 0x1013),
            ("TooltipAnimation",    0x1016, 0x1017),
            ("SelectionFade",       0x4058, 0x4059),
            ("ClientAreaAnimation", 0x1042, 0x1043),
        };

        public bool IsApplied()
        {
            try
            {
                foreach (var e in Effects)
                    if (Spi.GetBool(e.Get) == true) return false;

                if (Spi.GetMinAnimate() == true) return false;

                return (ReadTransparency() ?? 1) == 0;
            }
            catch { return false; }
        }

        public void Apply()
        {
            SaveBackupOnce();

            foreach (var e in Effects)
                Spi.SetBool(e.Set, false);

            Spi.SetMinAnimate(false);
            WriteTransparency(0);
        }

        public void Revert()
        {
            foreach (var e in Effects)
                Spi.SetBool(e.Set, Restore(e.Name));

            Spi.SetMinAnimate(Restore("MinAnimate"));

            if (TweakBackup.TryLoad(Id, "Transparency", out var t))
                WriteTransparency(int.TryParse(t, out var n) ? n : null);
            else
                WriteTransparency(1);

            TweakBackup.Clear(Id);
        }

        // helpers

        private void SaveBackupOnce()
        {
            if (TweakBackup.Has(Id)) return; 

            foreach (var e in Effects)
                if (Spi.GetBool(e.Get) is bool on)
                    TweakBackup.Save(Id, e.Name, on ? "1" : "0");

            if (Spi.GetMinAnimate() is bool min)
                TweakBackup.Save(Id, "MinAnimate", min ? "1" : "0");

            TweakBackup.Save(Id, "Transparency", ReadTransparency()?.ToString());
        }

        private bool Restore(string name) =>
            !TweakBackup.TryLoad(Id, name, out var v) || v != "0";

        private static int? ReadTransparency()
        {
            using var k = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return k?.GetValue(TransparencyName) is int v ? v : null;
        }

        private static void WriteTransparency(int? value)
        {
            using var k = Registry.CurrentUser.CreateSubKey(PersonalizeKey, true)!;
            if (value is int v) k.SetValue(TransparencyName, v, RegistryValueKind.DWord);
            else k.DeleteValue(TransparencyName, throwOnMissingValue: false);
        }
    }
}